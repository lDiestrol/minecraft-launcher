#pragma once
#include "storage.h"
#include <functional>

struct operationOutcome {
    int code = 1;
    void begin() { code = 1; } // An unconfirmed retry must never inherit an earlier success.
    void confirm(bool success) { code = success ? 0 : 1; }
};
inline bool readHelperLine(HANDLE output, HANDLE process, const std::atomic_bool& shutdown, std::string& line, ULONGLONG timeout, const std::function<void()>& poll = {}) {
    line.clear(); char c = 0; DWORD got = 0; ULONGLONG started = GetTickCount64();
    for (;;) {
        if (shutdown) return false;
        if (poll) poll(); // Only the owning worker may serialize a pending command into the pipe.
        if (GetTickCount64() - started > timeout) throw std::runtime_error("helper response deadline exceeded; no cleanup authorized");
        DWORD available = 0;
        if (!PeekNamedPipe(output, nullptr, 0, nullptr, &available, nullptr)) { if (GetLastError() == ERROR_BROKEN_PIPE) break; check(false); }
        if (!available) {
            if (WaitForSingleObject(process, 0) == WAIT_OBJECT_0) break;
            Sleep(10); continue;
        }
        check(ReadFile(output, &c, 1, &got, nullptr)); if (!got) break;
        if (c == '\n') return true; if (line.size() >= 32768) throw std::runtime_error("oversized helper response"); line += c;
    }
    if (!line.empty()) throw std::runtime_error("truncated helper response"); return false;
}
