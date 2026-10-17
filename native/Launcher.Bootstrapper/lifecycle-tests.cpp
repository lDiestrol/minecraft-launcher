// Component test, not an installer entry point: no payload, Setup, identity bypass, or user data access.
#define wWinMain unusedProductEntry
#include "main.cpp"
#undef wWinMain
#include <iostream>
#include <iterator>

constexpr UINT UiProbe = WM_APP + 32;
struct ownedProcess {
    handle process, thread;
    explicit ownedProcess(const PROCESS_INFORMATION& child) : process(child.hProcess), thread(child.hThread) {}
    ~ownedProcess() {
        // Only a fixture created by this test. Never resolve an arbitrary PID for cleanup.
        if (WaitForSingleObject(process.h, 0) == WAIT_TIMEOUT) { TerminateProcess(process.h, 99); WaitForSingleObject(process.h, 5000); }
    }
};
PROCESS_INFORMATION startFixture(const std::wstring& command, const std::vector<HANDLE>& handles, HANDLE input = nullptr, HANDLE output = nullptr) {
    SIZE_T size = 0; InitializeProcThreadAttributeList(nullptr, 1, 0, &size); std::vector<BYTE> data(size);
    auto list = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(data.data()); check(InitializeProcThreadAttributeList(list, 1, 0, &size));
    STARTUPINFOEXW startup{}; startup.StartupInfo.cb = sizeof(startup); startup.lpAttributeList = list;
    if (input && output) { startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES; startup.StartupInfo.hStdInput = input; startup.StartupInfo.hStdOutput = output; startup.StartupInfo.hStdError = output; }
    PROCESS_INFORMATION child{}; std::wstring mutableCommand = quoteArgument(executable()) + L" " + command;
    try {
        check(UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, const_cast<HANDLE*>(handles.data()), handles.size() * sizeof(HANDLE), nullptr, nullptr));
        check(CreateProcessW(executable().c_str(), mutableCommand.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT, nullptr, nullptr, &startup.StartupInfo, &child));
    } catch (...) { DeleteProcThreadAttributeList(list); throw; }
    DeleteProcThreadAttributeList(list); return child;
}
void createTestWindow(app& a, const std::wstring& root) {
    a.instance = GetModuleHandleW(nullptr); a.base = root; a.localData = root + L"\\isolated-control";
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_PROGRESS_CLASS}; check(InitCommonControlsEx(&controls));
    WNDCLASSW cls{}; cls.hInstance = a.instance; cls.lpszClassName = L"BootstrapperLifecycleProcessTest"; cls.lpfnWndProc = procedure;
    check(RegisterClassW(&cls) != 0);
    check(CreateWindowExW(WS_EX_CONTROLPARENT, cls.lpszClassName, L"Lifecycle test", WS_OVERLAPPEDWINDOW, 0, 0, 740, 520, nullptr, nullptr, a.instance, &a) != nullptr);
}
int initialWindowCase(const std::wstring& mode, const std::wstring& root) {
    app a; createTestWindow(a, root);
    // The first three cases deliberately never call begin/finish or send artificial success.
    if (mode == L"error") a.finish(L"failure", false);
    if (mode == L"success") a.finish(L"verified component result", true);
    if (mode == L"launch-failure") { a.finish(L"verified component result", true); a.post(L"failure", L"launch failed"); }
    if (mode == L"escape") { SetFocus(a.folder); PostMessageW(a.window, WM_KEYDOWN, VK_ESCAPE, 0); }
    else if (mode == L"cancel") SendMessageW(a.cancel, BM_CLICK, 0, 0);
    else PostMessageW(a.window, WM_CLOSE, 0, 0);
    MSG message{}; while (GetMessageW(&message, nullptr, 0, 0) > 0) if (!IsDialogMessageW(a.window, &message)) { TranslateMessage(&message); DispatchMessageW(&message); }
    return a.outcome.code;
}
int helperFixture(const std::wstring& mode) {
    DWORD written = 0; check(WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), "ready\n", 6, &written, nullptr));
    if (mode == L"unread") { Sleep(INFINITE); return 99; }
    char data[1024]; DWORD got = 0;
    if (mode == L"eof") {
        BOOL read = FALSE;
        while ((read = ReadFile(GetStdHandle(STD_INPUT_HANDLE), data, sizeof(data), &got, nullptr)) && got) { }
        check(read ? got == 0 : GetLastError() == ERROR_BROKEN_PIPE);
        check(WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), "cancelled-by-eof\n", 17, &written, nullptr)); return 0;
    }
    if (mode == L"ordered") {
        std::string line; unsigned last = 0;
        while (ReadFile(GetStdHandle(STD_INPUT_HANDLE), data, 1, &got, nullptr) && got) {
            if (*data != '\n') { line += *data; continue; }
            auto command = json::parser(json::wide(line)).read(); auto id = json::integer(command, L"id");
            check((last == 0 && id == 1) || (last == 1 && id == 2) || (last == 2 && id == 3) || (last == 3 && id == 5));
            last = static_cast<unsigned>(id); line.clear();
            if (id == 5) { check(json::text(command, L"command") == L"launch"); check(WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), "ordered:1,2,3,5\n", 16, &written, nullptr)); return 0; }
        }
        throw std::runtime_error("missing ordered launch");
    }
    throw std::runtime_error("unknown fixture mode");
}
int pipeWindowCase(const std::wstring& mode, const std::wstring& root, HANDLE input, HANDLE output, HANDLE process, HANDLE probe) {
    app a; createTestWindow(a, root); a.input = handle(input); handle reader(output), helperProcess(process), pipeProbe(probe);
    a.sid = L"component-user"; a.nonce = L"component-token"; a.outcome.begin(); a.running = true; a.workerDone = false;
    if (mode == L"launch-blocked" || mode == L"ordered") a.finish(L"verified component result", true);
    std::atomic_bool entered = false, returned = false; DWORD writeError = 0;
    a.worker = std::thread([&] {
        try {
            if (mode == L"watchdog") {
                // Model an uninterruptible driver: cancellation of synchronous I/O cannot release this wait.
                handle never(CreateEventW(nullptr, TRUE, FALSE, nullptr)); check(never.valid());
                std::lock_guard<std::mutex> lock(a.pipeLock); entered = true; WaitForSingleObject(never.h, INFINITE);
            }
            if (mode == L"cancel-blocked" || mode == L"launch-blocked") {
                std::lock_guard<std::mutex> lock(a.pipeLock); // Also prove Close/Launch do not wait on an occupied state mutex.
                entered = true; a.sendFromWorker(L"preflight", 2, std::wstring(131072, L'x')); // Much larger than the pipe quota.
                throw std::runtime_error("unread fixture unexpectedly accepted the write");
            }
            if (mode == L"ordered") { a.sendFromWorker(L"inspect", 1); a.sendFromWorker(L"preflight", 2, root); a.sendFromWorker(L"install", 3, root); }
            std::string line;
            bool read = readHelperLine(reader.h, helperProcess.h, a.shutdown, line, 10000, [&] { a.pollWorkerCommands(); });
            if (mode == L"ordered") { check(read && line == "ordered:1,2,3,5"); PostMessageW(a.window, WM_CLOSE, 0, 0); }
            else check(!read && a.shutdown);
        } catch (const std::exception&) { writeError = GetLastError(); }
        returned = true;
        a.input.close();
        WaitForSingleObject(helperProcess.h, 1000);
        a.workerDone = true; a.post(L"worker-finished");
    });
    bool blocked = mode == L"cancel-blocked" || mode == L"launch-blocked";
    if (mode == L"watchdog") {
        ULONGLONG started = GetTickCount64(); while (!entered && GetTickCount64() - started < 5000) Sleep(10); check(entered && !returned);
    }
    if (blocked) {
        DWORD available = 0, outbound = 0, inbound = 0; check(GetNamedPipeInfo(pipeProbe.h, nullptr, &outbound, &inbound, nullptr));
        ULONGLONG started = GetTickCount64();
        do { check(PeekNamedPipe(pipeProbe.h, nullptr, 0, nullptr, &available, nullptr)); if (entered && available >= (std::max)(outbound, inbound)) break; Sleep(10); } while (GetTickCount64() - started < 5000);
        Sleep(100); check(entered && !returned && available > 0 && available >= (std::max)(outbound, inbound));
    }
    if (mode == L"launch-blocked" || mode == L"ordered") {
        PostMessageW(a.window, WM_COMMAND, 107, 0); PostMessageW(a.window, WM_COMMAND, 107, 0); // Duplicate clicks must not duplicate launch.
    }
    if (mode != L"ordered") {
        // Queue before cancellation can post completion: this probe must run while app/worker are still alive.
        PostMessageW(a.window, mode == L"eof" ? WM_COMMAND : WM_CLOSE, mode == L"eof" ? 108 : 0, 0);
        PostMessageW(a.window, UiProbe, 0, 0);
    }
    ULONGLONG started = GetTickCount64(); bool responsive = false, launchSignalled = false;
    MSG message{}; while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        if (message.message == UiProbe) { responsive = true; launchSignalled = a.launchRequested; }
        if (!IsDialogMessageW(a.window, &message)) { TranslateMessage(&message); DispatchMessageW(&message); }
    }
    if (a.worker.joinable()) a.worker.join();
    check(a.workerDone && returned && GetTickCount64() - started < 3000);
    if (mode != L"ordered") check(responsive && a.shutdown);
    if (mode == L"launch-blocked") check(launchSignalled);
    if (blocked) check(writeError == ERROR_OPERATION_ABORTED);
    std::ifstream sentinel{std::filesystem::path(root + L"\\pending-intent.sentinel")};
    std::string text((std::istreambuf_iterator<char>(sentinel)), std::istreambuf_iterator<char>()); check(text == "must remain");
    return a.outcome.code;
}
void processRegressions(const std::wstring& root) {
    for (const auto* mode : {L"close", L"escape", L"cancel", L"error", L"success", L"launch-failure"}) {
        std::wstring command = L"--initial-window " + std::wstring(mode) + L" " + quoteArgument(root);
        STARTUPINFOW startup{}; startup.cb = sizeof(startup); PROCESS_INFORMATION created{}; command = quoteArgument(executable()) + L" " + command;
        check(CreateProcessW(executable().c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &created)); ownedProcess gui(created);
        check(WaitForSingleObject(gui.process.h, 8000) == WAIT_OBJECT_0); DWORD exit = 0; check(GetExitCodeProcess(gui.process.h, &exit)); check(exit == (wcscmp(mode, L"success") == 0 ? 0U : 1U));
        std::wcout << L"Initial/outcome window process: " << mode << L", exit=" << exit << L"; passed\n";
    }
    for (const auto* mode : {L"cancel-blocked", L"launch-blocked", L"eof", L"ordered", L"watchdog"}) {
        SECURITY_ATTRIBUTES inherit{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE}; HANDLE rawRead = nullptr, rawWrite = nullptr, rawOutput = nullptr, rawSink = nullptr;
        check(CreatePipe(&rawRead, &rawWrite, &inherit, 1024)); handle inputRead(rawRead), inputWrite(rawWrite);
        check(CreatePipe(&rawOutput, &rawSink, &inherit, 1024)); handle output(rawOutput), sink(rawSink);
        std::wstring fixtureMode = wcscmp(mode, L"eof") == 0 ? L"eof" : wcscmp(mode, L"ordered") == 0 ? L"ordered" : L"unread";
        ownedProcess child(startFixture(L"--fixture " + fixtureMode, {inputRead.h, sink.h}, inputRead.h, sink.h)); sink.close();
        std::atomic_bool shutdown = false; std::string line; check(readHelperLine(output.h, child.process.h, shutdown, line, 5000) && line == "ready");
        inheritedPins copies({inputWrite.h, output.h, child.process.h, inputRead.h});
        std::wstring command = L"--pipe-window " + std::wstring(mode) + L" " + quoteArgument(root);
        for (auto pin : copies.values) command += L" " + std::to_wstring(reinterpret_cast<uintptr_t>(pin));
        ownedProcess gui(startFixture(command, copies.values)); copies.owned.clear(); inputWrite.close(); inputRead.close();
        ULONGLONG started = GetTickCount64();
        check(WaitForSingleObject(gui.process.h, 8000) == WAIT_OBJECT_0); DWORD exit = 0; check(GetExitCodeProcess(gui.process.h, &exit));
        if (wcscmp(mode, L"watchdog") == 0) check(GetTickCount64() - started >= 5000 && GetTickCount64() - started < 8000);
        check(exit == (wcscmp(mode, L"launch-blocked") == 0 || wcscmp(mode, L"ordered") == 0 ? 0U : 1U));
        if (fixtureMode != L"unread") {
            check(WaitForSingleObject(child.process.h, 3000) == WAIT_OBJECT_0); DWORD childExit = 99; check(GetExitCodeProcess(child.process.h, &childExit) && childExit == 0);
            if (fixtureMode == L"eof") check(readHelperLine(output.h, child.process.h, shutdown, line, 3000) && line == "cancelled-by-eof");
        } else check(WaitForSingleObject(child.process.h, 0) == WAIT_TIMEOUT); // GUI did not kill even the hung fixture.
        std::ifstream sentinel{std::filesystem::path(root + L"\\pending-intent.sentinel")};
        std::string text((std::istreambuf_iterator<char>(sentinel)), std::istreambuf_iterator<char>()); check(text == "must remain");
        std::wcout << L"Pipe/window process: " << mode << L", exit=" << exit << L"; passed\n";
    }
}

