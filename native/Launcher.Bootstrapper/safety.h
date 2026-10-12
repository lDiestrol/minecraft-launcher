#pragma once
#include "storage.h"
#include <aclapi.h>

inline void requirePersistentAclFlags(DWORD flags) {
    if (!(flags & FILE_PERSISTENT_ACLS)) throw std::runtime_error("Выбранный том не поддерживает постоянные ACL. Нужен защищённый локальный том; другой диск не выбран.");
}
inline void requirePersistentAcls(const std::wstring& path) {
    DWORD flags = 0; check(GetVolumeInformationW(path.substr(0, 3).c_str(), nullptr, 0, nullptr, nullptr, &flags, nullptr, 0));
    requirePersistentAclFlags(flags);
}
struct localMemory {
    HLOCAL p = nullptr;
    ~localMemory() { if (p) LocalFree(p); }
};
inline bool sidEquals(PSID sid, const wchar_t* text) {
    localMemory expected; check(ConvertStringSidToSidW(text, reinterpret_cast<PSID*>(&expected.p)) != FALSE);
    return sid && EqualSid(sid, expected.p) != FALSE;
}
inline bool trustedNamespaceSid(PSID sid, const std::wstring& user) {
    // Administrators/SYSTEM/TrustedInstaller are Windows security authorities, not unprivileged peers.
    return sidEquals(sid, user.c_str()) || IsWellKnownSid(sid, WinLocalSystemSid) || IsWellKnownSid(sid, WinBuiltinAdministratorsSid) ||
        sidEquals(sid, L"S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");
}
inline void verifyDirectoryDescriptor(PSECURITY_DESCRIPTOR descriptor, const std::wstring& user, bool privatePayload) {
    PSID owner = nullptr; BOOL defaulted = FALSE, present = FALSE; PACL acl = nullptr;
    check(GetSecurityDescriptorOwner(descriptor, &owner, &defaulted) != FALSE);
    if (privatePayload ? !sidEquals(owner, user.c_str()) : !trustedNamespaceSid(owner, user)) throw std::runtime_error("untrusted directory owner");
    check(GetSecurityDescriptorDacl(descriptor, &present, &acl, &defaulted) != FALSE);
    if (!present || !acl) throw std::runtime_error("unprotected directory DACL");
    SECURITY_DESCRIPTOR_CONTROL control{}; DWORD revision = 0; check(GetSecurityDescriptorControl(descriptor, &control, &revision) != FALSE);
    if (privatePayload && !(control & SE_DACL_PROTECTED)) throw std::runtime_error("payload DACL inheritance is not protected");
    bool userAllowed = false, systemAllowed = false;
    for (DWORD i = 0; i < acl->AceCount; ++i) {
        void* raw = nullptr; check(GetAce(acl, i, &raw) != FALSE); auto header = static_cast<ACE_HEADER*>(raw);
        if (header->AceFlags & INHERIT_ONLY_ACE) continue;
        if (header->AceType == ACCESS_DENIED_ACE_TYPE) continue; // Conservative allow-ACE audit; denies never authorize a path.
        if (header->AceType != ACCESS_ALLOWED_ACE_TYPE) throw std::runtime_error("unsupported directory ACE");
        auto ace = static_cast<ACCESS_ALLOWED_ACE*>(raw); PSID trustee = &ace->SidStart; DWORD mask = ace->Mask;
        GENERIC_MAPPING mapping{FILE_GENERIC_READ, FILE_GENERIC_WRITE, FILE_GENERIC_EXECUTE, FILE_ALL_ACCESS}; MapGenericMask(&mask, &mapping);
        bool current = sidEquals(trustee, user.c_str()), system = IsWellKnownSid(trustee, WinLocalSystemSid) != FALSE;
        // Creating an unrelated sibling is harmless. Rewriting attributes/security, replacing children or changing reparse data is not.
        constexpr DWORD unsafe = WRITE_DAC | WRITE_OWNER | FILE_DELETE_CHILD | FILE_WRITE_DATA | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES;
        if (privatePayload ? (!current && !system && mask != 0) : (!trustedNamespaceSid(trustee, user) && (mask & unsafe)))
            throw std::runtime_error("unsafe directory namespace ACL");
        userAllowed |= current && (mask & FILE_ALL_ACCESS) == FILE_ALL_ACCESS;
        systemAllowed |= system && (mask & FILE_ALL_ACCESS) == FILE_ALL_ACCESS;
    }
    if (privatePayload && (!userAllowed || !systemAllowed)) throw std::runtime_error("incomplete private directory ACL");
}
inline void verifyDirectoryHandle(HANDLE file, const std::wstring& path, const std::wstring& user, bool privatePayload) {
    FILE_ATTRIBUTE_TAG_INFO attributes{}; check(GetFileInformationByHandleEx(file, FileAttributeTagInfo, &attributes, sizeof(attributes)) != FALSE);
    if (!(attributes.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) || (attributes.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT)) throw std::runtime_error("linked directory rejected");
    wchar_t finalName[32768]{}; DWORD count = GetFinalPathNameByHandleW(file, finalName, 32768, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
    check(count && count < 32768); std::wstring final(finalName);
    if (final.rfind(L"\\\\?\\", 0) != 0 || _wcsicmp(normalize(final.substr(4)).c_str(), path.c_str())) throw std::runtime_error("directory identity/path mismatch");
    localMemory descriptor;
    DWORD error = GetSecurityInfo(file, SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION, nullptr, nullptr, nullptr, nullptr,
        reinterpret_cast<PSECURITY_DESCRIPTOR*>(&descriptor.p));
    if (error != ERROR_SUCCESS) throw std::runtime_error("cannot verify directory security " + std::to_string(error));
    try { verifyDirectoryDescriptor(descriptor.p, user, privatePayload); }
    catch (const std::exception& error) { throw std::runtime_error(std::string(error.what()) + ": " + json::utf8(path)); }
}
class secureNamespace {
    struct directory { std::wstring path; handle pinned; bool privatePayload; };
    std::vector<directory> directories; std::wstring sid;
    void add(const std::wstring& path, bool create, bool privatePayload) {
        DWORD attributes = GetFileAttributesW(path.c_str());
        if (privatePayload && attributes != INVALID_FILE_ATTRIBUTES) throw std::runtime_error("private staging name collision");
        if (attributes == INVALID_FILE_ATTRIBUTES) {
            DWORD error = GetLastError();
            if (!create || (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)) throw std::runtime_error("directory unavailable");
            privateDirectory(path, sid); // Parent is already pinned and audited; never adopt an existing collided name.
        }
        // Metadata-only handles do not participate in Windows delete-sharing checks.
        handle file(CreateFileW(path.c_str(), READ_CONTROL | FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(file.valid());
        verifyDirectoryHandle(file.h, path, sid, privatePayload);
        directories.push_back({path, std::move(file), privatePayload});
    }
public:
    secureNamespace(const std::wstring& base, const std::wstring& user, bool create = true) : sid(user) {
        std::wstring normalized = normalize(base); requirePersistentAcls(normalized); add(normalized.substr(0, 3), false, false);
        size_t start = 3;
        while (start < normalized.size()) {
            size_t end = normalized.find(L'\\', start); if (end == std::wstring::npos) end = normalized.size();
            add(normalized.substr(0, end), create, false); start = end + 1;
        }
    }
    void child(const std::wstring& path, bool privatePayload = false) {
        if (directories.empty() || _wcsicmp(parentPath(path).c_str(), directories.back().path.c_str())) throw std::runtime_error("unguarded directory parent");
        add(path, true, privatePayload);
    }
    void recheck() const {
        for (const auto& directory : directories) verifyDirectoryHandle(directory.pinned.h, directory.path, sid, directory.privatePayload);
    }
    std::vector<HANDLE> handles() const {
        std::vector<HANDLE> result; for (const auto& directory : directories) result.push_back(directory.pinned.h); return result;
    }
};
