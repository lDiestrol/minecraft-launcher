#pragma once
#include "safety.h"

enum class diagnosticStage { startup, evidence, discovery, bundle, window, pathValidation, space, namespaceAudit, staging, extraction, payloadPin, helperStart, helperProtocol, helperFailure, gui };
enum class evidenceFile { failure, launched, selectionImage, inspectionImage, preflightImage, successImage, failureImage, launchedImage };

#ifdef INSTALLER_DIAGNOSTIC
inline const wchar_t* stageName(diagnosticStage stage) {
    switch (stage) {
    case diagnosticStage::startup: return L"startup";
    case diagnosticStage::evidence: return L"evidence";
    case diagnosticStage::discovery: return L"storage_discovery";
    case diagnosticStage::bundle: return L"bundle_validation";
    case diagnosticStage::window: return L"window_creation";
    case diagnosticStage::pathValidation: return L"path_validation";
    case diagnosticStage::space: return L"disk_space";
    case diagnosticStage::namespaceAudit: return L"namespace_audit";
    case diagnosticStage::staging: return L"staging_creation";
    case diagnosticStage::extraction: return L"payload_extraction";
    case diagnosticStage::payloadPin: return L"payload_pin";
    case diagnosticStage::helperStart: return L"helper_start";
    case diagnosticStage::helperProtocol: return L"helper_protocol";
    case diagnosticStage::helperFailure: return L"helper_failure";
    default: return L"gui";
    }
}
inline const wchar_t* ruleName(aclRule rule) {
    switch (rule) {
    case aclRule::owner: return L"trusted_owner_required";
    case aclRule::missingDacl: return L"non_null_dacl_required";
    case aclRule::inheritance: return L"protected_private_dacl_required";
    case aclRule::unsupportedAce: return L"supported_ace_required";
    case aclRule::unsafeAllow: return L"untrusted_allow_rights_rejected";
    default: return L"private_user_system_full_control_required";
    }
}
inline bool validLaunchId(const std::wstring& id) { return id.size() == 32 && id.find_first_not_of(L"0123456789abcdef") == std::wstring::npos; }
inline std::string failureJson(diagnosticStage stage, const std::wstring& launchId, const std::exception* error = nullptr) {
    if (!validLaunchId(launchId)) throw std::runtime_error("invalid diagnostic launch id");
    auto acl = dynamic_cast<const namespaceAclError*>(error);
    std::wstring body = L"{\"schemaVersion\":1,\"launchId\":" + json::quote(launchId) + L",\"stage\":" + json::quote(stageName(stage)) +
        L",\"category\":" + json::quote(acl ? L"namespace_acl" : L"bootstrapper_error") +
        L",\"detail\":" + json::quote(acl ? L"Directory security policy rejected the namespace." : L"Bootstrapper operation failed; no arbitrary exception text is recorded.");
    if (acl) {
        // Only the normalized directory actually audited, never what(), helper messages, locator/file contents or environment.
        auto path = acl->path.empty() ? std::wstring() : normalize(acl->path);
        body += L",\"path\":" + json::quote(path) + L",\"policy\":" + json::quote(acl->privatePayload ? L"private_payload" : L"directory_namespace") + L",\"rule\":" + json::quote(ruleName(acl->rule)) +
            L",\"allowedMask\":" + std::to_wstring(acl->allowedMask) + L",\"rejectedMask\":" + std::to_wstring(acl->rejectedMask) +
            L",\"trustee\":" + json::quote(acl->authenticatedUsers ? L"authenticated_users" : L"other_or_not_applicable");
    }
    auto result = json::utf8(body + L"}\n"); if (result.size() > 16384) throw std::runtime_error("failure evidence too large"); return result;
}
inline const wchar_t* evidenceName(evidenceFile file) {
    switch (file) {
    case evidenceFile::failure: return L"failure.json";
    case evidenceFile::launched: return L"launched.json";
    case evidenceFile::selectionImage: return L"selection.bmp";
    case evidenceFile::inspectionImage: return L"inspection.bmp";
    case evidenceFile::preflightImage: return L"preflight.bmp";
    case evidenceFile::successImage: return L"success.bmp";
    case evidenceFile::failureImage: return L"failure.bmp";
    case evidenceFile::launchedImage: return L"launched.bmp";
    default: throw std::runtime_error("unknown evidence file");
    }
}
class diagnosticEvidence {
    std::wstring root, launchId;
    secureNamespace guard;
public:
    diagnosticEvidence(const std::wstring& path, const std::wstring& sid, const std::wstring& id)
        : root(normalize(path)), launchId(id), guard(parentPath(root), sid, false) {
        if (!validLaunchId(id)) throw std::runtime_error("invalid diagnostic launch id");
        guard.child(root, true); guard.recheck(); // Fresh protected private leaf; collisions are never adopted.
    }
    bool write(evidenceFile file, const void* bytes, size_t size) noexcept {
        try {
            size_t limit = file == evidenceFile::failure || file == evidenceFile::launched ? 16384 : 32 * 1024 * 1024;
            if (size == 0 || size > limit) return false;
            guard.recheck(); auto path = root + L"\\" + evidenceName(file);
            handle output(CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(output.valid());
            DWORD written = 0; check(WriteFile(output.h, bytes, static_cast<DWORD>(size), &written, nullptr)); check(written == size); check(FlushFileBuffers(output.h));
            return true;
        } catch (...) { return false; } // Evidence never changes the installation decision, retries or security checks.
    }
    bool writeBarrierEvent(size_t sequence, const std::string& data) noexcept {
        static const wchar_t* names[]{L"extraction-01.json", L"extraction-02.json", L"extraction-03.json", L"extraction-04.json",
            L"extraction-05.json", L"extraction-06.json", L"extraction-07.json", L"extraction-08.json", L"extraction-09.json", L"extraction-10.json"};
        try {
            if (sequence < 1 || sequence > 10 || data.empty() || data.size() > 16384) return false;
            guard.recheck(); auto path = root + L"\\" + names[sequence - 1];
            handle output(CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(output.valid());
            DWORD written = 0; check(WriteFile(output.h, data.data(), static_cast<DWORD>(data.size()), &written, nullptr));
            check(written == data.size()); check(FlushFileBuffers(output.h)); return true;
        } catch (...) { return false; }
    }
    bool failure(diagnosticStage stage, const std::exception* error = nullptr) noexcept {
        try { auto data = failureJson(stage, launchId, error); return write(evidenceFile::failure, data.data(), data.size()); }
        catch (...) { return false; }
    }
};
#endif
