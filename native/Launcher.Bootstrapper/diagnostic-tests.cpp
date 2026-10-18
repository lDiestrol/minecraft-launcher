// Exercise the actual worker and evidence boundary; no installer/helper is executed.
#define EXTRACTION_BARRIER_COMPONENT_TESTS
#define wWinMain unusedProductEntry
#include "main.cpp"
#undef wWinMain
#include <iostream>
#include <string>

std::string currentDiagnosticPhase = "startup";
void reportDiagnosticPhase(const std::string& phase) {
    currentDiagnosticPhase = phase;
    std::cerr << "Diagnostic phase: " << currentDiagnosticPhase << '\n';
}
int checks = 0;
void require(bool ok) { if (!ok) throw std::runtime_error("diagnostic assertion after " + std::to_string(checks)); ++checks; }
template<class F> void rejects(F action) { bool rejected = false; try { action(); } catch (const std::exception&) { rejected = true; } require(rejected); }
std::wstring fixtureSid() {
    HANDLE raw = nullptr; check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw)); handle token(raw); DWORD size = 0;
    GetTokenInformation(token.h, TokenUser, nullptr, 0, &size); std::vector<BYTE> bytes(size); check(GetTokenInformation(token.h, TokenUser, bytes.data(), size, &size));
    localMemory sid; check(ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(bytes.data())->User.Sid, reinterpret_cast<LPWSTR*>(&sid.p)));
    return static_cast<wchar_t*>(sid.p);
}
void fixtureAcl(const std::wstring& path, const std::wstring& sid, const wchar_t* extra) {
    // Changes only a newly created disposable test object, never volume/profile/existing evidence ACLs.
    localMemory descriptor; auto sddl = L"O:" + sid + L"D:P(A;OICI;GA;;;SY)(A;OICI;GA;;;" + sid + L")" + extra;
    check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, reinterpret_cast<PSECURITY_DESCRIPTOR*>(&descriptor.p), nullptr));
    BOOL present = FALSE, defaulted = FALSE; PACL acl = nullptr; check(GetSecurityDescriptorDacl(descriptor.p, &present, &acl, &defaulted));
    auto name = path;
    DWORD status = SetNamedSecurityInfoW(name.data(), SE_FILE_OBJECT,
        DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, nullptr, nullptr, acl, nullptr);
    // SetNamedSecurityInfoW RETURNS the Win32 status; GetLastError() is not authoritative here.
    if (status != ERROR_SUCCESS)
        throw std::runtime_error("fixtureAcl SetNamedSecurityInfoW failed: status=" + std::to_string(status));
}
std::string contents(const std::wstring& path) {
    auto file = readFile(path); auto size = length(file.h); require(size <= 16384); std::string data(static_cast<size_t>(size), '\0');
    readExact(file.h, data.data(), static_cast<DWORD>(size)); return data;
}
// Future Windows component regression of the real message handler + worker, using ONLY data fixtures.
// Authored here, not executed by the Debian task. No vendor/helper can run in barrier mode.
void extractionBarrierChecks(const std::wstring& root, const std::wstring& sid, const wchar_t* fixture) {
    reportDiagnosticPhase("barrier: initialize window class");
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_PROGRESS_CLASS}; check(InitCommonControlsEx(&controls));
    WNDCLASSW cls{}; cls.hInstance = GetModuleHandleW(nullptr); cls.lpszClassName = L"ExtractionBarrierComponent"; cls.lpfnWndProc = procedure;
    check(RegisterClassW(&cls) != 0);
    for (int mode : {0, 1, 2, 3, 4, 5}) {
        reportDiagnosticPhase("barrier mode " + std::to_string(mode) + ": create app and evidence");
        handle premature; // Outlives app/worker if an assertion throws.
        app a; a.instance = cls.hInstance; a.sid = sid; a.launchId = guid(); a.base = root + L"\\barrier-base-" + std::to_wstring(mode);
        a.localData = root + L"\\barrier-control-" + std::to_wstring(mode); a.payload = std::make_unique<bundle>(fixture);
        auto evidence = root + L"\\barrier-evidence-" + std::to_wstring(mode);
        a.evidence = std::make_unique<diagnosticEvidence>(evidence, sid, a.launchId);
        a.barrier = std::make_unique<extractionBarrier>(*a.evidence, sid, a.launchId);
        if (mode == 4) a.barrier->beforePublicationForTest = [&](const std::wstring& name) {
            handle unavailable(OpenEventW(EVENT_MODIFY_STATE, FALSE, name.c_str()));
            check(!unavailable.valid() && GetLastError() == ERROR_FILE_NOT_FOUND);
            premature = handle(CreateEventW(nullptr, TRUE, FALSE, name.c_str())); check(premature.valid()); check(SetEvent(premature.h));
            // This signal is in the former check->publish window. Native exclusive create must reject it.
        };
        HWND window = CreateWindowExW(0, cls.lpszClassName, L"Hidden component", WS_OVERLAPPEDWINDOW, 0, 0, 740, 520, nullptr, nullptr, cls.hInstance, &a);
        check(window != nullptr); // Not shown. WM_CREATE constructs the actual Cancel control.
        reportDiagnosticPhase("barrier mode " + std::to_string(mode) + ": start worker and observe barrier");
        SendMessageW(window, WM_COMMAND, 107, 0); // The real Install-request handler starts installWorker.
        auto deadline = GetTickCount64() + 10000; json::object reached;
        while (GetTickCount64() < deadline) {
            try { auto text = json::wide(contents(evidence + L"\\extraction-04.json")); reached = json::parser(text).read(); break; }
            catch (const std::exception&) { if (a.workerDone) break; Sleep(1); }
        }
        require(!reached.empty()); require(json::text(reached, L"event") == L"barrier_reached");
        handle continuation;
        if (mode == 2 || mode == 5) {
            // Publication precedes event creation; probe actual object availability, never guess a duration.
            deadline = GetTickCount64() + 10000;
            do {
                continuation = handle(OpenEventW(EVENT_MODIFY_STATE, FALSE, json::text(reached, L"continueEvent").c_str()));
                if (continuation.valid()) break;
                check(GetLastError() == ERROR_FILE_NOT_FOUND); Sleep(1);
            } while (!a.workerDone && GetTickCount64() < deadline);
            check(continuation.valid());
        }
        reportDiagnosticPhase("barrier mode " + std::to_string(mode) + ": control cancellation/continue");
        if (mode == 0 || mode == 5) {
            SendMessageW(a.cancel, BM_CLICK, 0, 0); // Synthetic control activation: BN_CLICKED -> WM_COMMAND108 -> WM_CLOSE, not proof of a human click.
            if (mode == 5) check(SetEvent(continuation.h)); // Observed Cancel must win over concurrent/pending Continue.
        } else if (mode == 1) SendMessageW(window, WM_CLOSE, 0, 0);
        else if (mode == 2) check(SetEvent(continuation.h));
        deadline = GetTickCount64() + (mode == 3 ? 40000 : 10000); // Missing controller must time out fail-closed.
        while (!a.workerDone && GetTickCount64() < deadline) Sleep(1);
        reportDiagnosticPhase("barrier mode " + std::to_string(mode) + ": verify worker and journal");
        require(a.workerDone); a.worker.join();
        require(!a.input.valid()); require(a.log.empty()); require(a.outcome.code == 1);
        require(!std::filesystem::exists(a.base + L"\\App")); require(!std::filesystem::exists(a.base + L"\\GameData"));
        require(!std::filesystem::exists(a.localData));
        try {
            auto fifth = json::wide(contents(evidence + L"\\extraction-05.json")); auto fields = json::parser(fifth).read();
            require(json::text(fields, L"event") == ((mode == 0 || mode == 5) ? L"gui_cancel" : mode == 2 ? L"barrier_continue" : L"barrier_failed"));
            auto last = json::wide(contents(evidence + ((mode == 0 || mode == 5) ? L"\\extraction-08.json" : L"\\extraction-07.json")));
            require(json::text(json::parser(last).read(), L"event") == L"helper_not_started");
        } catch (const std::exception& error) {
            // Test-only, bounded diagnostic snapshot. Read fixed evidence filenames only; never modify a receipt.
            // This identifies missing phases and a missed synthetic BN_CLICKED without weakening assertions.
            std::cerr << "Barrier journal verification failed: mode=" << mode << ", error=" << error.what()
                      << ", stop=" << a.stop.load() << ", shutdown=" << a.shutdown.load()
                      << ", running=" << a.running << ", closing=" << a.closing
                      << ", workerDone=" << a.workerDone.load() << ", outcome=" << a.outcome.code << '\n';
            for (int sequence = 1; sequence <= 10; ++sequence) {
                std::wstring name = L"extraction-" + std::wstring(sequence < 10 ? L"0" : L"") + std::to_wstring(sequence) + L".json";
                std::wstring path = evidence + L"\\" + name;
                DWORD attrs = GetFileAttributesW(path.c_str());
                if (attrs == INVALID_FILE_ATTRIBUTES) {
                    DWORD status = GetLastError();
                    std::cerr << "  extraction-" << sequence << ": absent (win32=" << status << ")\\n";
                    continue;
                }
                try {
                    auto file = readFile(path); auto size = length(file.h);
                    if (size == 0 || size > 16384) {
                        std::cerr << "  extraction-" << sequence << ": invalid size=" << size << '\n';
                        continue;
                    }
                    std::string bytes(static_cast<size_t>(size), '\0');
                    readExact(file.h, bytes.data(), static_cast<DWORD>(size));
                    auto fields = json::parser(json::wide(bytes)).read();
                    std::cerr << "  extraction-" << sequence << ": " << json::utf8(json::text(fields, L"event")) << '\n';
                } catch (const std::exception& readError) {
                    std::cerr << "  extraction-" << sequence << ": unreadable (" << readError.what() << ")\\n";
                }
            }
            throw;
        }
        // WM_CLOSE may already destroy the window if the worker finishes before requestClose checks it.
        if (IsWindow(window)) DestroyWindow(window);
        MSG pending{}; while (PeekMessageW(&pending, nullptr, 0, 0, PM_REMOVE)) {
            if (pending.message == EventMessage) delete reinterpret_cast<event*>(pending.lParam);
        } // Discard stale events/WM_QUIT from this owned hidden window.
    }
    reportDiagnosticPhase("barrier: unregister test window class");
    check(UnregisterClassW(cls.lpszClassName, cls.hInstance));
}
int wmain(int count, wchar_t** arguments) {
    try {
        reportDiagnosticPhase("validate arguments and user SID");
        if (count != 3) throw std::runtime_error("expected data bundle and fresh test folder");
        auto sid = fixtureSid(), root = normalize(arguments[2]), id = std::wstring(32, L'a');
        reportDiagnosticPhase("initialize isolated namespace");
        secureNamespace testRoot(parentPath(root), sid, false); testRoot.child(root, true);
        reportDiagnosticPhase("initialize evidence and unsafe ACL fixture");
        auto evidencePath = root + L"\\evidence"; diagnosticEvidence evidence(evidencePath, sid, id);
        auto unsafe = root + L"\\unsafe-parent"; privateDirectory(unsafe, sid); fixtureAcl(unsafe, sid, L"(A;;0x001301BF;;;AU)");
        reportDiagnosticPhase("verify unsafe ACL rejection");
        std::unique_ptr<namespaceAclError> failure;
        try { secureNamespace guard(unsafe + L"\\not-created", sid); }
        catch (const namespaceAclError& error) { failure = std::make_unique<namespaceAclError>(error); }
        require(failure != nullptr); require(failure->rule == aclRule::unsafeAllow); require(failure->path == unsafe);
        require(failure->allowedMask == 0x001301BF); require(failure->rejectedMask == 0x112); require(failure->authenticatedUsers);
        require(GetFileAttributesW((unsafe + L"\\not-created").c_str()) == INVALID_FILE_ATTRIBUTES);
        require(evidence.failure(diagnosticStage::namespaceAudit, failure.get()));
        auto original = contents(evidencePath + L"\\failure.json"); auto wide = json::wide(original); auto fields = json::parser(wide).read();
        require(fields.size() == 11); require(json::integer(fields, L"schemaVersion") == 1); require(json::text(fields, L"launchId") == id);
        require(json::text(fields, L"policy") == L"directory_namespace");
        require(json::text(fields, L"category") == L"namespace_acl"); require(json::text(fields, L"stage") == L"namespace_audit");
        require(json::text(fields, L"path") == unsafe); require(json::text(fields, L"rule") == L"untrusted_allow_rights_rejected");
        require(json::integer(fields, L"allowedMask") == 0x001301BF); require(json::integer(fields, L"rejectedMask") == 0x112);
        require(json::text(fields, L"trustee") == L"authenticated_users");
        require(!evidence.failure(diagnosticStage::gui)); require(contents(evidencePath + L"\\failure.json") == original);
        reportDiagnosticPhase("verify evidence namespace rejection and failure redaction");
        rejects([&] { diagnosticEvidence collision(evidencePath, sid, id); });
        rejects([&] { diagnosticEvidence traversal(root + L"\\..\\escape", sid, id); });
        rejects([&] { diagnosticEvidence insecure(unsafe + L"\\evidence", sid, id); });
        rejects([&] { diagnosticEvidence invalid(root + L"\\invalid", sid, L"token"); });
        std::runtime_error secret("TOKEN=never-record\nuser file content");
        auto generic = failureJson(diagnosticStage::bundle, id, &secret); auto genericWide = json::wide(generic); auto genericFields = json::parser(genericWide).read();
        require(genericFields.size() == 5); require(generic.find("never-record") == std::string::npos); require(generic.find("user file") == std::string::npos);
        require(json::text(genericFields, L"category") == L"bootstrapper_error"); require(json::text(genericFields, L"stage") == L"bundle_validation");
        auto longFailure = failure->at(L"C:\\" + std::wstring(2045, L'\x4e00')); require(failureJson(diagnosticStage::namespaceAudit, id, &longFailure).size() <= 16384);
        auto oversized = failure->at(L"C:\\" + std::wstring(2046, L'a')); rejects([&] { failureJson(diagnosticStage::namespaceAudit, id, &oversized); });
        reportDiagnosticPhase("verify evidence immutable writes and bounds");
        require(!evidence.write(evidenceFile::launched, generic.data(), 16385));
        require(!evidence.write(static_cast<evidenceFile>(99), generic.data(), generic.size()));
        const std::string marker = "existing marker";
        require(evidence.write(evidenceFile::launched, marker.data(), marker.size()));
        require(!evidence.write(evidenceFile::launched, generic.data(), generic.size())); require(contents(evidencePath + L"\\launched.json") == marker);
        require(evidence.write(evidenceFile::selectionImage, marker.data(), marker.size()));
        require(!evidence.write(evidenceFile::selectionImage, generic.data(), generic.size())); require(contents(evidencePath + L"\\selection.bmp") == marker);
        require(!evidence.writeBarrierEvent(0, marker)); require(!evidence.writeBarrierEvent(11, marker));
        require(!evidence.writeBarrierEvent(1, std::string(16385, 'x')));
        require(evidence.writeBarrierEvent(1, marker)); require(!evidence.writeBarrierEvent(1, generic));
        require(contents(evidencePath + L"\\extraction-01.json") == marker);
        // A held destination denies writes. Recording remains best effort and never throws or overwrites.
        reportDiagnosticPhase("verify evidence blocked files and modified ACLs");
        auto blockedPath = root + L"\\blocked"; diagnosticEvidence blocked(blockedPath, sid, id);
        handle occupied(CreateFileW((blockedPath + L"\\failure.json").c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(occupied.valid());
        require(!blocked.failure(diagnosticStage::namespaceAudit, failure.get()));
        auto poisonedPath = root + L"\\changed-acl"; diagnosticEvidence poisoned(poisonedPath, sid, id);
        fixtureAcl(poisonedPath, sid, L"(A;;GR;;;AU)"); require(!poisoned.failure(diagnosticStage::gui));
        require(!poisoned.writeBarrierEvent(1, marker));
        require(GetFileAttributesW((poisonedPath + L"\\failure.json").c_str()) == INVALID_FILE_ATTRIBUTES);
        reportDiagnosticPhase("verify symlink/path safety fixtures");
        auto target = root + L"\\target", linked = root + L"\\linked"; privateDirectory(target, sid);
        check(CreateSymbolicLinkW(linked.c_str(), target.c_str(), SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE));
        rejects([&] { diagnosticEvidence reparse(linked + L"\\evidence", sid, id); }); require(std::filesystem::is_empty(target));
        auto linkedFilePath = root + L"\\linked-file"; diagnosticEvidence linkedFile(linkedFilePath, sid, id);
        auto sentinel = target + L"\\sentinel"; { std::ofstream file{std::filesystem::path(sentinel)}; file << marker; }
        check(CreateSymbolicLinkW((linkedFilePath + L"\\failure.json").c_str(), sentinel.c_str(), SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE));
        require(!linkedFile.failure(diagnosticStage::gui)); require(contents(sentinel) == marker);
        // Actual installWorker must stop at the insecure ancestor before creating staging or helper log/input.
        for (bool blockedEvidence : {false, true}) {
            reportDiagnosticPhase(std::string("verify rejected worker with ") + (blockedEvidence ? "occupied" : "fresh") + " failure evidence");
            app a; a.sid = sid; a.launchId = id; a.diagnostic = true; a.localData = root + L"\\isolated-control";
            a.payload = std::make_unique<bundle>(arguments[1]); auto path = root + (blockedEvidence ? L"\\worker-blocked" : L"\\worker-evidence");
            a.evidence = std::make_unique<diagnosticEvidence>(path, sid, id);
            if (blockedEvidence) require(a.evidence->write(evidenceFile::failure, marker.data(), marker.size()));
            auto selected = unsafe + L"\\install"; installWorker(a, selected);
            require(a.workerDone); require(a.outcome.code == 1); require(!a.input.valid()); require(a.log.empty());
            require(!std::filesystem::exists(selected)); require(!std::filesystem::exists(unsafe + L"\\.launcher-install"));
            if (blockedEvidence) require(contents(path + L"\\failure.json") == marker);
            else {
                auto result = json::wide(contents(path + L"\\failure.json")); auto actual = json::parser(result).read();
                require(json::text(actual, L"category") == L"namespace_acl"); require(json::text(actual, L"path") == unsafe);
                require(json::text(actual, L"stage") == L"namespace_audit");
            }
        }
        reportDiagnosticPhase("enter extraction barrier component cases");
        extractionBarrierChecks(root, sid, arguments[1]);
        std::cout << "Diagnostic checks: " << checks << " passed; fixtures retained\n"; return 0;
    } catch (const std::exception& error) {
        std::cerr << "Diagnostic FAIL at phase [" << currentDiagnosticPhase << "], checks=" << checks
                  << ": " << error.what() << '\n';
        return 1;
    }
}
