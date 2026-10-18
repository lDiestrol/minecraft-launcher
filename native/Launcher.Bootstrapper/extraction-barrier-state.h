#pragma once
#ifdef INSTALLER_DIAGNOSTIC
#include <cstdint>
#include <stdexcept>
#include <vector>

// Portable policy shared by the native adapter and Linux unit tests. No OS side effects.
namespace extractionDiagnostic {
// The native Continue endpoint may only be created after durable publication succeeds.
// A publication/creation failure poisons this one-shot gate; it can never be retried/adopted.
enum class decision { reject, cancel, resume };
class continueGate {
    enum class phase { initial, publishing, published, armed, failed };
    phase state = phase::initial;
public:
    template<class Publish, class Create> void publishThenCreate(Publish publish, Create create) {
        if (state != phase::initial) throw std::runtime_error("extraction control gate cannot be reused");
        state = phase::publishing;
        try {
            publish(); state = phase::published;
            create(); state = phase::armed;
        } catch (...) { state = phase::failed; throw; }
    }
    decision resolve(bool stopped, bool cancelSignaled, bool continueSignaled) const {
        if (state != phase::armed) return decision::reject;
        if (stopped || cancelSignaled) return stopped ? decision::cancel : decision::reject;
        return continueSignaled ? decision::resume : decision::reject;
    }
};
enum class step { installRequested, extractionStarted, payloadWritten, barrierReached,
    guiCancel, cancellationObserved, extractionAborted, helperNotStarted,
    continued, extractionCompleted, failed };
struct stamp { std::uint64_t utc, monotonic; };
struct entry { step stage; stamp time; std::uint64_t bytes, total; };
class trace {
    std::vector<entry> entries;
public:
    const std::vector<entry>& events() const { return entries; }
    void append(step next, stamp time, std::uint64_t bytes, std::uint64_t total) {
        if (!time.utc || !total || (bytes >= total && next != step::extractionCompleted && next != step::helperNotStarted))
            throw std::runtime_error("invalid extraction diagnostic bounds");
        bool allowed = entries.empty() ? next == step::installRequested : false;
        if (!entries.empty()) {
            auto previous = entries.back().stage;
            if (time.monotonic < entries.back().time.monotonic || total != entries.back().total || bytes < entries.back().bytes || bytes > total ||
                (next == step::guiCancel && time.monotonic - entries.back().time.monotonic > 30000))
                throw std::runtime_error("invalid extraction diagnostic order");
            allowed = (next == step::extractionStarted && previous == step::installRequested) ||
                (next == step::payloadWritten && previous == step::extractionStarted && bytes > 0) ||
                (next == step::barrierReached && previous == step::payloadWritten) ||
                (next == step::guiCancel && previous == step::barrierReached) ||
                (next == step::cancellationObserved && previous == step::guiCancel) ||
                (next == step::extractionAborted && (previous == step::cancellationObserved || previous == step::failed)) ||
                (next == step::continued && previous == step::barrierReached) ||
                (next == step::extractionCompleted && previous == step::continued && bytes == total) ||
                (next == step::helperNotStarted && (previous == step::extractionAborted || previous == step::extractionCompleted)) ||
                (next == step::failed && previous != step::failed && previous != step::helperNotStarted && previous != step::extractionAborted && previous != step::extractionCompleted);
        }
        if (!allowed || entries.size() >= 10) throw std::runtime_error("invalid extraction diagnostic transition");
        entries.push_back({next, time, bytes, total});
    }
};
}
#endif
