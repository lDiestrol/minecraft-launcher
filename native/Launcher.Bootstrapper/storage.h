#pragma once
#include "json.h"
#include <bcrypt.h>
#include <sddl.h>
#include <shlobj.h>
#include <array>
#include <atomic>
#include <filesystem>
#include <vector>
#include <memory>

inline void check(bool ok) { if (!ok) throw std::runtime_error("Win32 error " + std::to_string(GetLastError())); }
struct handle {
    HANDLE h = INVALID_HANDLE_VALUE;
    handle() = default; explicit handle(HANDLE v) : h(v) {}
    handle(const handle&) = delete; handle& operator=(const handle&) = delete;
    handle(handle&& v) noexcept : h(v.h) { v.h = INVALID_HANDLE_VALUE; }
    handle& operator=(handle&& v) noexcept { if (this != &v) { close(); h = v.h; v.h = INVALID_HANDLE_VALUE; } return *this; }
    ~handle() { close(); }
    void close() { if (h != INVALID_HANDLE_VALUE && h != nullptr) CloseHandle(h); h = INVALID_HANDLE_VALUE; }
    bool valid() const { return h != INVALID_HANDLE_VALUE && h != nullptr; }
};
struct inheritedPins {
    std::vector<handle> owned; std::vector<HANDLE> values;
    explicit inheritedPins(const std::vector<HANDLE>& source) {
        for (HANDLE original : source) {
            HANDLE copy = nullptr; check(DuplicateHandle(GetCurrentProcess(), original, GetCurrentProcess(), &copy, 0, TRUE, DUPLICATE_SAME_ACCESS));
            owned.emplace_back(copy); values.push_back(copy);
        }
    }
};
inline std::wstring known(REFKNOWNFOLDERID id) { PWSTR p = nullptr; if (FAILED(SHGetKnownFolderPath(id, 0, nullptr, &p))) throw std::runtime_error("known folder unavailable"); std::wstring r(p); CoTaskMemFree(p); return r; }
inline std::wstring executable() { wchar_t p[32768]{}; DWORD n = GetModuleFileNameW(nullptr, p, 32768); check(n && n < 32768); return p; }
inline std::wstring parentPath(const std::wstring& p) { return std::filesystem::path(p).parent_path().wstring(); }
inline std::wstring normalize(const std::wstring& p) {
    if (p.size() < 3 || p.size() > 2048 || p[1] != L':' || !iswalpha(p[0]) || (p[2] != L'\\' && p[2] != L'/')) throw std::runtime_error("absolute local path required");
    if (p.find_first_of(L"\"<>|?*\r\n") != std::wstring::npos || p.find(L':', 2) != std::wstring::npos) throw std::runtime_error("invalid local path");
    std::wstring separators = p; for (auto& c : separators) if (c == L'/') c = L'\\';
    for (const auto& part : std::filesystem::path(separators)) {
        if (part == L".." || part == L".") throw std::runtime_error("traversal rejected");
        auto name = part.wstring(); if (!name.empty() && (name.back() == L'.' || name.back() == L' ')) throw std::runtime_error("ambiguous directory name");
        name = name.substr(0, name.find(L'.')); for (auto& c : name) c = towupper(c);
        if (name == L"CON" || name == L"PRN" || name == L"AUX" || name == L"NUL" ||
            (name.size() == 4 && (name.rfind(L"COM", 0) == 0 || name.rfind(L"LPT", 0) == 0) && name[3] >= L'1' && name[3] <= L'9')) throw std::runtime_error("reserved directory name");
    }
    wchar_t full[32768]{}; DWORD n = GetFullPathNameW(separators.c_str(), 32768, full, nullptr); check(n && n < 32768);
    std::wstring r(full); while (r.size() > 3 && r.back() == L'\\') r.pop_back(); return r;
}
inline bool within(const std::wstring& path, const std::wstring& root) {
    if (_wcsicmp(path.c_str(), root.c_str()) == 0) return true;
    std::wstring prefix = root; if (prefix.back() != L'\\') prefix += L'\\';
    return path.size() >= prefix.size() && _wcsnicmp(path.c_str(), prefix.c_str(), prefix.size()) == 0;
}
inline void noOverlap(const std::wstring& a, const std::wstring& b) { if (within(a, b) || within(b, a)) throw std::runtime_error("unsafe directory overlap"); }
inline void noLinks(const std::wstring& p) {
    for (std::filesystem::path part(p); !part.empty();) {
        DWORD a = GetFileAttributesW(part.c_str());
        if (a == INVALID_FILE_ATTRIBUTES) { DWORD e = GetLastError(); if (e != ERROR_FILE_NOT_FOUND && e != ERROR_PATH_NOT_FOUND) throw std::runtime_error("cannot inspect directory"); }
        else if (a & FILE_ATTRIBUTE_REPARSE_POINT) throw std::runtime_error("reparse point rejected");
        auto next = part.parent_path(); if (next == part) break; part = next;
    }
}
inline std::wstring userSid() {
    HANDLE impersonated = nullptr;
    if (OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &impersonated)) { CloseHandle(impersonated); throw std::runtime_error("impersonated installer rejected"); }
    if (GetLastError() != ERROR_NO_TOKEN) throw std::runtime_error("cannot verify thread identity");
    HANDLE raw = nullptr; check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw)); handle token(raw);
    TOKEN_ELEVATION elevation{}; DWORD bytes = 0; check(GetTokenInformation(token.h, TokenElevation, &elevation, sizeof(elevation), &bytes));
    if (elevation.TokenIsElevated) throw std::runtime_error("Запустите установщик обычным пользователем, без прав администратора. Настройки безопасности Windows менять не требуется.");
    GetTokenInformation(token.h, TokenUser, nullptr, 0, &bytes); std::vector<BYTE> data(bytes); check(GetTokenInformation(token.h, TokenUser, data.data(), bytes, &bytes));
    LPWSTR sid = nullptr; check(ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, &sid)); std::wstring r(sid); LocalFree(sid); return r;
}
inline std::wstring guid() { GUID g{}; if (FAILED(CoCreateGuid(&g))) throw std::runtime_error("GUID unavailable"); wchar_t s[40]{}; StringFromGUID2(g, s, 40); std::wstring r; for (wchar_t c : s) if (iswxdigit(c)) r += towlower(c); return r; }
inline void makeDirectory(const std::wstring& p) {
    noLinks(p); if (GetFileAttributesW(p.c_str()) != INVALID_FILE_ATTRIBUTES) { check((GetFileAttributesW(p.c_str()) & FILE_ATTRIBUTE_DIRECTORY) != 0); return; }
    auto parent = parentPath(p); if (parent != p && !parent.empty()) makeDirectory(parent);
    check(CreateDirectoryW(p.c_str(), nullptr)); noLinks(p);
}
inline void privateDirectory(const std::wstring& p, const std::wstring& sid) {
    PSECURITY_DESCRIPTOR sd = nullptr;
    std::wstring sddl = L"O:" + sid + L"D:P(A;OICI;GA;;;SY)(A;OICI;GA;;;" + sid + L")";
    check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &sd, nullptr));
    SECURITY_ATTRIBUTES attributes{sizeof(SECURITY_ATTRIBUTES), sd, FALSE}; BOOL ok = CreateDirectoryW(p.c_str(), &attributes); DWORD error = GetLastError(); LocalFree(sd);
    if (!ok) throw std::runtime_error("cannot create unique private staging " + std::to_string(error)); noLinks(p);
}
inline handle readFile(const std::wstring& p, DWORD share = FILE_SHARE_READ) {
    noLinks(p); handle h(CreateFileW(p.c_str(), GENERIC_READ, share, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(h.valid());
    FILE_ATTRIBUTE_TAG_INFO info{}; check(GetFileInformationByHandleEx(h.h, FileAttributeTagInfo, &info, sizeof(info))); if (info.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) throw std::runtime_error("linked file rejected"); return h;
}
inline void readExact(HANDLE h, void* p, DWORD n) { DWORD got = 0; check(ReadFile(h, p, n, &got, nullptr)); if (got != n) throw std::runtime_error("truncated payload"); }
inline void seek(HANDLE h, unsigned long long offset) { LARGE_INTEGER p{}; p.QuadPart = static_cast<LONGLONG>(offset); check(SetFilePointerEx(h, p, nullptr, FILE_BEGIN)); }
inline unsigned long long length(HANDLE h) { LARGE_INTEGER n{}; check(GetFileSizeEx(h, &n)); return static_cast<unsigned long long>(n.QuadPart); }
class sha256 {
    BCRYPT_ALG_HANDLE algorithm = nullptr; BCRYPT_HASH_HANDLE hash = nullptr; std::vector<BYTE> object;
    static void nt(NTSTATUS status) { if (status < 0) throw std::runtime_error("SHA-256 unavailable"); }
public:
    sha256() {
        nt(BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0)); ULONG n = 0, got = 0;
        nt(BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&n), sizeof(n), &got, 0)); object.resize(n);
        nt(BCryptCreateHash(algorithm, &hash, object.data(), n, nullptr, 0, 0));
    }
    ~sha256() { if (hash) BCryptDestroyHash(hash); if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0); }
    void add(BYTE* p, ULONG n) { nt(BCryptHashData(hash, p, n, 0)); }
    std::array<BYTE, 32> finish() { std::array<BYTE, 32> r{}; nt(BCryptFinishHash(hash, r.data(), 32, 0)); return r; }
};
struct payloadEntry { std::wstring path; unsigned long long size, offset; std::array<BYTE, 32> hash; };
class bundle {
    handle file; std::vector<payloadEntry> entries;
public:
    unsigned long long bytes = 0;
    explicit bundle(const std::wstring& source = executable()) : file(readFile(source)) {
        unsigned long long total = length(file.h); if (total < 52) throw std::runtime_error("payload absent");
        seek(file.h, total - 52); std::array<BYTE, 52> footer{}; readExact(file.h, footer.data(), 52);
        if (memcmp(footer.data(), "MCBOOT01", 8)) throw std::runtime_error("payload absent");
        unsigned long long offset = 0; DWORD size = 0; memcpy(&offset, footer.data() + 8, 8); memcpy(&size, footer.data() + 16, 4);
        if (size < 8 || size > 1048576 || offset > total - 52 || size != total - 52 - offset) throw std::runtime_error("invalid payload index");
        seek(file.h, offset); std::vector<BYTE> data(size); readExact(file.h, data.data(), size); sha256 digest; digest.add(data.data(), size);
        auto indexHash = digest.finish(); if (memcmp(indexHash.data(), footer.data() + 20, 32)) throw std::runtime_error("corrupt payload index");
        size_t p = 0; auto number = [&](size_t n) { if (n > data.size() - p) throw std::runtime_error("truncated index"); unsigned long long v = 0; memcpy(&v, data.data() + p, n); p += n; return v; };
        if (number(4) != 1) throw std::runtime_error("unsupported payload index"); auto count = number(4); if (count < 4 || count > 1025) throw std::runtime_error("invalid payload count");
        std::map<std::wstring, bool> seen; unsigned long long end = 0;
        for (unsigned long long i = 0; i < count; ++i) {
            auto nameSize = number(4); payloadEntry entry{}; entry.size = number(8); entry.offset = number(8);
            if (nameSize == 0 || nameSize > 240 || entry.size == 0 || entry.size > 2147483648ULL || entry.offset < end || entry.offset > offset || entry.size > offset - entry.offset || p + 32 + nameSize > data.size()) throw std::runtime_error("invalid payload bounds");
            memcpy(entry.hash.data(), data.data() + p, 32); p += 32; entry.path = json::wide(std::string(reinterpret_cast<char*>(data.data() + p), static_cast<size_t>(nameSize))); p += static_cast<size_t>(nameSize);
            if (entry.path != L"bootstrapper-catalog.json" && entry.path.rfind(L"helper/", 0) != 0 && entry.path != L"vendor/Setup.exe" && entry.path != L"vendor/full.nupkg") throw std::runtime_error("unapproved payload path");
            if (entry.path.find_first_not_of(L"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/._-") != std::wstring::npos || entry.path.find(L"..") != std::wstring::npos || entry.path.back() == L'/' || entry.path.back() == L'.' || entry.path.find(L"//") != std::wstring::npos) throw std::runtime_error("invalid payload path");
            for (const auto& segment : std::filesystem::path(entry.path)) {
                auto name = segment.wstring(); if (name.empty() || name.back() == L'.' || name == L".") throw std::runtime_error("invalid payload segment");
                name = name.substr(0, name.find(L'.')); for (auto& c : name) c = towupper(c);
                if (name == L"CON" || name == L"NUL" || name == L"AUX" || name == L"PRN" ||
                    (name.size() == 4 && (name.rfind(L"COM", 0) == 0 || name.rfind(L"LPT", 0) == 0) && name[3] >= L'1' && name[3] <= L'9')) throw std::runtime_error("reserved payload name");
            }
            std::wstring caseKey = entry.path; for (auto& c : caseKey) c = towlower(c); if (!seen.emplace(caseKey, true).second) throw std::runtime_error("duplicate payload path");
            end = entry.offset + entry.size; bytes += entry.size; entries.push_back(entry);
        }
        if (p != data.size() || bytes > 8589934592ULL || !seen.count(L"bootstrapper-catalog.json") || !seen.count(L"helper/launcher.installhelper.exe") || !seen.count(L"vendor/setup.exe") || !seen.count(L"vendor/full.nupkg")) throw std::runtime_error("incomplete payload");
    }
    void extract(const std::wstring& root, const std::atomic_bool& cancel) {
        std::array<BYTE, 65536> buffer{};
        for (const auto& entry : entries) {
            if (cancel) throw std::runtime_error("extraction cancelled");
            std::wstring relative = entry.path; for (auto& c : relative) if (c == L'/') c = L'\\'; std::wstring target = root + L"\\" + relative;
            makeDirectory(parentPath(target)); noLinks(target);
            handle output(CreateFileW(target.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_FLAG_OPEN_REPARSE_POINT, nullptr)); check(output.valid());
            seek(file.h, entry.offset); sha256 digest; unsigned long long left = entry.size;
            while (left) {
                if (cancel) throw std::runtime_error("extraction cancelled");
                DWORD n = static_cast<DWORD>((std::min)(left, static_cast<unsigned long long>(buffer.size()))); readExact(file.h, buffer.data(), n); DWORD written = 0;
                check(WriteFile(output.h, buffer.data(), n, &written, nullptr)); if (written != n) throw std::runtime_error("short payload write"); digest.add(buffer.data(), n); left -= n;
            }
            check(FlushFileBuffers(output.h)); if (digest.finish() != entry.hash) throw std::runtime_error("corrupt extracted payload");
        }
    }
    std::vector<handle> pinExtracted(const std::wstring& root) const {
        // No write/delete sharing. Read-only duplicates can be handed to the helper through HANDLE_LIST.
        std::vector<handle> pinned; std::array<BYTE, 65536> buffer{};
        for (const auto& entry : entries) {
            std::wstring relative = entry.path; for (auto& c : relative) if (c == L'/') c = L'\\';
            auto fileHandle = readFile(root + L"\\" + relative); if (length(fileHandle.h) != entry.size) throw std::runtime_error("payload size changed");
            sha256 digest; unsigned long long left = entry.size;
            while (left) { DWORD n = static_cast<DWORD>((std::min)(left, static_cast<unsigned long long>(buffer.size()))); readExact(fileHandle.h, buffer.data(), n); digest.add(buffer.data(), n); left -= n; }
            if (digest.finish() != entry.hash) throw std::runtime_error("payload changed before helper launch");
            pinned.push_back(std::move(fileHandle));
        }
        return pinned;
    }
};
