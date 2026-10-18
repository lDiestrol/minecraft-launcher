#define UNICODE
#define _UNICODE
#define NOMINMAX
#include "storage.h"
#include "protocol.h"
#include "safety.h"
#include "diagnostics.h"
#ifdef INSTALLER_DIAGNOSTIC
#include "extraction-barrier.h"
#endif
#include "lifecycle.h"
#include "layout.h"
#include <commctrl.h>
#include <shobjidl.h>
#include <thread>
#include <mutex>
#include <fstream>

constexpr UINT EventMessage = WM_APP + 1;
struct event { std::wstring kind, message; json::object fields; };
struct app {
    HINSTANCE instance{}; HWND window{}, label{}, folder{}, browse{}, paths{}, status{}, progress{}, install{}, cancel{}, details{};
    HFONT font{}; std::thread worker; std::atomic_bool stop = false, shutdown = false, workerDone = true, launchRequested = false; std::mutex pipeLock; handle input;
    std::unique_ptr<bundle> payload;
    std::wstring sid, nonce, localData, game, base, installedApp, log, technical;
    bool running = false, success = false, diagnostic = false, closing = false, pendingRetry = false; operationOutcome outcome; unsigned dpi = 96; unsigned long long payloadBytes = 0; ULONGLONG closeStarted = 0;
    std::wstring launchId;
#ifdef INSTALLER_DIAGNOSTIC
    std::unique_ptr<diagnosticEvidence> evidence;
    std::unique_ptr<extractionBarrier> barrier;
#endif
    void failureEvidence(diagnosticStage stage, const std::exception* error = nullptr) noexcept {
#ifdef INSTALLER_DIAGNOSTIC
        if (evidence) evidence->failure(stage, error);
#else
        (void)stage; (void)error;
#endif
    }
    ~app() { shutdown = true; if (worker.joinable()) { CancelSynchronousIo(worker.native_handle()); worker.join(); } input.close(); if (font) DeleteObject(font); }
    void post(std::wstring kind, std::wstring message = {}, json::object fields = {}) {
        auto e = std::make_unique<event>(event{std::move(kind), std::move(message), std::move(fields)});
        if (PostMessageW(window, EventMessage, 0, reinterpret_cast<LPARAM>(e.get()))) e.release();
    }
    void sendFromWorker(const std::wstring& command, unsigned long long id, const std::wstring& selected = {}) {
        if (GetCurrentThreadId() == GetWindowThreadProcessId(window, nullptr)) throw std::logic_error("pipe writes are forbidden on the UI thread");
        if (shutdown) return;
        if (!input.valid()) return; // stdin belongs exclusively to the single worker, never to UI callbacks.
        std::wstring body = L"{\"v\":1,\"id\":" + std::to_wstring(id) + L",\"nonce\":" + json::quote(nonce) + L",\"sid\":" + json::quote(sid) + L",\"command\":" + json::quote(command);
        if (!selected.empty()) body += L",\"baseDirectory\":" + json::quote(selected); body += L"}\n";
        std::string data = json::utf8(body);
        DWORD written = 0; check(WriteFile(input.h, data.data(), static_cast<DWORD>(data.size()), &written, nullptr)); check(written == data.size());
    }
    void pollWorkerCommands() { if (launchRequested.exchange(false)) sendFromWorker(L"launch", 5); }
    void layout() {
        int scale = static_cast<int>(dpi); auto unit = [=](int n) { return MulDiv(n, scale, 96); };
        RECT client{}; GetClientRect(window, &client); auto positions = calculateLayout(client.right, client.bottom, dpi);
        if (font) DeleteObject(font); font = CreateFontW(-unit(16), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
        for (HWND child : {label, folder, browse, paths, status, install, cancel, details}) SendMessageW(child, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        auto move = [](HWND child, const RECT& r) { MoveWindow(child, r.left, r.top, r.right - r.left, r.bottom - r.top, TRUE); };
        move(label, positions.label); move(folder, positions.folder); move(browse, positions.browse); move(paths, positions.paths); move(status, positions.status);
        move(progress, positions.progress); move(details, positions.details); move(install, positions.install); move(cancel, positions.cancel);
    }
    void preview() {
        wchar_t text[2050]{}; GetWindowTextW(folder, text, 2050);
        std::wstring selected(text); std::wstring actual = game.empty() ? selected + L"\\GameData" : game;
        std::wstring display = L"Приложение: " + selected + L"\\App\r\nИгровые данные: " + actual + L"\r\n";
        if (!game.empty()) display += L"Существующие игровые данные сохраняются без переноса.";
        if (payloadBytes) {
            display += L"\r\nИзвлечение: " + std::to_wstring(payloadBytes / (1024 * 1024)) + L" МиБ; запас для установки проверяется помощником.";
            ULARGE_INTEGER free{}; if (selected.size() >= 3 && GetDiskFreeSpaceExW(selected.substr(0, 3).c_str(), &free, nullptr, nullptr))
                display += L"\r\nСвободно на выбранном томе: " + std::to_wstring(free.QuadPart / (1024 * 1024)) + L" МиБ.";
        }
        SetWindowTextW(paths, display.c_str());
    }
    void finish(const std::wstring& message, bool ok) {
        outcome.confirm(ok);
        running = false; success = ok; SetWindowTextW(status, message.c_str()); SendMessageW(progress, PBM_SETMARQUEE, FALSE, 0);
        EnableWindow(folder, !ok); EnableWindow(browse, !ok); EnableWindow(install, TRUE); EnableWindow(cancel, TRUE);
        SetWindowTextW(install, ok ? L"&Запустить Minecraft Launcher" : L"&Повторить установку"); SetWindowTextW(cancel, L"&Закрыть");
        EnableWindow(details, TRUE);
    }
    void requestClose() {
        closing = true; shutdown = true; closeStarted = GetTickCount64();
        EnableWindow(install, FALSE); EnableWindow(cancel, FALSE);
        // Never wait for the pipe mutex or join on the UI thread while the worker is doing I/O.
        if (workerDone) DestroyWindow(window);
        else {
            if (!SetTimer(window, 2, 20, nullptr)) ExitProcess(static_cast<UINT>(outcome.code));
            if (worker.joinable()) CancelSynchronousIo(worker.native_handle());
        }
    }
    void screenshot(evidenceFile file) noexcept {
#ifdef INSTALLER_DIAGNOSTIC
        if (!evidence) return;
        try {
        RECT r{}; GetWindowRect(window, &r); int width = r.right - r.left, height = r.bottom - r.top;
        if (width <= 0 || height <= 0 || width > 4096 || height > 2048) return;
        HDC screen = GetDC(window), memory = CreateCompatibleDC(screen); HBITMAP bitmap = CreateCompatibleBitmap(screen, width, height);
        HGDIOBJ old = SelectObject(memory, bitmap); BOOL printed = PrintWindow(window, memory, 2);
        BITMAPINFO info{}; info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER); info.bmiHeader.biWidth = width; info.bmiHeader.biHeight = height; info.bmiHeader.biPlanes = 1; info.bmiHeader.biBitCount = 32;
        std::vector<BYTE> pixels(static_cast<size_t>(width) * height * 4); SelectObject(memory, old);
        if (printed && GetDIBits(memory, bitmap, 0, height, pixels.data(), &info, DIB_RGB_COLORS)) {
            BITMAPFILEHEADER header{}; header.bfType = 0x4d42; header.bfOffBits = sizeof(header) + sizeof(info.bmiHeader); header.bfSize = header.bfOffBits + static_cast<DWORD>(pixels.size());
            std::vector<BYTE> image(header.bfSize);
            memcpy(image.data(), &header, sizeof(header)); memcpy(image.data() + sizeof(header), &info.bmiHeader, sizeof(info.bmiHeader));
            memcpy(image.data() + header.bfOffBits, pixels.data(), pixels.size()); evidence->write(file, image.data(), image.size());
        }
        DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(window, screen);
        } catch (...) { }
#else
        (void)file;
#endif
    }
};

std::wstring quoteArgument(const std::wstring& value) {
    std::wstring r = L"\""; size_t slashes = 0;
    for (wchar_t c : value) {
        if (c == L'\\') { ++slashes; continue; }
        r.append(slashes * (c == L'"' ? 2 : 1), L'\\'); slashes = 0;
        if (c == L'"') r += L'\\'; r += c;
    }
    r.append(slashes * 2, L'\\'); return r + L'"';
}
std::wstring readSmall(const std::wstring& file) {
    auto h = readFile(file); auto n = length(h.h); if (n == 0 || n > 16384) throw std::runtime_error("invalid locator size");
    std::string data(static_cast<size_t>(n), '\0'); readExact(h.h, data.data(), static_cast<DWORD>(n)); return json::wide(data);
}
std::wstring discoverGame(const std::wstring& local) {
    std::wstring locator = local + L"\\MinecraftLauncherControl\\storage-root.json"; noLinks(locator);
    DWORD attributes = GetFileAttributesW(locator.c_str());
    if (attributes != INVALID_FILE_ATTRIBUTES) {
        auto text = readSmall(locator); auto root = json::parser(text).read();
        if (root.size() != 2 || json::integer(root, L"schemaVersion") != 1) throw std::runtime_error("unsupported storage locator");
        auto game = normalize(json::text(root, L"gameDataRoot")); noLinks(game); check(GetFileAttributesW(game.c_str()) != INVALID_FILE_ATTRIBUTES); return game;
    }
    DWORD error = GetLastError(); if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND) throw std::runtime_error("cannot inspect storage locator");
    std::wstring legacy = local + L"\\MinecraftLauncher"; noLinks(legacy);
    if (std::filesystem::exists(legacy) && (!std::filesystem::is_directory(legacy) || !std::filesystem::is_empty(legacy))) return legacy;
    return {};
}
bool discoverApp(const std::wstring& local) {
#ifdef INSTALLER_DIAGNOSTIC
    const std::wstring product = L"MinecraftLauncher.BootstrapperDiagnostic";
#else
    const std::wstring product = L"lDiestrol.MinecraftLauncher";
#endif
    auto path = local + L"\\" + product; noLinks(path);
    if (std::filesystem::exists(path) && (!std::filesystem::is_directory(path) || !std::filesystem::is_empty(path))) return true;
    HKEY key = nullptr; auto result = RegOpenKeyExW(HKEY_CURRENT_USER, (L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\" + product).c_str(), 0, KEY_QUERY_VALUE, &key);
    if (result == ERROR_SUCCESS) { RegCloseKey(key); return true; }
    if (result != ERROR_FILE_NOT_FOUND && result != ERROR_PATH_NOT_FOUND) throw std::runtime_error("cannot inspect current-user registration");
    return false;
}
void validateExtraction(const std::wstring& base, const std::wstring& game, const std::wstring& local) {
    if (base.size() <= 3) throw std::runtime_error("drive root rejected"); noLinks(base);
    UINT drive = GetDriveTypeW(base.substr(0, 3).c_str()); if (drive != DRIVE_FIXED && drive != DRIVE_REMOVABLE) throw std::runtime_error("network or missing drive rejected");
    std::wstring staging = base + L"\\.launcher-install"; noLinks(staging);
    std::wstring data = game.empty() ? base + L"\\GameData" : game;
    noOverlap(staging, data); noOverlap(base + L"\\App", data); noOverlap(base, local + L"\\MinecraftLauncherControl");
    for (const auto& system : {known(FOLDERID_Windows), known(FOLDERID_ProgramFiles), known(FOLDERID_ProgramFilesX86), known(FOLDERID_ProgramData)}) noOverlap(base, normalize(system));
    wchar_t temp[32768]{}; check(GetTempPathW(32768, temp) > 0); if (within(base, normalize(temp))) throw std::runtime_error("temporary install base rejected");
    if (within(executable(), base + L"\\App")) throw std::runtime_error("bootstrapper inside replaceable App");
}

struct helper {
    handle process, output, errors;
    helper(app& a, const std::wstring& root, const std::vector<HANDLE>& protection) {
#ifdef INSTALLER_DIAGNOSTIC
        if (a.barrier) throw std::runtime_error("helper forbidden in extraction barrier mode");
#endif
        std::wstring exe = root + L"\\helper\\Launcher.InstallHelper.exe"; noLinks(exe);
        SECURITY_ATTRIBUTES inherit{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE}; HANDLE inputRead = nullptr, inputWrite = nullptr, outputRead = nullptr, outputWrite = nullptr;
        check(CreatePipe(&inputRead, &inputWrite, &inherit, 0)); handle childInput(inputRead), parentInput(inputWrite); check(SetHandleInformation(parentInput.h, HANDLE_FLAG_INHERIT, 0));
        check(CreatePipe(&outputRead, &outputWrite, &inherit, 0)); output = handle(outputRead); handle childOutput(outputWrite); check(SetHandleInformation(output.h, HANDLE_FLAG_INHERIT, 0));
        std::wstring log = root + L"\\helper.log"; { std::lock_guard<std::mutex> lock(a.pipeLock); a.log = log; }
        noLinks(log); errors = handle(CreateFileW(log.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, &inherit, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(errors.valid());
        SIZE_T size = 0; InitializeProcThreadAttributeList(nullptr, 1, 0, &size); std::vector<BYTE> attributes(size); auto list = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(attributes.data());
        check(InitializeProcThreadAttributeList(list, 1, 0, &size));
        try {
            // Keep namespace/file pins alive even if this GUI crashes or reaches its shutdown watchdog.
            // These copies have only the original read/security-query rights, not write/delete rights.
            inheritedPins pins(protection); auto handles = pins.values; handles.insert(handles.end(), {childInput.h, childOutput.h, errors.h});
            check(UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handles.data(), handles.size() * sizeof(HANDLE), nullptr, nullptr));
            STARTUPINFOEXW startup{}; startup.StartupInfo.cb = sizeof(startup); startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
            startup.StartupInfo.hStdInput = childInput.h; startup.StartupInfo.hStdOutput = childOutput.h; startup.StartupInfo.hStdError = errors.h; startup.lpAttributeList = list;
            FILETIME created{}, exited{}, kernel{}, user{}; check(GetProcessTimes(GetCurrentProcess(), &created, &exited, &kernel, &user)); ULARGE_INTEGER ticks{}; ticks.LowPart = created.dwLowDateTime; ticks.HighPart = created.dwHighDateTime;
            std::wstring command = quoteArgument(exe) + L" " + a.nonce + L" " + std::to_wstring(GetCurrentProcessId()) + L" " + std::to_wstring(ticks.QuadPart + 504911232000000000ULL) + L" " + quoteArgument(a.sid);
#ifdef INSTALLER_DIAGNOSTIC
            command += L" " + a.launchId;
#endif
            struct compare { bool operator()(const std::wstring& x, const std::wstring& y) const { return _wcsicmp(x.c_str(), y.c_str()) < 0; } };
            std::map<std::wstring, std::wstring, compare> env; LPWCH inherited = GetEnvironmentStringsW(); check(inherited != nullptr);
            for (auto p = inherited; *p; p += wcslen(p) + 1) { std::wstring entry(p); auto equals = entry.find(L'=', 1); if (equals != std::wstring::npos) env[entry.substr(0, equals)] = entry.substr(equals + 1); } FreeEnvironmentStringsW(inherited);
            env[L"TEMP"] = root; env[L"TMP"] = root;
            std::wstring pinList; for (HANDLE pin : pins.values) { if (!pinList.empty()) pinList += L','; pinList += std::to_wstring(reinterpret_cast<uintptr_t>(pin)); }
            env[L"LAUNCHER_BOOTSTRAPPER_READ_PINS"] = pinList; std::wstring block; for (const auto& pair : env) { block += pair.first + L"=" + pair.second; block += L'\0'; } block += L'\0';
            PROCESS_INFORMATION createdProcess{}; std::wstring cwd = root + L"\\helper";
            check(CreateProcessW(exe.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, block.data(), cwd.c_str(), &startup.StartupInfo, &createdProcess));
            process = handle(createdProcess.hProcess); handle thread(createdProcess.hThread);
            a.input = std::move(parentInput);
        } catch (...) { DeleteProcThreadAttributeList(list); throw; }
        DeleteProcThreadAttributeList(list);
    }
    bool read(app& a, std::string& line, ULONGLONG timeout) {
        return readHelperLine(output.h, process.h, a.shutdown, line, timeout, [&] { a.pollWorkerCommands(); });
    }
};

void installWorker(app& a, std::wstring selected) {
    struct completion { app& a; ~completion() { a.workerDone = true; a.post(L"worker-finished"); } } completedWorker{a};
    diagnosticStage stage = diagnosticStage::pathValidation;
    try {
#ifdef INSTALLER_DIAGNOSTIC
        if (a.barrier) a.barrier->installRequested(a.payload->bytes);
#endif
        selected = normalize(selected); std::wstring actualGame = discoverGame(a.localData); validateExtraction(selected, actualGame, a.localData);
        // Reuse the original pinned bundle, never reopen the executable path after UI selection/retry.
        stage = diagnosticStage::space;
        bundle& payload = *a.payload; ULARGE_INTEGER available{}; check(GetDiskFreeSpaceExW(selected.substr(0, 3).c_str(), &available, nullptr, nullptr));
        if (available.QuadPart < payload.bytes + 512ULL * 1024 * 1024) throw std::runtime_error("insufficient extraction space");
        a.post(L"status", L"Подготовка установочных файлов на выбранном диске…");
        stage = diagnosticStage::namespaceAudit;
        secureNamespace namespaceGuard(selected, a.sid); stage = diagnosticStage::staging; namespaceGuard.child(selected + L"\\.launcher-install");
        std::wstring root = selected + L"\\.launcher-install\\bootstrap-" + guid(); namespaceGuard.child(root, true); stage = diagnosticStage::extraction;
#ifdef INSTALLER_DIAGNOSTIC
        if (a.barrier) {
            a.barrier->extractionStarted();
            payload.extract(root, a.stop, [&](unsigned long long count) { a.barrier->partialWrite(count, a.stop); });
            a.barrier->extractionFinished(); // Always exits this extraction-only mode before pinning/helper.
        } else
#endif
        payload.extract(root, a.stop);
        stage = diagnosticStage::payloadPin;
        auto pinnedPayload = payload.pinExtracted(root); namespaceGuard.recheck();
        if (a.stop) throw std::runtime_error("extraction cancelled"); a.post(L"status", L"Проверка установочного помощника…");
        { std::lock_guard<std::mutex> lock(a.pipeLock); a.nonce = guid(); }
        auto protection = namespaceGuard.handles(); for (const auto& file : pinnedPayload) protection.push_back(file.h);
        stage = diagnosticStage::helperStart;
        helper managed(a, root, protection); stage = diagnosticStage::helperProtocol; a.sendFromWorker(L"inspect", 1); unsigned long long active = 1; bool completed = false, launched = false;
        responseContract contract(a.nonce, a.sid, selected + L"\\App", a.launchId);
        std::string line;
        while (managed.read(a, line, active == 3 ? 16ULL * 60 * 1000 : completed ? 24ULL * 60 * 60 * 1000 : 60000)) {
            auto responseText = json::wide(line); auto fields = json::parser(responseText).read();
            auto type = contract.accept(fields);
            if (type == L"inspection" && active == 1) {
                a.post(L"inspection", {}, fields); if (a.stop) throw std::runtime_error("installation cancelled");
                active = 2; a.sendFromWorker(L"preflight", active, selected);
            } else if (type == L"preflight" && active == 2) {
                a.post(L"preflight", {}, fields); if (a.stop) throw std::runtime_error("installation cancelled"); active = 3; a.sendFromWorker(L"install", active, selected);
            } else if (type == L"progress" && active == 3 && !completed) {
                auto helperStage = json::text(fields, L"stage"); if (helperStage != L"Preflight" && helperStage != L"Storage" && helperStage != L"Setup" && helperStage != L"Verification" && helperStage != L"Completed") throw std::runtime_error("unknown installation stage");
                a.post(L"status", helperStage == L"Completed" ? L"Проверки закончены. Ожидаем итогового подтверждения помощника…" : json::text(fields, L"message"));
            } else if (type == L"success" && active == 3 && !completed) {
                if (_wcsicmp(json::text(fields, L"app").c_str(), (selected + L"\\App").c_str())) throw std::runtime_error("unexpected installed App");
                completed = true; active = 5; a.post(L"success", {}, fields);
            } else if (type == L"launched" && active == 5 && completed) { launched = true; a.post(L"launched", {}, fields); break; }
            else if (type == L"failure") { a.failureEvidence(diagnosticStage::helperFailure); a.post(L"failure", json::text(fields, L"message"), fields); break; }
            else throw std::runtime_error("unexpected helper state transition");
        }
        if (!a.shutdown) {
            if (!launched) a.failureEvidence(diagnosticStage::helperProtocol);
            if (!completed) a.post(L"ended", L"Связь с помощником завершена. Успех не подтверждён; игровые данные сохранены.");
            else if (!launched) a.post(L"failure", L"Установка была проверена, но связь с помощником потеряна. Повторите проверку установки для безопасного запуска; данные сохранены.");
        }
        a.input.close();
        WaitForSingleObject(managed.process.h, 1000); // EOF asks the helper to cancel/recover; never terminate Setup here.
    } catch (const std::exception& e) {
#ifdef INSTALLER_DIAGNOSTIC
        if (a.barrier) a.barrier->failed();
#endif
        a.failureEvidence(stage, &e);
        if (!a.shutdown) a.post(L"failure", a.stop ? L"Установка отменена. Данные сохранены; можно повторить попытку." :
            L"Установку завершить не удалось. Данные сохранены. Проверьте выбранный каталог, диск и целостность установщика.",
            {{L"detail", {json::wide(e.what()), true}}});
        a.input.close();
    }
}

void chooseFolder(app& a) {
    IFileOpenDialog* dialog = nullptr;
    if (FAILED(CoCreateInstance(CLSID_FileOpenDialog, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&dialog)))) return;
    DWORD options = 0; dialog->GetOptions(&options); dialog->SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_NOCHANGEDIR);
    dialog->SetTitle(L"Выберите основной каталог Minecraft Launcher");
    if (SUCCEEDED(dialog->Show(a.window))) {
        IShellItem* item = nullptr; if (SUCCEEDED(dialog->GetResult(&item))) {
            PWSTR path = nullptr; if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path))) { SetWindowTextW(a.folder, path); CoTaskMemFree(path); } item->Release();
        }
    } dialog->Release();
}
LRESULT CALLBACK procedure(HWND window, UINT message, WPARAM w, LPARAM l) {
    auto* a = reinterpret_cast<app*>(GetWindowLongPtrW(window, GWLP_USERDATA));
    if (message == WM_NCCREATE) { a = static_cast<app*>(reinterpret_cast<CREATESTRUCTW*>(l)->lpCreateParams); a->window = window; SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(a)); }
    if (!a) return DefWindowProcW(window, message, w, l);
    try {
        switch (message) {
        case DM_GETDEFID: return MAKELONG(107, DC_HASDEFID);
        case WM_CREATE: {
            auto control = [&](const wchar_t* cls, const wchar_t* text, DWORD style, int id) { return CreateWindowExW(0, cls, text, WS_CHILD | WS_VISIBLE | style, 0, 0, 0, 0, window, reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), a->instance, nullptr); };
            a->label = control(L"STATIC", L"Основной &каталог установки:", SS_LEFT, 109);
            a->folder = control(L"EDIT", a->base.c_str(), WS_TABSTOP | ES_AUTOHSCROLL | WS_BORDER, 101); SendMessageW(a->folder, EM_LIMITTEXT, 2048, 0);
            a->browse = control(L"BUTTON", L"&Выбрать…", WS_TABSTOP, 102); a->paths = control(L"EDIT", L"", WS_TABSTOP | WS_BORDER | WS_VSCROLL | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL, 103);
            a->status = control(L"EDIT", L"Выберите папку. App и GameData хранятся отдельно. Перенос существующих данных не выполняется.", WS_TABSTOP | WS_VSCROLL | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL, 104);
            a->progress = control(PROGRESS_CLASSW, L"", PBS_MARQUEE, 105); a->details = control(L"BUTTON", L"П&одробности", WS_TABSTOP, 106);
            a->install = control(L"BUTTON", L"&Установить", WS_TABSTOP | BS_DEFPUSHBUTTON, 107); a->cancel = control(L"BUTTON", L"&Отмена", WS_TABSTOP, 108);
            EnableWindow(a->details, FALSE); a->dpi = GetDpiForWindow(window); a->layout(); a->preview();
            if (discoverApp(a->localData)) SetWindowTextW(a->status, L"Обнаружена существующая установка. Данные сохраняются; допустимость повторной установки проверит помощник. Автоматического переноса нет.");
            if (a->diagnostic) SetTimer(window, 1, 700, nullptr); return 0;
        }
        case WM_SIZE: a->layout(); return 0;
        case WM_GETMINMAXINFO: {
            MONITORINFO monitor{sizeof(MONITORINFO)}; check(GetMonitorInfoW(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), &monitor));
            reinterpret_cast<MINMAXINFO*>(l)->ptMinTrackSize = {std::min<LONG>(MulDiv(420, a->dpi, 96), monitor.rcWork.right - monitor.rcWork.left), std::min<LONG>(MulDiv(360, a->dpi, 96), monitor.rcWork.bottom - monitor.rcWork.top)}; return 0;
        }
        case WM_DPICHANGED: {
            a->dpi = HIWORD(w); MONITORINFO monitor{sizeof(MONITORINFO)}; auto proposed = reinterpret_cast<RECT*>(l);
            check(GetMonitorInfoW(MonitorFromRect(proposed, MONITOR_DEFAULTTONEAREST), &monitor)); auto r = fitWorkArea(*proposed, monitor.rcWork);
            SetWindowPos(window, nullptr, r.left, r.top, r.right - r.left, r.bottom - r.top, SWP_NOZORDER | SWP_NOACTIVATE); a->layout(); return 0;
        }
        case WM_TIMER:
            if (w == 1) { KillTimer(window, 1); a->screenshot(evidenceFile::selectionImage); PostMessageW(window, WM_COMMAND, 107, 0); }
            else if (w == 2 && a->closing) {
                if (a->workerDone) DestroyWindow(window);
                // A stuck filesystem driver must not make join unbounded. Exit only this GUI; keep durable recovery state and installer tree untouched.
                else if (GetTickCount64() - a->closeStarted >= 5000) ExitProcess(static_cast<UINT>(a->outcome.code));
            }
            return 0;
        case WM_COMMAND:
            if (LOWORD(w) == IDOK) { PostMessageW(window, WM_COMMAND, 107, 0); return 0; }
            if (LOWORD(w) == IDCANCEL) { SendMessageW(window, WM_CLOSE, 0, 0); return 0; }
            if (LOWORD(w) == 101 && HIWORD(w) == EN_CHANGE) { a->preview(); return 0; }
            if (LOWORD(w) == 102 && !a->running) { chooseFolder(*a); return 0; }
            if (LOWORD(w) == 106) {
                std::wstring log; { std::lock_guard<std::mutex> lock(a->pipeLock); log = a->log; }
                std::wstring detail = a->technical + L"\r\nЖурнал помощника:\r\n" + log;
                if (!log.empty()) { auto h = readFile(log, FILE_SHARE_READ | FILE_SHARE_WRITE); auto n = std::min<unsigned long long>(length(h.h), 16384); std::string text(static_cast<size_t>(n), '\0'); readExact(h.h, text.data(), static_cast<DWORD>(n)); detail += L"\r\n" + json::wide(text); }
                MessageBoxW(window, detail.c_str(), L"Технические подробности", MB_OK | MB_ICONINFORMATION); return 0;
            }
            if (LOWORD(w) == 107 && !a->running && !a->closing) {
                if (a->success) { if (IsWindowEnabled(a->install)) { EnableWindow(a->install, FALSE); a->launchRequested = true; } return 0; }
                if (!a->workerDone) { a->pendingRetry = true; EnableWindow(a->install, FALSE); return 0; }
                a->pendingRetry = false;
                if (a->worker.joinable()) a->worker.join(); wchar_t text[2050]{}; GetWindowTextW(a->folder, text, 2050);
                a->outcome.begin(); a->workerDone = false; a->shutdown = false; a->launchRequested = false; a->running = true; a->stop = false; a->success = false; a->nonce.clear(); EnableWindow(a->folder, FALSE); EnableWindow(a->browse, FALSE); EnableWindow(a->install, FALSE);
                SetWindowTextW(a->cancel, L"Отменить"); SendMessageW(a->progress, PBM_SETMARQUEE, TRUE, 50);
#ifdef INSTALLER_DIAGNOSTIC
                if (a->barrier) a->barrier->installButton();
#endif
                a->worker = std::thread(installWorker, std::ref(*a), std::wstring(text)); return 0;
            }
            if (LOWORD(w) == 108) {
#ifdef INSTALLER_DIAGNOSTIC
                if (a->barrier && a->running && HIWORD(w) == BN_CLICKED && reinterpret_cast<HWND>(l) == a->cancel) a->barrier->cancelButton();
#endif
                SendMessageW(window, WM_CLOSE, 0, 0); return 0;
            } break;
        case EventMessage: {
            std::unique_ptr<event> e(reinterpret_cast<event*>(l));
            if (e->kind == L"worker-finished") {
                // A queued completion from a previous attempt must not destroy a newer worker's app.
                if (!a->workerDone) return 0;
                if (a->closing) DestroyWindow(window);
                else if (a->pendingRetry) { a->pendingRetry = false; PostMessageW(window, WM_COMMAND, 107, 0); }
                return 0;
            }
            if (a->closing) return 0;
            if (e->kind == L"inspection") {
                a->game = json::text(e->fields, L"game"); a->preview(); if (json::boolean(e->fields, L"existing")) SetWindowTextW(a->status, L"Обнаружена существующая установка. Её принятие без установочной записи или перенос не выполняются.");
            } else if (e->kind == L"preflight") {
                a->game = json::text(e->fields, L"game"); a->preview();
                auto required = json::integer(e->fields, L"requiredBytes") / (1024 * 1024), available = json::integer(e->fields, L"availableBytes") / (1024 * 1024);
                std::wstring text = L"Проверки выполнены. Нужно " + std::to_wstring(required) + L" МиБ; доступно " + std::to_wstring(available) + L" МиБ."; SetWindowTextW(a->status, text.c_str());
                a->screenshot(evidenceFile::preflightImage);
            } else if (e->kind == L"status") SetWindowTextW(a->status, e->message.c_str());
            else if (e->kind == L"success") {
                a->installedApp = json::text(e->fields, L"app"); a->game = json::text(e->fields, L"game"); a->preview(); a->finish(L"Minecraft Launcher успешно установлен.\r\n" + a->installedApp, true); a->screenshot(evidenceFile::successImage);
                if (a->diagnostic) PostMessageW(window, WM_COMMAND, 107, 0);
            } else if (e->kind == L"launched") {
#ifdef INSTALLER_DIAGNOSTIC
                if (a->diagnostic) {
                    std::string evidence = json::utf8(L"{\"schemaVersion\":1,\"launchId\":" + json::quote(json::text(e->fields, L"launchId")) + L",\"pid\":" + std::to_wstring(json::integer(e->fields, L"pid")) + L",\"startUtcTicks\":" + std::to_wstring(json::integer(e->fields, L"startUtcTicks")) + L"}");
                    if (a->evidence) a->evidence->write(evidenceFile::launched, evidence.data(), evidence.size());
                    a->screenshot(evidenceFile::launchedImage); a->requestClose();
                } else
#endif
                    SetWindowTextW(a->status, L"Minecraft Launcher запущен.");
            }
            else if (e->kind == L"failure") { a->failureEvidence(diagnosticStage::helperFailure); a->technical = e->fields.count(L"detail") ? json::text(e->fields, L"detail") : e->fields.count(L"error") ? json::text(e->fields, L"error") : L""; a->finish(e->message, false); a->screenshot(evidenceFile::failureImage); if (a->diagnostic) a->requestClose(); }
            else if (e->kind == L"ended" && a->running) { a->failureEvidence(diagnosticStage::helperProtocol); a->finish(e->message, false); a->screenshot(evidenceFile::failureImage); if (a->diagnostic) a->requestClose(); }
            return 0;
        }
        case WM_CLOSE:
            if (a->running) { a->stop = true; SetWindowTextW(a->status, L"Отмена установки… Данные и состояние восстановления сохраняются."); }