int wmain(int count, wchar_t** arguments) {
    try {
        if (count == 3 && wcscmp(arguments[1], L"--fixture") == 0) return helperFixture(arguments[2]);
        if (count == 4 && wcscmp(arguments[1], L"--initial-window") == 0) return initialWindowCase(arguments[2], normalize(arguments[3]));
        if (count == 8 && wcscmp(arguments[1], L"--pipe-window") == 0) {
            auto value = [&](int i) { return reinterpret_cast<HANDLE>(static_cast<uintptr_t>(_wcstoui64(arguments[i], nullptr, 10))); };
            return pipeWindowCase(arguments[2], normalize(arguments[3]), value(4), value(5), value(6), value(7));
        }
        if (count != 3) throw std::runtime_error("expected hung fixture and unique test directory");
        std::wstring root = normalize(arguments[2]); makeDirectory(root);
        std::wstring intent = root + L"\\pending-intent.sentinel";
        { std::ofstream file{std::filesystem::path(intent)}; file << "must remain"; }
        processRegressions(root);
        SECURITY_ATTRIBUTES inherit{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE}; HANDLE rawRead = nullptr, rawWrite = nullptr;
        check(CreatePipe(&rawRead, &rawWrite, &inherit, 0)); handle output(rawRead), input(rawWrite); check(SetHandleInformation(output.h, HANDLE_FLAG_INHERIT, 0));
        STARTUPINFOW startup{}; startup.cb = sizeof(startup); startup.dwFlags = STARTF_USESTDHANDLES; startup.hStdOutput = input.h; startup.hStdError = input.h;
        std::wstring command = quoteArgument(arguments[1]) + L" --hang-after-success"; PROCESS_INFORMATION created{};
        check(CreateProcessW(arguments[1], command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &created));
        handle child(created.hProcess), thread(created.hThread); input.close();
        std::atomic_bool cancelled = false; std::string line; check(readHelperLine(output.h, child.h, cancelled, line, 5000) && line == "success");
        app a; a.instance = GetModuleHandleW(nullptr); a.base = root; a.localData = root + L"\\isolated-control";
        INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_PROGRESS_CLASS}; check(InitCommonControlsEx(&controls));
        WNDCLASSW cls{}; cls.hInstance = a.instance; cls.lpszClassName = L"BootstrapperLifecycleComponentTest"; cls.lpfnWndProc = procedure; check(RegisterClassW(&cls) != 0);
        check(CreateWindowExW(WS_EX_CONTROLPARENT, cls.lpszClassName, L"Lifecycle test", WS_OVERLAPPEDWINDOW, 0, 0, 740, 520, nullptr, nullptr, a.instance, &a) != nullptr);
        a.finish(L"First attempt failed", false); check(a.outcome.code == 1);
        a.outcome.begin(); check(a.outcome.code == 1); a.finish(L"Retry failed", false); check(a.outcome.code == 1);
        a.running = true; a.outcome.begin(); a.workerDone = false;
        a.worker = std::thread([&] {
            a.post(L"success", {}, {{L"app", {root + L"\\App", true}}, {L"game", {root + L"\\GameData", true}}});
            std::string pending; readHelperLine(output.h, child.h, a.shutdown, pending, 86400000);
            WaitForSingleObject(child.h, 1000); // Production bounded wait; fixture intentionally stays alive.
            a.workerDone = true; a.post(L"worker-finished");
        });
        ULONGLONG started = GetTickCount64(); bool closePosted = false, stalePosted = false, staleChecked = false;
        MSG message{};
        while (GetMessageW(&message, nullptr, 0, 0) > 0) {
            bool staleProbe = message.message == EventMessage && a.closing && !a.workerDone;
            TranslateMessage(&message); DispatchMessageW(&message);
            if (staleProbe) { check(IsWindow(a.window)); staleChecked = true; }
            if (a.success && !closePosted) { closePosted = true; PostMessageW(a.window, WM_CLOSE, 0, 0); }
            if (a.closing && !stalePosted && !a.workerDone) { stalePosted = true; a.post(L"worker-finished"); }
        }
        check(a.workerDone && closePosted && staleChecked && a.outcome.code == 0 && GetTickCount64() - started < 3000);
        if (a.worker.joinable()) a.worker.join(); // Only after completion; no app destruction with a live worker.
        std::ifstream file{std::filesystem::path(intent)}; std::string retained; std::getline(file, retained); check(retained == "must remain");
        check(WaitForSingleObject(child.h, 0) == WAIT_TIMEOUT); check(TerminateProcess(child.h, 0)); check(WaitForSingleObject(child.h, 5000) == WAIT_OBJECT_0);
        std::cout << "Native GUI lifecycle component: success -> hung helper -> WM_CLOSE -> bounded exit, intent preserved; passed\n"; return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        // Never let a failed assertion masquerade as the expected non-success product exit1.
        return count > 1 && wcsncmp(arguments[1], L"--", 2) == 0 ? 99 : 1;
    }
}
