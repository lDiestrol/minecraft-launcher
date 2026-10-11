#define UNICODE
#define _UNICODE
#define NOMINMAX
#include "storage.h"
#include "protocol.h"
#include <iostream>
#include <fstream>
#include <thread>

int checks = 0;
void require(bool value) { if (!value) throw std::runtime_error("test assertion"); ++checks; }
template<class F> void rejects(F action) { bool rejected = false; try { action(); } catch (const std::exception&) { rejected = true; } require(rejected); }
json::object response(unsigned id, const wchar_t* type, const std::wstring& extra = L"") {
    auto text = L"{\"v\":1,\"id\":" + std::to_wstring(id) + L",\"nonce\":\"token\",\"sid\":\"user\",\"type\":" + json::quote(type) + extra + L"}";
    return json::parser(text).read();
}
void prepare(responseContract& contract) {
    contract.accept(response(1, L"inspection", L",\"game\":\"\",\"existing\":false,\"pending\":false"));
    contract.accept(response(2, L"preflight", L",\"app\":\"D:\\\\App\",\"game\":\"D:\\\\GameData\",\"requiredBytes\":1,\"availableBytes\":2"));
}
int wmain(int count, wchar_t** arguments) {
    try {
        if (count != 3) throw std::runtime_error("expected bundle and disposable test folder");
        auto root = normalize(arguments[2]); makeDirectory(root);
        std::wstring unicode = L"D:\\Игры с пробелами\\\"quoted\"\\";
        require(json::wide(json::utf8(unicode)) == unicode);
        auto objectText = L"{\"value\":" + json::quote(unicode) + L"}"; require(json::text(json::parser(objectText).read(), L"value") == unicode);
        for (const auto* text : {L"[]", L"{\"v\":1,\"v\":2}", L"{\"a\":{}}", L"{\"a\":1.2}", L"{\"a\":01}", L"{\"a\":\"\\ud800\"}", L"{\"a\":1}tail"})
            rejects([&] { json::parser(std::wstring(text)).read(); });
        rejects([] { json::wide(std::string(1, static_cast<char>(0xFF))); });
        for (const auto* path : {L"//server/share/GameData", L"\\\\?\\C:\\test", L"relative", L"C:relative", L"C:\\root\\..\\other", L"C:\\a\"b", L"C:\\file:stream", L"C:\\NUL", L"C:\\trailing.", L"C:\\trailing "}) rejects([&] { normalize(path); });
        require(normalize(L"D:/Игры с пробелами/") == L"D:\\Игры с пробелами");
        rejects([] { noOverlap(L"D:\\root", L"D:\\root\\child"); }); rejects([] { noOverlap(L"D:\\root\\child", L"D:\\root"); });
        for (const auto* type : {L"success", L"launched", L"progress"}) { responseContract c(L"token", L"user", L"D:\\App"); rejects([&] { c.accept(response(1, type)); }); }
        responseContract c(L"token", L"user", L"D:\\App"); prepare(c);
        c.accept(response(3, L"progress", L",\"stage\":\"Completed\",\"message\":\"done\"")); require(!c.isComplete());
        rejects([&] { c.accept(response(3, L"progress", L",\"stage\":\"Setup\",\"message\":\"regression\"")); });
        rejects([&] { c.accept(response(3, L"success", L",\"app\":\"E:\\\\App\",\"game\":\"D:\\\\GameData\",\"alreadyInstalled\":false")); });
        c.accept(response(3, L"success", L",\"app\":\"D:\\\\App\",\"game\":\"D:\\\\GameData\",\"alreadyInstalled\":false")); require(c.isComplete());
        c.accept(response(5, L"launched")); rejects([&] { c.accept(response(5, L"launched")); });
        responseContract failure(L"token", L"user", L"D:\\App"); failure.accept(response(1, L"failure", L",\"error\":\"Identity\",\"message\":\"refused\"")); require(!failure.isComplete());
        rejects([&] { failure.accept(response(1, L"inspection")); });
        responseContract wrong(L"other", L"user", L"D:\\App"); rejects([&] { wrong.accept(response(1, L"inspection")); });
        responseContract noProgress(L"token", L"user", L"D:\\App"); prepare(noProgress);
        rejects([&] { noProgress.accept(response(3, L"success", L",\"app\":\"D:\\\\App\",\"game\":\"D:\\\\GameData\",\"alreadyInstalled\":false")); });
        bundle payload(arguments[1]); require(payload.bytes > 0);
        std::atomic_bool cancelled = true; auto cancelledRoot = root + L"\\cancelled"; makeDirectory(cancelledRoot);
        rejects([&] { payload.extract(cancelledRoot, cancelled); }); require(std::filesystem::is_empty(cancelledRoot));
        auto interrupted = root + L"\\interrupted"; makeDirectory(interrupted); cancelled = false; std::atomic_bool finished = false;
        std::thread cancellation([&] {
            while (!finished && GetFileAttributesW((interrupted + L"\\bootstrapper-catalog.json").c_str()) == INVALID_FILE_ATTRIBUTES) Sleep(1);
            if (!finished) cancelled = true;
        });
        bool stopped = false; try { payload.extract(interrupted, cancelled); } catch (const std::exception&) { stopped = true; }
        finished = true; cancellation.join(); require(stopped); require(cancelled); require(std::filesystem::exists(interrupted + L"\\bootstrapper-catalog.json"));
        cancelled = false; auto extracted = root + L"\\extracted"; makeDirectory(extracted); payload.extract(extracted, cancelled);
        require(std::filesystem::exists(extracted + L"\\helper\\Launcher.InstallHelper.exe"));
        rejects([&] { payload.extract(extracted, cancelled); }); // CreateNew, never overwrite.
        auto broken = root + L"\\corrupt-index.exe"; require(CopyFileW(arguments[1], broken.c_str(), TRUE) != 0);
        { std::fstream file(std::filesystem::path(broken), std::ios::in | std::ios::out | std::ios::binary); file.seekg(-1, std::ios::end); char value = 0; file.get(value); file.seekp(-1, std::ios::end); file.put(static_cast<char>(value ^ 0xFF)); }
        rejects([&] { bundle invalid(broken); });
        auto corrupt = root + L"\\corrupt-payload.exe"; require(CopyFileW(arguments[1], corrupt.c_str(), TRUE) != 0);
        { std::fstream file(std::filesystem::path(corrupt), std::ios::in | std::ios::out | std::ios::binary);
            unsigned long long index = 0, offset = 0; file.seekg(-44, std::ios::end); file.read(reinterpret_cast<char*>(&index), 8);
            file.seekg(static_cast<std::streamoff>(index + 20)); file.read(reinterpret_cast<char*>(&offset), 8); file.seekp(static_cast<std::streamoff>(offset)); file.put('x'); }
        auto badRoot = root + L"\\bad-payload"; makeDirectory(badRoot); bundle bad(corrupt); rejects([&] { bad.extract(badRoot, cancelled); });
        require(!std::filesystem::exists(badRoot + L"\\helper\\Launcher.InstallHelper.exe"));
        std::cout << "Native checks: " << checks << " passed; retained isolated artifacts: " << json::utf8(root) << '\n'; return 0;
    } catch (const std::exception& exception) { std::cerr << exception.what() << '\n'; return 1; }
}
