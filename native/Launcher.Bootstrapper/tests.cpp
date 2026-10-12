#define UNICODE
#define _UNICODE
#define NOMINMAX
#include "storage.h"
#include "protocol.h"
#include "safety.h"
#include "lifecycle.h"
#include "layout.h"
#include <iostream>
#include <fstream>
#include <thread>

int checks = 0;
void require(bool value) { if (!value) throw std::runtime_error("test assertion after " + std::to_string(checks) + " checks"); ++checks; }
template<class F> void rejects(F action) { bool rejected = false; try { action(); } catch (const std::exception&) { rejected = true; } require(rejected); }
json::object response(unsigned id, const wchar_t* type, const std::wstring& extra = L"") {
    auto text = L"{\"v\":1,\"id\":" + std::to_wstring(id) + L",\"nonce\":\"token\",\"sid\":\"user\",\"type\":" + json::quote(type) + extra + L"}";
    return json::parser(text).read();
}
json::object launchResponse(const std::wstring& launchId = L"launch", unsigned pid = 1) {
#ifdef INSTALLER_DIAGNOSTIC
    return response(5, L"launched", L",\"launchId\":" + json::quote(launchId) + L",\"pid\":" + std::to_wstring(pid) + L",\"startUtcTicks\":42");
#else
    (void)launchId; (void)pid; return response(5, L"launched");
#endif
}
void prepare(responseContract& contract) {
    contract.accept(response(1, L"inspection", L",\"game\":\"\",\"existing\":false,\"pending\":false"));
    contract.accept(response(2, L"preflight", L",\"app\":\"D:\\\\App\",\"game\":\"D:\\\\GameData\",\"requiredBytes\":1,\"availableBytes\":2"));
}
void newSafetyTests(const std::wstring& root, bundle& payload) {
    rejects([] { requirePersistentAclFlags(0); }); rejects([] { requirePersistentAclFlags(FILE_CASE_PRESERVED_NAMES); }); requirePersistentAclFlags(FILE_PERSISTENT_ACLS); require(true);
    HANDLE raw = nullptr; check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw)); handle token(raw); DWORD size = 0;
    GetTokenInformation(token.h, TokenUser, nullptr, 0, &size); std::vector<BYTE> data(size); check(GetTokenInformation(token.h, TokenUser, data.data(), size, &size));
    LPWSTR text = nullptr; check(ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, &text)); std::wstring sid(text); LocalFree(text);
    auto descriptor = [&](const std::wstring& sddl, bool privatePayload, bool accepted) {
        localMemory memory; check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, reinterpret_cast<PSECURITY_DESCRIPTOR*>(&memory.p), nullptr));
        if (accepted) { verifyDirectoryDescriptor(memory.p, sid, privatePayload); require(true); }
        else rejects([&] { verifyDirectoryDescriptor(memory.p, sid, privatePayload); });
    };
    std::wstring protectedAcl = L"O:" + sid + L"D:P(A;OICI;GA;;;SY)(A;OICI;GA;;;" + sid + L")";
    descriptor(protectedAcl, true, true);
    for (const auto* access : {L"GA", L"WD", L"WO", L"DC", L"0x2", L"0x100"}) {
        descriptor(protectedAcl + L"(A;;" + access + L";;;AU)", false, false);
    }
    descriptor(L"O:S-1-5-32-545D:P(A;;GA;;;" + sid + L")", false, false);
    descriptor(L"O:" + sid + L"D:(A;;GA;;;SY)(A;;GA;;;" + sid + L")", true, false);
    descriptor(protectedAcl + L"(A;;GR;;;AU)", true, false);
    descriptor(protectedAcl + L"(A;;0x4;;;AU)", false, true); // Only adding unrelated siblings is allowed.
    descriptor(L"O:" + sid + L"D:NO_ACCESS_CONTROL", false, false);
    auto privateRoot = root + L"\\private-acl"; privateDirectory(privateRoot, sid);
    handle directory(CreateFileW(privateRoot.c_str(), READ_CONTROL | FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(directory.valid());
    verifyDirectoryHandle(directory.h, privateRoot, sid, true); require(true);
    rejects([&] { verifyDirectoryHandle(directory.h, root, sid, true); });
    require(MoveFileW(privateRoot.c_str(), (privateRoot + L"-replaced").c_str()) == FALSE); // Pin rejects replacement.
    localMemory insecure; check(ConvertStringSecurityDescriptorToSecurityDescriptorW((protectedAcl + L"(A;;GA;;;AU)").c_str(), SDDL_REVISION_1, reinterpret_cast<PSECURITY_DESCRIPTOR*>(&insecure.p), nullptr));
    BOOL present = FALSE, defaulted = FALSE; PACL acl = nullptr; check(GetSecurityDescriptorDacl(insecure.p, &present, &acl, &defaulted));
    check(SetNamedSecurityInfoW(privateRoot.data(), SE_FILE_OBJECT, DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, nullptr, nullptr, acl, nullptr) == ERROR_SUCCESS);
    rejects([&] { verifyDirectoryHandle(directory.h, privateRoot, sid, true); }); // Recheck catches changed ACL.
    bool extracted = false, launched = false;
    rejects([&] { requirePersistentAclFlags(0); extracted = true; launched = true; }); require(!extracted && !launched);
    auto pinned = payload.pinExtracted(root + L"\\extracted"); require(!pinned.empty());
    auto exe = root + L"\\extracted\\helper\\Launcher.InstallHelper.exe";
    handle forbidden(CreateFileW(exe.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr)); require(!forbidden.valid());
    require(MoveFileW(exe.c_str(), (exe + L".replaced").c_str()) == FALSE);
    // Real Windows namespace walk, without adopting/modifying ACLs of an existing profile or shared workspace.
    auto namespaceRoot = root.substr(0, 3) + L"LauncherBootstrapperSafety-" + guid();
    secureNamespace guard(namespaceRoot, sid); guard.child(namespaceRoot + L"\\.launcher-install");
    auto staging = namespaceRoot + L"\\.launcher-install\\bootstrap-" + guid(); guard.child(staging, true); guard.recheck(); require(true);
    require(MoveFileW(namespaceRoot.c_str(), (namespaceRoot + L"-replaced").c_str()) == FALSE);
    check(SetNamedSecurityInfoW(staging.data(), SE_FILE_OBJECT, DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, nullptr, nullptr, acl, nullptr) == ERROR_SUCCESS);
    rejects([&] { guard.recheck(); });
    // An existing collided private name is never adopted, even if its ACL belongs to us.
    { secureNamespace collision(parentPath(staging), sid, false); rejects([&] { collision.child(staging, true); }); }
    auto target = root + L"\\link-target", link = root + L"\\linked-staging"; privateDirectory(target, sid);
    check(CreateSymbolicLinkW(link.c_str(), target.c_str(), SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE) != FALSE);
    rejects([&] { noLinks(link); });
    handle linked(CreateFileW(link.c_str(), READ_CONTROL | FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(linked.valid());
    rejects([&] { verifyDirectoryHandle(linked.h, link, sid, true); }); require(std::filesystem::is_empty(target));
}
void layoutAndOutcomeTests() {
    operationOutcome result; result.begin(); result.confirm(false); result.begin(); require(result.code == 1); result.confirm(true); require(result.code == 0);
    result.begin(); result.confirm(false); require(result.code == 1);
    for (unsigned dpi : {96U, 120U, 144U, 192U}) for (int width : {840, 1366}) {
        // 1366x768 with a 40px taskbar and scaled title/frame; small window tests stacked actions.
        int height = 728 - MulDiv(40, dpi, 96), clientWidth = width - MulDiv(16, dpi, 96); auto r = calculateLayout(clientWidth, height, dpi);
        for (RECT button : {r.details, r.install, r.cancel, r.folder, r.browse, r.paths, r.status, r.progress})
            require(button.left >= 0 && button.top >= 0 && button.right <= clientWidth && button.bottom <= height && button.bottom > button.top);
        require(r.paths.bottom <= r.status.top && r.status.bottom <= r.progress.top && r.progress.bottom <= r.details.top);
        require(r.details.right <= r.install.left || r.details.bottom <= r.install.top);
    }
    for (RECT work : {RECT{0,0,1366,728}, RECT{-1920,0,0,1040}}) {
        auto r = fitWorkArea({-3000,-100,100,1500}, work); require(r.left >= work.left && r.top >= work.top && r.right <= work.right && r.bottom <= work.bottom);
    }
}
void shutdownTest(const std::wstring& root) {
    SECURITY_ATTRIBUTES attributes{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE}; HANDLE read = nullptr, write = nullptr;
    check(CreatePipe(&read, &write, &attributes, 0)); handle output(read), inherited(write); check(SetHandleInformation(output.h, HANDLE_FLAG_INHERIT, 0));
    STARTUPINFOW startup{}; startup.cb = sizeof(startup); startup.dwFlags = STARTF_USESTDHANDLES; startup.hStdOutput = inherited.h; startup.hStdError = inherited.h;
    std::wstring command = L"\"" + executable() + L"\" --hang-after-success"; PROCESS_INFORMATION created{};
    check(CreateProcessW(executable().c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &created)); handle child(created.hProcess), thread(created.hThread); inherited.close();
    std::atomic_bool shutdown = false; std::string line; require(readHelperLine(output.h, child.h, shutdown, line, 5000)); require(line == "success");
    auto sentinel = root + L"\\pending-intent.sentinel"; { std::ofstream file{std::filesystem::path(sentinel)}; file << "unchanged"; }
    std::atomic_bool done = false; std::thread worker([&] { std::string next; require(!readHelperLine(output.h, child.h, shutdown, next, 86400000)); done = true; });
    ULONGLONG started = GetTickCount64(); shutdown = true;
    while (!done && GetTickCount64() - started < 2000) Sleep(10);
    bool bounded = done; if (!bounded) { TerminateProcess(child.h, 1); shutdown = true; } worker.join(); require(bounded); require(std::filesystem::exists(sentinel));
    require(WaitForSingleObject(child.h, 0) == WAIT_TIMEOUT); // Shutdown does not kill helper/installer tree.
    check(TerminateProcess(child.h, 0)); check(WaitForSingleObject(child.h, 5000) == WAIT_OBJECT_0); // Only the explicitly created hung fixture.
}
void inheritedPinTest(const std::wstring& root) {
    auto file = root + L"\\pin-lifetime.sentinel"; { std::ofstream stream{std::filesystem::path(file)}; stream << "unchanged"; }
    auto directoryPath = root + L"\\child-pinned-directory"; makeDirectory(directoryPath);
    handle directory(CreateFileW(directoryPath.c_str(), READ_CONTROL | FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(directory.valid());
    auto pinned = readFile(file); inheritedPins pins({pinned.h, directory.h});
    SECURITY_ATTRIBUTES attributes{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE}; HANDLE read = nullptr, write = nullptr;
    check(CreatePipe(&read, &write, &attributes, 0)); handle output(read), input(write); check(SetHandleInformation(output.h, HANDLE_FLAG_INHERIT, 0));
    handle canary(CreateFileW(file.c_str(), GENERIC_READ, FILE_SHARE_READ, &attributes, OPEN_EXISTING, 0, nullptr)); check(canary.valid());
    SIZE_T size = 0; InitializeProcThreadAttributeList(nullptr, 1, 0, &size); std::vector<BYTE> buffer(size); auto list = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(buffer.data());
    check(InitializeProcThreadAttributeList(list, 1, 0, &size));
    STARTUPINFOEXW startup{}; startup.StartupInfo.cb = sizeof(startup); startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES; startup.StartupInfo.hStdOutput = input.h; startup.StartupInfo.hStdError = input.h; startup.lpAttributeList = list;
    HANDLE handles[] = {pins.values[0], pins.values[1], input.h}; check(UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handles, sizeof(handles), nullptr, nullptr));
    std::wstring command = L"\"" + executable() + L"\" --pin-child " + std::to_wstring(reinterpret_cast<uintptr_t>(pins.values[0])) + L" " + std::to_wstring(reinterpret_cast<uintptr_t>(canary.h)); PROCESS_INFORMATION created{};
    BOOL started = CreateProcessW(executable().c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT, nullptr, nullptr, &startup.StartupInfo, &created); DeleteProcThreadAttributeList(list); check(started != FALSE);
    handle child(created.hProcess), thread(created.hThread); input.close(); pins.owned.clear(); pinned.close(); directory.close(); canary.close();
    std::atomic_bool shutdown = false; std::string line; require(readHelperLine(output.h, child.h, shutdown, line, 5000)); require(line == "pinned;canary-absent");
    { handle writer(CreateFileW(file.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr)); require(!writer.valid()); }
    require(MoveFileW(file.c_str(), (file + L"-replaced").c_str()) == FALSE);
    require(MoveFileW(directoryPath.c_str(), (directoryPath + L"-replaced").c_str()) == FALSE);
    check(TerminateProcess(child.h, 0)); check(WaitForSingleObject(child.h, 5000) == WAIT_OBJECT_0);
    handle writer(CreateFileW(file.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr)); require(writer.valid());
    require(MoveFileW(directoryPath.c_str(), (directoryPath + L"-replaced").c_str()) != FALSE);
}
int wmain(int count, wchar_t** arguments) {
    try {
        if (count == 2 && wcscmp(arguments[1], L"--hang-after-success") == 0) { DWORD written = 0; check(WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), "success\n", 8, &written, nullptr)); Sleep(INFINITE); return 1; }
        if (count == 4 && wcscmp(arguments[1], L"--pin-child") == 0) {
            auto pin = reinterpret_cast<HANDLE>(static_cast<uintptr_t>(_wcstoui64(arguments[2], nullptr, 10))), canary = reinterpret_cast<HANDLE>(static_cast<uintptr_t>(_wcstoui64(arguments[3], nullptr, 10)));
            BY_HANDLE_FILE_INFORMATION info{}; check(GetFileInformationByHandle(pin, &info)); DWORD written = 0;
            if (GetFileInformationByHandle(canary, &info)) return 2;
            check(WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), "pinned;canary-absent\n", 21, &written, nullptr)); Sleep(INFINITE); return 1;
        }
        if (count != 3) throw std::runtime_error("expected bundle and disposable test folder");
        auto root = normalize(arguments[2]); makeDirectory(root);
        std::wstring unicode = L"D:\\Игры с пробелами\\\"quoted\"\\";
        require(json::wide(json::utf8(unicode)) == unicode);
        auto objectText = L"{\"value\":" + json::quote(unicode) + L"}"; require(json::text(json::parser(objectText).read(), L"value") == unicode);
        for (const auto* text : {L"[]", L"{\"v\":1,\"v\":2}", L"{\"a\":{}}", L"{\"a\":1.2}", L"{\"a\":01}", L"{\"a\":\"\\ud800\"}", L"{\"a\":1}tail"})
            rejects([&] { json::parser(std::wstring(text)).read(); });
        rejects([] { json::wide(std::string(1, static_cast<char>(0xFF))); });
        for (const auto* path : {L"//server/share/GameData", L"\\\\?\\C:\\test", L"relative", L"C:relative", L"C:\\root\\..\\other", L"C:\\a\"b", L"C:\\file:stream", L"C:\\NUL", L"C:\\trailing.", L"C:\\trailing "}) rejects([&] { normalize(path); });
        require(normalize(L"D:/Игры с пробелами/") == L"D:\\Игры с пробелами");
        rejects([] { noOverlap(L"D:\\root", L"D:\\root\\child"); }); rejects([] { noOverlap(L"D:\\root\\child", L"D:\\root"); });
        for (const auto* type : {L"success", L"launched", L"progress"}) { responseContract c(L"token", L"user", L"D:\\App", L"launch"); rejects([&] { c.accept(response(1, type)); }); }
        responseContract c(L"token", L"user", L"D:\\App", L"launch"); prepare(c);
        c.accept(response(3, L"progress", L",\"stage\":\"Completed\",\"message\":\"done\"")); require(!c.isComplete());
        rejects([&] { c.accept(response(3, L"progress", L",\"stage\":\"Setup\",\"message\":\"regression\"")); });
        rejects([&] { c.accept(response(3, L"success", L",\"app\":\"E:\\\\App\",\"game\":\"D:\\\\GameData\",\"alreadyInstalled\":false")); });
        c.accept(response(3, L"success", L",\"app\":\"D:\\\\App\",\"game\":\"D:\\\\GameData\",\"alreadyInstalled\":false")); require(c.isComplete());
#ifdef INSTALLER_DIAGNOSTIC
        rejects([&] { c.accept(launchResponse(L"old")); }); rejects([&] { c.accept(launchResponse(L"launch", 0)); });
#endif
        c.accept(launchResponse()); rejects([&] { c.accept(launchResponse()); });
        responseContract failure(L"token", L"user", L"D:\\App"); failure.accept(response(1, L"failure", L",\"error\":\"Identity\",\"message\":\"refused\"")); require(!failure.isComplete());
        rejects([&] { failure.accept(response(1, L"inspection")); });
        responseContract wrong(L"other", L"user", L"D:\\App"); rejects([&] { wrong.accept(response(1, L"inspection")); });
        responseContract noProgress(L"token", L"user", L"D:\\App"); prepare(noProgress);
        rejects([&] { noProgress.accept(response(3, L"success", L",\"app\":\"D:\\\\App\",\"game\":\"D:\\\\GameData\",\"alreadyInstalled\":false")); });
        bundle payload(arguments[1]); require(payload.bytes > 0);
        std::atomic_bool cancelled = true; auto cancelledRoot = root + L"\\cancelled"; makeDirectory(cancelledRoot);
        rejects([&] { payload.extract(cancelledRoot, cancelled); }); require(std::filesystem::is_empty(cancelledRoot));
        auto interrupted = root + L"\\interrupted"; makeDirectory(interrupted); cancelled = false; std::atomic_bool finished = false;
        std::thread cancellation([&] {
            while (!finished && GetFileAttributesW((interrupted + L"\\bootstrapper-catalog.json").c_str()) == INVALID_FILE_ATTRIBUTES) Sleep(1);
            if (!finished) cancelled = true;
        });
        bool stopped = false; try { payload.extract(interrupted, cancelled); } catch (const std::exception&) { stopped = true; }
        finished = true; cancellation.join(); require(stopped); require(cancelled); require(std::filesystem::exists(interrupted + L"\\bootstrapper-catalog.json"));
        cancelled = false; auto extracted = root + L"\\extracted"; makeDirectory(extracted); payload.extract(extracted, cancelled);
        require(std::filesystem::exists(extracted + L"\\helper\\Launcher.InstallHelper.exe"));
        rejects([&] { payload.extract(extracted, cancelled); }); // CreateNew, never overwrite.
        auto broken = root + L"\\corrupt-index.exe"; require(CopyFileW(arguments[1], broken.c_str(), TRUE) != 0);
        { std::fstream file(std::filesystem::path(broken), std::ios::in | std::ios::out | std::ios::binary); file.seekg(-1, std::ios::end); char value = 0; file.get(value); file.seekp(-1, std::ios::end); file.put(static_cast<char>(value ^ 0xFF)); }
        rejects([&] { bundle invalid(broken); });
        auto corrupt = root + L"\\corrupt-payload.exe"; require(CopyFileW(arguments[1], corrupt.c_str(), TRUE) != 0);
        { std::fstream file(std::filesystem::path(corrupt), std::ios::in | std::ios::out | std::ios::binary);
            unsigned long long index = 0, offset = 0; file.seekg(-44, std::ios::end); file.read(reinterpret_cast<char*>(&index), 8);
            file.seekg(static_cast<std::streamoff>(index + 20)); file.read(reinterpret_cast<char*>(&offset), 8); file.seekp(static_cast<std::streamoff>(offset)); file.put('x'); }
        auto badRoot = root + L"\\bad-payload"; makeDirectory(badRoot); bundle bad(corrupt); rejects([&] { bad.extract(badRoot, cancelled); });
        require(!std::filesystem::exists(badRoot + L"\\helper\\Launcher.InstallHelper.exe"));
        newSafetyTests(root, payload); layoutAndOutcomeTests(); shutdownTest(root); inheritedPinTest(root);
        std::cout << "Native checks: " << checks << " passed; retained isolated artifacts: " << json::utf8(root) << '\n'; return 0;
    } catch (const std::exception& exception) { std::cerr << exception.what() << '\n'; return 1; }
}
