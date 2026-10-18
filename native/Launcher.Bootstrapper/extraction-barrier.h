#pragma once
#ifdef INSTALLER_DIAGNOSTIC
#include "diagnostics.h"
#include "extraction-barrier-state.h"
#include <mutex>

// This mode is extraction-only: no continuation, timeout or failure can authorize a helper.
class extractionBarrier {
    diagnosticEvidence& evidence;
    std::wstring runId, nonce = guid(), continueName, userSid;
    handle cancelEvent, continueEvent;
    extractionDiagnostic::trace journal;
    extractionDiagnostic::continueGate control;
    std::mutex cancelLock;
    extractionDiagnostic::stamp buttonTime{}, installTime{};
    bool buttonSeen = false, observed = false, terminal = false;
    unsigned long long bytes = 0, total = 0, processStart = 0;
    static extractionDiagnostic::stamp now() {
        FILETIME ft{}; GetSystemTimeAsFileTime(&ft); ULARGE_INTEGER ticks{}; ticks.LowPart = ft.dwLowDateTime; ticks.HighPart = ft.dwHighDateTime;
        return {ticks.QuadPart, GetTickCount64()};
    }
    static const wchar_t* name(extractionDiagnostic::step stage) {
        using extractionDiagnostic::step;
        switch (stage) {
        case step::installRequested: return L"install_requested";
        case step::extractionStarted: return L"extraction_started";
        case step::payloadWritten: return L"payload_bytes_written";
        case step::barrierReached: return L"barrier_reached";
        case step::guiCancel: return L"gui_cancel";
        case step::cancellationObserved: return L"worker_observes_cancellation";
        case step::extractionAborted: return L"extraction_aborted";
        case step::helperNotStarted: return L"helper_not_started";
        case step::continued: return L"barrier_continue";
        case step::extractionCompleted: return L"extraction_completed";
        default: return L"barrier_failed";
        }
    }
    void record(extractionDiagnostic::step stage, extractionDiagnostic::stamp time) {
        journal.append(stage, time, bytes, total);
        auto sequence = journal.events().size();
        std::wstring body = L"{\"schemaVersion\":1,\"runId\":" + json::quote(runId) + L",\"sessionNonce\":" + json::quote(nonce) +
            L",\"sequence\":" + std::to_wstring(sequence) + L",\"event\":" + json::quote(name(stage)) +
            L",\"utcFileTimeTicks\":" + std::to_wstring(time.utc) + L",\"monotonicMs\":" + std::to_wstring(time.monotonic) +
            L",\"pid\":" + std::to_wstring(GetCurrentProcessId()) + L",\"processStartFileTimeTicks\":" + std::to_wstring(processStart) +
            L",\"bytesWritten\":" + std::to_wstring(bytes) + L",\"totalBytes\":" + std::to_wstring(total);
        if (stage == extractionDiagnostic::step::barrierReached)
            body += L",\"continueEvent\":" + json::quote(continueName) + L",\"timeoutMs\":30000";
        auto data = json::utf8(body + L"}\n");
#ifdef EXTRACTION_BARRIER_COMPONENT_TESTS
        if (stage == extractionDiagnostic::step::barrierReached && beforePublicationForTest) beforePublicationForTest(continueName);
#endif
        // Fixed immutable filenames; the existing writer audits and pins the private evidence namespace.
        if (!evidence.writeBarrierEvent(sequence, data)) throw std::runtime_error("extraction journal write failed");
    }
    void createContinueEvent() {
        localMemory sd; auto sddl = L"D:P(A;;GA;;;SY)(A;;GA;;;" + userSid + L")";
        check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, reinterpret_cast<PSECURITY_DESCRIPTOR*>(&sd.p), nullptr));
        SECURITY_ATTRIBUTES security{sizeof(SECURITY_ATTRIBUTES), sd.p, FALSE};
        SetLastError(ERROR_SUCCESS); continueEvent = handle(CreateEventW(&security, TRUE, FALSE, continueName.c_str()));
        auto error = GetLastError(); check(continueEvent.valid());
        if (error == ERROR_ALREADY_EXISTS) throw std::runtime_error("existing extraction control event rejected");
    }
public:
#ifdef EXTRACTION_BARRIER_COMPONENT_TESTS
    std::function<void(const std::wstring&)> beforePublicationForTest;
