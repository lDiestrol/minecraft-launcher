#pragma once
#include "json.h"
#include <algorithm>
#include <initializer_list>

// Success is a typed coordinator result, never a progress stage or an exit code.
class responseContract {
    std::wstring nonce, sid, expectedApp, expectedGame;
    unsigned long long active = 1; int lastStage = -1; bool completed = false, terminal = false;
    static void keys(const json::object& fields, std::initializer_list<const wchar_t*> extra) {
        for (const auto& field : fields) {
            bool allowed = field.first == L"v" || field.first == L"id" || field.first == L"nonce" || field.first == L"sid" || field.first == L"type";
            for (const auto* key : extra) allowed |= field.first == key;
            if (!allowed) throw std::runtime_error("unknown response field");
        }
        if (fields.size() != 5 + extra.size()) throw std::runtime_error("missing response field");
    }
public:
    responseContract(std::wstring token, std::wstring user, std::wstring app) : nonce(std::move(token)), sid(std::move(user)), expectedApp(std::move(app)) {}
    bool isComplete() const { return completed; }
    unsigned long long requestId() const { return active; }
    std::wstring accept(const json::object& fields) {
        if (terminal || json::integer(fields, L"v") != 1 || json::integer(fields, L"id") != active ||
            json::text(fields, L"nonce") != nonce || json::text(fields, L"sid") != sid) throw std::runtime_error("invalid helper context");
        auto type = json::text(fields, L"type");
        if (type == L"failure") { keys(fields, {L"error", L"message"}); json::text(fields, L"error"); json::text(fields, L"message"); terminal = true; return type; }
        if (type == L"inspection" && active == 1) {
            keys(fields, {L"game", L"existing", L"pending"}); json::text(fields, L"game"); json::boolean(fields, L"existing"); json::boolean(fields, L"pending"); active = 2;
        } else if (type == L"preflight" && active == 2) {
            keys(fields, {L"app", L"game", L"requiredBytes", L"availableBytes"});
            if (_wcsicmp(json::text(fields, L"app").c_str(), expectedApp.c_str())) throw std::runtime_error("unexpected App");
            expectedGame = json::text(fields, L"game"); if (expectedGame.empty()) throw std::runtime_error("missing GameData");
            json::integer(fields, L"requiredBytes"); json::integer(fields, L"availableBytes"); active = 3;
        } else if (type == L"progress" && active == 3 && !completed) {
            keys(fields, {L"stage", L"message"}); json::text(fields, L"message"); auto stage = json::text(fields, L"stage");
            const std::wstring names[] = {L"Preflight", L"Storage", L"Setup", L"Verification", L"Completed"};
            auto position = std::find(std::begin(names), std::end(names), stage); int index = static_cast<int>(position - std::begin(names));
            if (position == std::end(names) || index < lastStage) throw std::runtime_error("invalid progress transition"); lastStage = index;
        } else if (type == L"success" && active == 3 && !completed) {
            keys(fields, {L"app", L"game", L"alreadyInstalled"}); json::boolean(fields, L"alreadyInstalled");
            if (lastStage != 4 || _wcsicmp(json::text(fields, L"app").c_str(), expectedApp.c_str()) ||
                _wcsicmp(json::text(fields, L"game").c_str(), expectedGame.c_str())) throw std::runtime_error("unverified completion paths or stages");
            completed = true; active = 5;
        } else if (type == L"launched" && active == 5 && completed) { keys(fields, {}); terminal = true; }
        else throw std::runtime_error("unexpected helper state transition");
        return type;
    }
};
