#include "Logger.hpp"

#include <chrono>
#include <filesystem>
#include <iomanip>
#include <sstream>

namespace ts4dx11
{
    namespace
    {
        constexpr std::uint64_t ProgressSignalStepBytes = 200ull * 1024ull * 1024ull;
        extern "C" IMAGE_DOS_HEADER __ImageBase;

        std::wstring GetEnvironmentString(const wchar_t* name)
        {
            const DWORD required = GetEnvironmentVariableW(name, nullptr, 0);
            if (required == 0)
            {
                return {};
            }

            std::wstring value(static_cast<std::size_t>(required), L'\0');
            const DWORD copied = GetEnvironmentVariableW(name, value.data(), required);
            if (copied == 0 || copied >= required)
            {
                return {};
            }

            value.resize(copied);
            return value;
        }

        std::wstring GetFallbackDirectory()
        {
            wchar_t buffer[MAX_PATH];
            const DWORD tempLength = GetTempPathW(MAX_PATH, buffer);
            if (tempLength == 0 || tempLength >= MAX_PATH)
            {
                return L".";
            }

            return buffer;
        }

        std::wstring GetProxyDirectory()
        {
            wchar_t modulePathBuffer[MAX_PATH];
            const DWORD length = GetModuleFileNameW(reinterpret_cast<HMODULE>(&__ImageBase), modulePathBuffer, MAX_PATH);
            if (length == 0 || length >= MAX_PATH)
            {
                return {};
            }

            std::wstring path(modulePathBuffer, modulePathBuffer + length);
            const std::size_t separator = path.find_last_of(L"\\/");
            if (separator == std::wstring::npos)
            {
                return {};
            }

            return path.substr(0, separator);
        }

        std::wstring TrimWhitespace(const std::wstring& value)
        {
            const std::wstring whitespace = L" \t\r\n";
            const std::size_t start = value.find_first_not_of(whitespace);
            if (start == std::wstring::npos)
            {
                return {};
            }

            const std::size_t end = value.find_last_not_of(whitespace);
            return value.substr(start, end - start + 1);
        }

        std::wstring ReadSessionDirectoryFromSidecar()
        {
            const std::wstring proxyDirectory = GetProxyDirectory();
            if (proxyDirectory.empty())
            {
                return {};
            }

            std::wstring sidecarPath = proxyDirectory;
            if (!sidecarPath.empty() && sidecarPath.back() != L'\\')
            {
                sidecarPath.push_back(L'\\');
            }

            sidecarPath += L"ts4-dx11-introspection-session-dir.txt";
            const HANDLE handle = CreateFileW(
                sidecarPath.c_str(),
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (handle == INVALID_HANDLE_VALUE)
            {
                return {};
            }

            LARGE_INTEGER size {};
            if (GetFileSizeEx(handle, &size) == FALSE || size.QuadPart <= 0 || size.QuadPart > 4096)
            {
                CloseHandle(handle);
                return {};
            }

            std::string bytes(static_cast<std::size_t>(size.QuadPart), '\0');
            DWORD read = 0;
            const BOOL ok = ReadFile(handle, bytes.data(), static_cast<DWORD>(bytes.size()), &read, nullptr);
            CloseHandle(handle);
            if (ok == FALSE || read == 0)
            {
                return {};
            }

            bytes.resize(read);
            if (bytes.size() >= 3 &&
                static_cast<unsigned char>(bytes[0]) == 0xEF &&
                static_cast<unsigned char>(bytes[1]) == 0xBB &&
                static_cast<unsigned char>(bytes[2]) == 0xBF)
            {
                bytes.erase(0, 3);
            }

            return TrimWhitespace(Utf8ToWide(bytes));
        }

        std::wstring GetSessionDirectoryFromEnvironment()
        {
            std::wstring value = GetEnvironmentString(L"TS4_DX11_INTROSPECTION_SESSION_DIR");
            if (!value.empty())
            {
                return value;
            }

            value = GetEnvironmentString(L"TS4_DX11_INTROSPECTION_FALLBACK_DIR");
            if (!value.empty())
            {
                return value;
            }

            value = ReadSessionDirectoryFromSidecar();
            if (!value.empty())
            {
                return value;
            }

            return GetFallbackDirectory();
        }

        std::string ExtractEventType(const std::string& jsonLine)
        {
            constexpr const char* marker = "\"event_type\":\"";
            const std::size_t start = jsonLine.find(marker);
            if (start == std::string::npos)
            {
                return "events";
            }

            const std::size_t valueStart = start + std::char_traits<char>::length(marker);
            const std::size_t valueEnd = jsonLine.find('"', valueStart);
            if (valueEnd == std::string::npos)
            {
                return "events";
            }

            const std::string value = jsonLine.substr(valueStart, valueEnd - valueStart);
            if (value == "frame_boundary")
            {
                return "frames";
            }

            if (value == "shader_created")
            {
                return "shaders";
            }

            if (value == "draw_call")
            {
                return "draws";
            }

            if (value == "state_definition")
            {
                return "states";
            }

            if (value == "bookmark")
            {
                return "captures";
            }

            return "events";
        }
    }

