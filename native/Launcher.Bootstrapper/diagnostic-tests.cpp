// Exercise the actual worker and evidence boundary; no installer/helper is executed.
#define wWinMain unusedProductEntry
#include "main.cpp"
#undef wWinMain
#include <iostream>

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
    auto name = path; check(SetNamedSecurityInfoW(name.data(), SE_FILE_OBJECT, DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, nullptr, nullptr, acl, nullptr) == ERROR_SUCCESS);
}
std::string contents(const std::wstring& path) {
    auto file = readFile(path); auto size = length(file.h); require(size <= 16384); std::string data(static_cast<size_t>(size), '\0');
    readExact(file.h, data.data(), static_cast<DWORD>(size)); return data;
}
int wmain(int count, wchar_t** arguments) {
    try {
        if (count != 3) throw std::runtime_error("expected data bundle and fresh test folder");
        auto sid = fixtureSid(), root = normalize(arguments[2]), id = std::wstring(32, L'a');
        secureNamespace testRoot(parentPath(root), sid, false); testRoot.child(root, true);
        auto evidencePath = root + L"\\evidence"; diagnosticEvidence evidence(evidencePath, sid, id);
        auto unsafe = root + L"\\unsafe-parent"; privateDirectory(unsafe, sid); fixtureAcl(unsafe, sid, L"(A;;0x001301BF;;;AU)");
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
        require(!evidence.write(evidenceFile::launched, generic.data(), 16385));
        require(!evidence.write(static_cast<evidenceFile>(99), generic.data(), generic.size()));
        const std::string marker = "existing marker";
        require(evidence.write(evidenceFile::launched, marker.data(), marker.size()));
        require(!evidence.write(evidenceFile::launched, generic.data(), generic.size())); require(contents(evidencePath + L"\\launched.json") == marker);
        require(evidence.write(evidenceFile::selectionImage, marker.data(), marker.size()));
        require(!evidence.write(evidenceFile::selectionImage, generic.data(), generic.size())); require(contents(evidencePath + L"\\selection.bmp") == marker);
        // A held destination denies writes. Recording remains best effort and never throws or overwrites.
        auto blockedPath = root + L"\\blocked"; diagnosticEvidence blocked(blockedPath, sid, id);
        handle occupied(CreateFileW((blockedPath + L"\\failure.json").c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(occupied.valid());
        require(!blocked.failure(diagnosticStage::namespaceAudit, failure.get()));
        auto poisonedPath = root + L"\\changed-acl"; diagnosticEvidence poisoned(poisonedPath, sid, id);
        fixtureAcl(poisonedPath, sid, L"(A;;GR;;;AU)"); require(!poisoned.failure(diagnosticStage::gui));
        require(GetFileAttributesW((poisonedPath + L"\\failure.json").c_str()) == INVALID_FILE_ATTRIBUTES);
        auto target = root + L"\\target", linked = root + L"\\linked"; privateDirectory(target, sid);
        check(CreateSymbolicLinkW(linked.c_str(), target.c_str(), SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE));
        rejects([&] { diagnosticEvidence reparse(linked + L"\\evidence", sid, id); }); require(std::filesystem::is_empty(target));
        auto linkedFilePath = root + L"\\linked-file"; diagnosticEvidence linkedFile(linkedFilePath, sid, id);
        auto sentinel = target + L"\\sentinel"; { std::ofstream file{std::filesystem::path(sentinel)}; file << marker; }
        check(CreateSymbolicLinkW((linkedFilePath + L"\\failure.json").c_str(), sentinel.c_str(), SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE));
        require(!linkedFile.failure(diagnosticStage::gui)); require(contents(sentinel) == marker);
        // Actual installWorker must stop at the insecure ancestor before creating staging or helper log/input.
        for (bool blockedEvidence : {false, true}) {
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
        std::cout << "Diagnostic checks: " << checks << " passed; fixtures retained\n"; return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << '\n'; return 1; }
}