#endif
    extractionBarrier(diagnosticEvidence& writer, const std::wstring& sid, const std::wstring& id) : evidence(writer), runId(id), userSid(sid) {
        if (!validLaunchId(id)) throw std::runtime_error("invalid extraction run id");
        FILETIME created{}, exited{}, kernel{}, user{};
        check(GetProcessTimes(GetCurrentProcess(), &created, &exited, &kernel, &user));
        ULARGE_INTEGER ticks{}; ticks.LowPart = created.dwLowDateTime; ticks.HighPart = created.dwHighDateTime; processStart = ticks.QuadPart;
        cancelEvent = handle(CreateEventW(nullptr, TRUE, FALSE, nullptr)); check(cancelEvent.valid());
        continueName = L"Local\\MinecraftLauncher.Extraction." + runId + L"." + nonce;
    }
    // Captures the Cancel control's BN_CLICKED handler route, not proof of a physical human click.
    void cancelButton() {
        std::lock_guard<std::mutex> lock(cancelLock);
        if (!buttonSeen) { buttonTime = now(); buttonSeen = true; }
    }
    // WM_CLOSE calls this only after publishing app.stop. No file I/O on the UI thread.
    void notifyStop() noexcept { SetEvent(cancelEvent.h); }
    void installButton() {
        if (installTime.utc) throw std::runtime_error("extraction barrier cannot be reused");
        installTime = now();
    }
    void installRequested(unsigned long long size) { total = size; record(extractionDiagnostic::step::installRequested, installTime); }
    void extractionStarted() { record(extractionDiagnostic::step::extractionStarted, now()); }
    void partialWrite(unsigned long long count, const std::atomic_bool& stop) {
        using extractionDiagnostic::step;
        bytes = count; record(step::payloadWritten, now());
        if (stop) throw std::runtime_error("early extraction cancellation rejected");
        auto reached = now();
        control.publishThenCreate([&] { record(step::barrierReached, reached); }, [&] { createContinueEvent(); });
        // The name can be known earlier, but our endpoint does not exist until publication completes.
        // An early external CreateEvent (signaled or not) fails the exclusive creation check above.
        auto deadline = reached.monotonic + 30000, current = GetTickCount64();
        if (current >= deadline) throw std::runtime_error("extraction barrier publication deadline exceeded");
        HANDLE events[]{cancelEvent.h, continueEvent.h};
        DWORD result = WaitForMultipleObjects(2, events, FALSE, static_cast<DWORD>(deadline - current));
        if ((result != WAIT_OBJECT_0 && result != WAIT_OBJECT_0 + 1) || GetTickCount64() > deadline)
            throw std::runtime_error("extraction barrier timeout or controller failure");
        // Cancel observed at this decision wins over Continue. A later Cancel is caught by extract's stop checks.
        auto selected = control.resolve(stop.load(), result == WAIT_OBJECT_0, result == WAIT_OBJECT_0 + 1);
        if (selected == extractionDiagnostic::decision::reject) throw std::runtime_error("invalid extraction control signal");
        if (selected == extractionDiagnostic::decision::cancel) {
            extractionDiagnostic::stamp captured{};
            { std::lock_guard<std::mutex> lock(cancelLock); if (!buttonSeen) throw std::runtime_error("non-button extraction cancellation"); captured = buttonTime; }
            if (captured.monotonic > deadline) throw std::runtime_error("late extraction cancellation rejected");
            record(step::guiCancel, captured); record(step::cancellationObserved, now()); observed = true;
            throw std::runtime_error("diagnostic extraction cancelled");
        }
        record(step::continued, now());
    }
    void extractionFinished() {
        bytes = total; record(extractionDiagnostic::step::extractionCompleted, now());
        record(extractionDiagnostic::step::helperNotStarted, now()); terminal = true;
        throw std::runtime_error("diagnostic extraction-only completed; helper forbidden");
    }
    void failed() noexcept {
        if (terminal || !total) return;
        try {
            if (!observed) record(extractionDiagnostic::step::failed, now());
            record(extractionDiagnostic::step::extractionAborted, now());
            record(extractionDiagnostic::step::helperNotStarted, now()); terminal = true;
        } catch (...) { } // Incomplete journal is FAIL; never authorizes helper or success.
    }
};
#endif
