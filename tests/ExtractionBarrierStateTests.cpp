// Linux portable unit test: no Win32, GUI, filesystem, payload or executable packaging.
#define INSTALLER_DIAGNOSTIC
#include "../native/Launcher.Bootstrapper/extraction-barrier-state.h"
#include <iostream>
#include <functional>
using namespace extractionDiagnostic;
int checks = 0;
void require(bool ok) { if (!ok) throw std::runtime_error("barrier policy assertion"); ++checks; }
void rejects(const std::function<void()>& action) {
    bool rejected = false; try { action(); } catch (const std::runtime_error&) { rejected = true; } require(rejected);
}
trace prefix() {
    trace state; state.append(step::installRequested, {1, 1}, 0, 100);
    state.append(step::extractionStarted, {2, 2}, 0, 100);
    state.append(step::payloadWritten, {3, 3}, 32, 100);
    state.append(step::barrierReached, {4, 4}, 32, 100); return state;
}
int main() {
    continueGate gate;
    require(gate.resolve(false, false, true) == decision::reject); // Before publication: no Continue endpoint.
    bool endpoint = false, published = false;
    gate.publishThenCreate([&] {
        require(!endpoint);
        require(gate.resolve(false, false, true) == decision::reject); // Signal in the old check->publish race window.
        published = true;
    }, [&] {
        require(published && !endpoint);
        require(gate.resolve(false, false, true) == decision::reject); // Not armed until exclusive create succeeds.
        endpoint = true;
    });
    require(endpoint);
    for (bool stopped : {false, true}) for (bool cancelSignal : {false, true}) for (bool continueSignal : {false, true}) {
        auto expected = stopped ? decision::cancel : cancelSignal ? decision::reject : continueSignal ? decision::resume : decision::reject;
        require(gate.resolve(stopped, cancelSignal, continueSignal) == expected);
    }
    rejects([&] { gate.publishThenCreate([] {}, [] {}); });
    continueGate failedPublish;
    bool createdAfterFailure = false;
    rejects([&] { failedPublish.publishThenCreate([] { throw std::runtime_error("flush failure"); }, [&] { createdAfterFailure = true; }); });
    require(!createdAfterFailure && failedPublish.resolve(false, false, true) == decision::reject);
    rejects([&] { failedPublish.publishThenCreate([] {}, [] {}); });
    continueGate collision;
    rejects([&] { collision.publishThenCreate([] {}, [] { throw std::runtime_error("early externally created event, even unsignaled"); }); });
    require(collision.resolve(false, false, true) == decision::reject);
    rejects([&] { collision.publishThenCreate([] {}, [] {}); });
    auto lateCancel = prefix(); lateCancel.append(step::continued, {5, 5}, 32, 100);
    rejects([&] { lateCancel.append(step::guiCancel, {6, 6}, 32, 100); }); // Cancel after Continue cannot fabricate a Cancel PASS.
    lateCancel.append(step::failed, {6, 6}, 32, 100);
    lateCancel.append(step::extractionAborted, {7, 7}, 32, 100);
    lateCancel.append(step::helperNotStarted, {8, 8}, 32, 100);
    require(lateCancel.events().back().stage == step::helperNotStarted);
    auto cancelled = prefix();
    cancelled.append(step::guiCancel, {5, 5}, 32, 100);
    cancelled.append(step::cancellationObserved, {6, 6}, 32, 100);
    cancelled.append(step::extractionAborted, {7, 7}, 32, 100);
    cancelled.append(step::helperNotStarted, {8, 8}, 32, 100);
    require(cancelled.events().size() == 8);
    rejects([&] { cancelled.append(step::continued, {9, 9}, 32, 100); });
    auto resumed = prefix(); resumed.append(step::continued, {5, 5}, 32, 100);
    resumed.append(step::extractionCompleted, {6, 6}, 100, 100);
    resumed.append(step::helperNotStarted, {7, 7}, 100, 100); require(resumed.events().size() == 7);
    for (auto bad : {step::cancellationObserved, step::extractionAborted, step::helperNotStarted, step::extractionCompleted}) {
        auto state = prefix(); rejects([&] { state.append(bad, {5, 5}, 32, 100); });
    }
    for (int phase = 0; phase < 4; ++phase) {
        auto full = prefix(); trace failed;
        for (int i = 0; i <= phase; ++i) {
            auto e = full.events()[static_cast<size_t>(i)]; failed.append(e.stage, e.time, e.bytes, e.total);
        }
        auto bytes = failed.events().back().bytes;
        failed.append(step::failed, {5, 5}, bytes, 100); // timeout / controller / I/O failure
        failed.append(step::extractionAborted, {6, 6}, bytes, 100);
        failed.append(step::helperNotStarted, {7, 7}, bytes, 100);
        require(failed.events().back().stage == step::helperNotStarted);
    }
    auto state = prefix();
    rejects([&] { state.append(step::guiCancel, {5, 3}, 32, 100); });
    rejects([&] { state.append(step::guiCancel, {0, 5}, 32, 100); });
    rejects([&] { state.append(step::guiCancel, {5, 30005}, 32, 100); });
    rejects([&] { state.append(step::guiCancel, {5, 5}, 32, 101); });
    trace empty; rejects([&] { empty.append(step::barrierReached, {1, 1}, 32, 100); });
    trace partial; partial.append(step::installRequested, {1, 1}, 0, 100); partial.append(step::extractionStarted, {2, 2}, 0, 100);
    rejects([&] { partial.append(step::payloadWritten, {3, 3}, 0, 100); });
    rejects([&] { partial.append(step::payloadWritten, {3, 3}, 100, 100); });
    std::cout << "Portable extraction policy: " << checks << " checks passed\n";
}