    EventLogger& EventLogger::Instance()
    {
        static EventLogger logger;
        return logger;
    }

    EventLogger::EventLogger()
        : sessionDirectory_(GetSessionDirectoryFromEnvironment()),
          sessionId_(MakeSessionId(GetCurrentProcessId())),
          processId_(GetCurrentProcessId()),
          lastWriteError_(ERROR_SUCCESS),
          totalBytesWritten_(0),
          nextProgressSignalBytes_(ProgressSignalStepBytes),
          traceHandle_(INVALID_HANDLE_VALUE)
    {
    }

    EventLogger::~EventLogger()
    {
        for (auto& pair : handles_)
        {
            if (pair.second != INVALID_HANDLE_VALUE)
            {
                FlushFileBuffers(pair.second);
                CloseHandle(pair.second);
            }
        }

        if (traceHandle_ != INVALID_HANDLE_VALUE)
        {
            FlushFileBuffers(traceHandle_);
            CloseHandle(traceHandle_);
        }
    }

    const std::string& EventLogger::SessionId() const noexcept
    {
        return sessionId_;
    }

    DWORD EventLogger::ProcessId() const noexcept
    {
        return processId_;
    }

    std::wstring EventLogger::SessionDirectory() const
    {
        std::scoped_lock lock(mutex_);
        return sessionDirectory_;
    }

    DWORD EventLogger::LastWriteError() const noexcept
    {
        return lastWriteError_;
    }

    std::string EventLogger::TimestampUtc() const
    {
        const auto now = std::chrono::system_clock::now();
        const auto time = std::chrono::system_clock::to_time_t(now);
        std::tm utc {};
        gmtime_s(&utc, &time);

        const auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;

        std::ostringstream stream;
        stream << std::put_time(&utc, "%Y-%m-%dT%H:%M:%S")
               << '.'
               << std::setw(3)
               << std::setfill('0')
               << millis.count()
               << 'Z';
        return stream.str();
    }

    bool EventLogger::WriteLine(const std::string& jsonLine)
    {
        std::scoped_lock lock(mutex_);
        EnsureSinkLocked();

        std::string payload = jsonLine;
        payload.push_back('\n');

        const std::string bucket = ExtractEventType(jsonLine);
        const bool flushAfterWrite = bucket == "frames";

        bool ok = WriteToHandleLocked(HandleForEventTypeLocked(bucket), payload, flushAfterWrite);
        if (bucket != "events")
        {
            ok = WriteToHandleLocked(HandleForEventTypeLocked("events"), payload, flushAfterWrite) && ok;
        }

        while (ok && totalBytesWritten_ >= nextProgressSignalBytes_)
        {
            Beep(950, 70);
            nextProgressSignalBytes_ += ProgressSignalStepBytes;
        }

        return ok;
    }

    bool EventLogger::WriteBootstrap(const char* eventType, const std::string& extraJson)
    {
        std::ostringstream json;
        json << '{'
             << "\"schema_version\":1"
             << ",\"event_type\":\"" << eventType << '"'
             << ",\"timestamp_utc\":\"" << TimestampUtc() << '"'
             << ",\"pid\":" << ProcessId()
             << ",\"session_id\":\"" << SessionId() << '"';

        if (!extraJson.empty())
        {
            json << ',' << extraJson;
        }

        json << '}';
        return WriteLine(json.str());
    }

    bool EventLogger::WriteSessionTrace(const char* component, const char* action, const std::string& extraJson)
    {
        std::scoped_lock lock(mutex_);
        EnsureSessionDirectoryLocked();

        std::ostringstream json;
        json << '{'
             << "\"schema_version\":1"
             << ",\"event_type\":\"session_trace\""
             << ",\"timestamp_utc\":\"" << TimestampUtc() << '"'
             << ",\"pid\":" << ProcessId()
             << ",\"session_id\":\"" << SessionId() << '"'
             << ",\"component\":\"" << JsonEscape(component == nullptr ? "" : component) << '"'
             << ",\"action\":\"" << JsonEscape(action == nullptr ? "" : action) << '"';

        if (!extraJson.empty())
        {
            json << ',' << extraJson;
        }

        json << '}';

        std::string payload = json.str();
        payload.push_back('\n');
        return WriteToHandleLocked(TraceHandleLocked(), payload, true);
    }

    void EventLogger::EnsureSinkLocked()
    {
        EnsureSessionDirectoryLocked();
        std::filesystem::create_directories(sessionDirectory_);
    }