#ifdef INSTALLER_DIAGNOSTIC
            if (a->barrier) a->barrier->notifyStop();
#endif
            // The worker closes stdin (EOF => existing helper CTS cancellation). UI never locks or writes the pipe.
            if (!a->closing) a->requestClose(); return 0;
        case WM_DESTROY: PostQuitMessage(0); return 0;
        }
    } catch (const std::exception& e) { a->failureEvidence(diagnosticStage::gui, &e); a->finish(L"Ошибка установщика. Данные сохранены.\r\n" + json::wide(e.what()), false); }
    return DefWindowProcW(window, message, w, l);
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int show) {
    if (FAILED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED))) return 1;
    int result = 0;
    {
    app a; diagnosticStage stage = diagnosticStage::startup;
    try {
        a.instance = instance; a.sid = userSid(); a.localData = known(FOLDERID_LocalAppData); a.base = known(FOLDERID_Profile) + L"\\Games\\MinecraftLauncher";
#ifdef INSTALLER_DIAGNOSTIC
        a.localData = parentPath(executable()) + L"\\diagnostic-profile";
        int count = 0; LPWSTR* arguments = CommandLineToArgvW(GetCommandLineW(), &count); check(arguments != nullptr);
        localMemory argumentMemory; argumentMemory.p = arguments;
        a.launchId = guid();
        if (count == 5 && (wcscmp(arguments[1], L"--smoke") == 0 || wcscmp(arguments[1], L"--extraction-barrier") == 0)) {
            a.diagnostic = true; a.launchId = arguments[4]; stage = diagnosticStage::evidence;
            a.evidence = std::make_unique<diagnosticEvidence>(arguments[3], a.sid, a.launchId);
            if (wcscmp(arguments[1], L"--extraction-barrier") == 0) a.barrier = std::make_unique<extractionBarrier>(*a.evidence, a.sid, a.launchId);
            stage = diagnosticStage::pathValidation; a.base = normalize(arguments[2]);
        }
        else if (count != 1) throw std::runtime_error("invalid diagnostic invocation");
#endif
        stage = diagnosticStage::discovery; a.game = discoverGame(a.localData);
        stage = diagnosticStage::bundle; a.payload = std::make_unique<bundle>(); a.payloadBytes = a.payload->bytes;
        stage = diagnosticStage::window;
        INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_PROGRESS_CLASS}; InitCommonControlsEx(&controls);
        WNDCLASSW cls{}; cls.hInstance = instance; cls.lpszClassName = L"MinecraftLauncherBootstrapper"; cls.lpfnWndProc = procedure; cls.hCursor = LoadCursorW(nullptr, IDC_ARROW); cls.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
        check(RegisterClassW(&cls) != 0); check(CreateWindowExW(WS_EX_CONTROLPARENT, cls.lpszClassName, L"Установка Minecraft Launcher", WS_OVERLAPPEDWINDOW & ~(WS_MAXIMIZEBOX), CW_USEDEFAULT, CW_USEDEFAULT, 740, 520, nullptr, nullptr, instance, &a) != nullptr);
        MONITORINFO monitor{sizeof(MONITORINFO)}; check(GetMonitorInfoW(MonitorFromWindow(a.window, MONITOR_DEFAULTTONEAREST), &monitor));
        int width = std::min<int>(MulDiv(740, a.dpi, 96), monitor.rcWork.right - monitor.rcWork.left);
        int height = std::min<int>(MulDiv(520, a.dpi, 96), monitor.rcWork.bottom - monitor.rcWork.top);
        SetWindowPos(a.window, nullptr, monitor.rcWork.left + (monitor.rcWork.right - monitor.rcWork.left - width) / 2,
            monitor.rcWork.top + (monitor.rcWork.bottom - monitor.rcWork.top - height) / 2, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        ShowWindow(a.window, show); UpdateWindow(a.window); SetFocus(a.folder);
        MSG message{}; while (GetMessageW(&message, nullptr, 0, 0) > 0) if (!IsDialogMessageW(a.window, &message)) { TranslateMessage(&message); DispatchMessageW(&message); }
        result = a.outcome.code;
    } catch (const std::exception& e) {
        a.failureEvidence(stage, &e);
#ifndef INSTALLER_DIAGNOSTIC
        MessageBoxW(nullptr, (L"Не удалось подготовить установщик. Игровые данные не изменены.\r\n" + json::wide(e.what())).c_str(), L"Minecraft Launcher", MB_OK | MB_ICONERROR);
#endif
        result = 1;
    }
    } // Close handles and join the worker before COM teardown.
    CoUninitialize(); return result;
}
