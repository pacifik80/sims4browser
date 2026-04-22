#pragma once

#include <Windows.h>

#include <cstdint>
#include <mutex>
#include <string>
#include <unordered_map>

namespace ts4dx11
{
    class EventLogger
    {
    public:
        static EventLogger& Instance();

        const std::string& SessionId() const noexcept;
        DWORD ProcessId() const noexcept;
        std::wstring SessionDirectory() const;
        std::string TimestampUtc() const;
        bool WriteLine(const std::string& jsonLine);
        bool WriteBootstrap(const char* eventType, const std::string& extraJson = {});
        bool WriteSessionTrace(const char* component, const char* action, const std::string& extraJson = {});
        DWORD LastWriteError() const noexcept;

    private:
        EventLogger();
        ~EventLogger();

        void EnsureSinkLocked();
        void EnsureSessionDirectoryLocked();
        std::wstring LogDirectoryLocked() const;
        std::wstring FilePathForEventTypeLocked(const std::string& eventType) const;
        std::wstring TraceFilePathLocked() const;
        HANDLE HandleForEventTypeLocked(const std::string& eventType);
        HANDLE TraceHandleLocked();
        bool WriteToHandleLocked(HANDLE handle, const std::string& payload, bool flushAfterWrite);
        static std::string MakeSessionId(DWORD processId);

        mutable std::mutex mutex_;
        std::wstring sessionDirectory_;
        std::string sessionId_;
        DWORD processId_;
        DWORD lastWriteError_;
        std::uint64_t totalBytesWritten_;
        std::uint64_t nextProgressSignalBytes_;
        std::unordered_map<std::string, HANDLE> handles_;
        HANDLE traceHandle_;
    };

    std::string JsonEscape(const std::string& value);
    std::string WideToUtf8(const std::wstring& value);
    std::wstring Utf8ToWide(const std::string& value);
}