    void EventLogger::EnsureSessionDirectoryLocked()
    {
        if (sessionDirectory_.empty())
        {
            sessionDirectory_ = GetSessionDirectoryFromEnvironment();
        }
    }

    std::wstring EventLogger::LogDirectoryLocked() const
    {
        return sessionDirectory_;
    }

    std::wstring EventLogger::FilePathForEventTypeLocked(const std::string& eventType) const
    {
        std::wstringstream path;
        path << LogDirectoryLocked();
        if (!sessionDirectory_.empty() && sessionDirectory_.back() != L'\\')
        {
            path << L'\\';
        }

        path << Utf8ToWide(eventType) << L".jsonl";
        return path.str();
    }

    std::wstring EventLogger::TraceFilePathLocked() const
    {
        std::wstringstream path;
        path << LogDirectoryLocked();
        if (!sessionDirectory_.empty() && sessionDirectory_.back() != L'\\')
        {
            path << L'\\';
        }

        path << L"session-trace.jsonl";
        return path.str();
    }

    HANDLE EventLogger::HandleForEventTypeLocked(const std::string& eventType)
    {
        const auto iterator = handles_.find(eventType);
        if (iterator != handles_.end())
        {
            return iterator->second;
        }

        const HANDLE handle = CreateFileW(
            FilePathForEventTypeLocked(eventType).c_str(),
            FILE_APPEND_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr,
            OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr);

        handles_.emplace(eventType, handle);
        return handle;
    }

    HANDLE EventLogger::TraceHandleLocked()
    {
        if (traceHandle_ != INVALID_HANDLE_VALUE)
        {
            return traceHandle_;
        }

        const HANDLE handle = CreateFileW(
            TraceFilePathLocked().c_str(),
            FILE_APPEND_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr,
            OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr);
        traceHandle_ = handle;
        return traceHandle_;
    }

    bool EventLogger::WriteToHandleLocked(const HANDLE handle, const std::string& payload, const bool flushAfterWrite)
    {
        if (handle == INVALID_HANDLE_VALUE)
        {
            lastWriteError_ = GetLastError();
            return false;
        }

        DWORD written = 0;
        if (WriteFile(handle, payload.data(), static_cast<DWORD>(payload.size()), &written, nullptr) == FALSE)
        {
            lastWriteError_ = GetLastError();
            return false;
        }

        if (written != payload.size())
        {
            lastWriteError_ = ERROR_WRITE_FAULT;
            return false;
        }

        if (flushAfterWrite && FlushFileBuffers(handle) == FALSE)
        {
            lastWriteError_ = GetLastError();
            return false;
        }

        totalBytesWritten_ += static_cast<std::uint64_t>(written);
        lastWriteError_ = ERROR_SUCCESS;
        return true;
    }

    std::string EventLogger::MakeSessionId(const DWORD processId)
    {
        SYSTEMTIME systemTime {};
        GetSystemTime(&systemTime);

        std::ostringstream stream;
        stream << processId << '-'
               << systemTime.wYear
               << std::setw(2) << std::setfill('0') << systemTime.wMonth
               << std::setw(2) << std::setfill('0') << systemTime.wDay
               << 'T'
               << std::setw(2) << std::setfill('0') << systemTime.wHour
               << std::setw(2) << std::setfill('0') << systemTime.wMinute
               << std::setw(2) << std::setfill('0') << systemTime.wSecond;
        return stream.str();
    }

    std::string JsonEscape(const std::string& value)
    {
        std::ostringstream stream;
        for (const unsigned char ch : value)
        {
            switch (ch)
            {
            case '\\':
                stream << "\\\\";
                break;
            case '"':
                stream << "\\\"";
                break;
            case '\b':
                stream << "\\b";
                break;
            case '\f':
                stream << "\\f";
                break;
            case '\n':
                stream << "\\n";
                break;
            case '\r':
                stream << "\\r";
                break;
            case '\t':
                stream << "\\t";
                break;
            default:
                if (ch < 0x20)
                {
                    stream << "\\u"
                           << std::hex
                           << std::setw(4)
                           << std::setfill('0')
                           << static_cast<int>(ch)
                           << std::dec;
                }
                else
                {
                    stream << static_cast<char>(ch);
                }

                break;
            }
        }

        return stream.str();
    }

    std::string WideToUtf8(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const int needed = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
        std::string result(static_cast<std::size_t>(needed), '\0');
        WideCharToMultiByte(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), result.data(), needed, nullptr, nullptr);
        return result;
    }

    std::wstring Utf8ToWide(const std::string& value)
    {
        if (value.empty())
        {
            return {};
        }

        const int needed = MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), nullptr, 0);
        std::wstring result(static_cast<std::size_t>(needed), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, value.c_str(), static_cast<int>(value.size()), result.data(), needed);
        return result;
    }
}
