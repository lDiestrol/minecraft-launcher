#pragma once
#include <windows.h>
#include <map>
#include <stdexcept>
#include <string>
#include <cwctype>

namespace json {
inline std::wstring wide(const std::string& s) {
    if (s.empty()) return {};
    int n = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()), nullptr, 0);
    if (!n) throw std::runtime_error("invalid UTF-8");
    std::wstring r(n, L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()), r.data(), n);
    return r;
}
inline std::string utf8(const std::wstring& s) {
    if (s.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()), nullptr, 0, nullptr, nullptr);
    if (!n) throw std::runtime_error("invalid UTF-16");
    std::string r(n, '\0');
    WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, s.data(), static_cast<int>(s.size()), r.data(), n, nullptr, nullptr);
    return r;
}
inline std::wstring quote(const std::wstring& s) {
    std::wstring r = L"\"";
    for (wchar_t c : s) {
        if (c == L'"' || c == L'\\') { r += L'\\'; r += c; }
        else if (c < 32) {
            wchar_t hex[7]{}; swprintf_s(hex, L"\\u%04x", static_cast<unsigned>(c)); r += hex;
        } else r += c;
    }
    return r + L'"';
}
struct value { std::wstring text; bool string = false; };
using object = std::map<std::wstring, value>;
class parser {
    const std::wstring& s; size_t p = 0;
    [[noreturn]] void bad() const { throw std::runtime_error("malformed flat JSON"); }
    void space() { while (p < s.size() && (s[p] == L' ' || s[p] == L'\r' || s[p] == L'\n' || s[p] == L'\t')) ++p; }
    wchar_t take() { if (p >= s.size()) bad(); return s[p++]; }
    unsigned hex() {
        unsigned n = 0;
        for (int i = 0; i < 4; ++i) {
            wchar_t c = take();
            unsigned d = c >= L'0' && c <= L'9' ? c - L'0' : c >= L'a' && c <= L'f' ? c - L'a' + 10 : c >= L'A' && c <= L'F' ? c - L'A' + 10 : 16;
            if (d == 16) bad(); n = n * 16 + d;
        } return n;
    }
    std::wstring string() {
        if (take() != L'"') bad(); std::wstring r;
        for (;;) {
            wchar_t c = take(); if (c == L'"') { utf8(r); return r; } if (c < 32) bad();
            if (c != L'\\') { r += c; continue; }
            switch (c = take()) {
            case L'"': case L'\\': case L'/': r += c; break;
            case L'b': r += L'\b'; break; case L'f': r += L'\f'; break;
            case L'n': r += L'\n'; break; case L'r': r += L'\r'; break; case L't': r += L'\t'; break;
            case L'u': r += static_cast<wchar_t>(hex()); break;
            default: bad();
            }
        }
    }
public:
    explicit parser(const std::wstring& text) : s(text) {}
    object read() {
        if (s.size() > 32768) bad(); object r; space(); if (take() != L'{') bad(); space();
        if (p < s.size() && s[p] == L'}') { ++p; space(); if (p != s.size()) bad(); return r; }
        for (;;) {
            space(); std::wstring key = string(); space(); if (take() != L':') bad(); space();
            value v;
            if (p < s.size() && s[p] == L'"') { v.string = true; v.text = string(); }
            else {
                size_t start = p; while (p < s.size() && (iswalnum(s[p]) || s[p] == L'-')) ++p;
                v.text = s.substr(start, p - start);
                if (v.text != L"true" && v.text != L"false" && v.text != L"null") {
                    if (v.text.empty() || v.text.find_first_not_of(L"0123456789") != std::wstring::npos ||
                        (v.text.size() > 1 && v.text[0] == L'0')) bad();
                }
            }
            if (!r.emplace(key, v).second || r.size() > 32) bad(); space(); wchar_t end = take();
            if (end == L'}') break; if (end != L',') bad();
        }
        space(); if (p != s.size()) bad(); return r;
    }
};
inline std::wstring text(const object& o, const wchar_t* k) {
    const auto& v = o.at(k); if (!v.string) throw std::runtime_error("JSON string expected"); return v.text;
}
inline unsigned long long integer(const object& o, const wchar_t* k) {
    const auto& v = o.at(k); if (v.string || v.text.find_first_not_of(L"0123456789") != std::wstring::npos) throw std::runtime_error("JSON integer expected");
    return std::stoull(v.text);
}
inline bool boolean(const object& o, const wchar_t* k) {
    const auto& v = o.at(k); if (v.string || (v.text != L"true" && v.text != L"false")) throw std::runtime_error("JSON boolean expected"); return v.text == L"true";
}
}
