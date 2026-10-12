// Component test, not an installer entry point: no payload, Setup, identity bypass, or user data access.
#define wWinMain unusedProductEntry
#include "main.cpp"
#undef wWinMain
#include <iostream>

int wmain(int count, wchar_t** arguments) {
    try {
        if (count != 3) throw std::runtime_error("expected hung fixture and unique test directory");
        std::wstring root = normalize(arguments[2]); makeDirectory(root);
        std::wstring intent = root + L"\\pending-intent.sentinel";
        { std::ofstream file{std::filesystem::path(intent)}; file << "must remain"; }
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
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
