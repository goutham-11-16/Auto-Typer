#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#undef GetCurrentTime
#include <winrt/Windows.Storage.h>
#include <winrt/base.h>
#include <string>
#include <mutex>
#include <chrono>
#include <iomanip>
#include <sstream>

namespace AutoTyperWidget
{
    inline std::wstring Utf8ToWide(const std::string& str)
    {
        if (str.empty()) return std::wstring();
        int sizeNeeded = MultiByteToWideChar(CP_UTF8, 0, str.data(), static_cast<int>(str.size()), NULL, 0);
        if (sizeNeeded <= 0) return std::wstring();
        std::wstring wstr(sizeNeeded, 0);
        MultiByteToWideChar(CP_UTF8, 0, str.data(), static_cast<int>(str.size()), &wstr[0], sizeNeeded);
        return wstr;
    }

    inline std::string WideToUtf8(const std::wstring& wstr)
    {
        if (wstr.empty()) return std::string();
        int sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, wstr.data(), static_cast<int>(wstr.size()), NULL, 0, NULL, NULL);
        if (sizeNeeded <= 0) return std::string();
        std::string str(sizeNeeded, 0);
        WideCharToMultiByte(CP_UTF8, 0, wstr.data(), static_cast<int>(wstr.size()), &str[0], sizeNeeded, NULL, NULL);
        return str;
    }

    class Logger
    {
    public:
        static void Log(const wchar_t* message)
        {
            if (!message) return;
            static std::mutex s_logMutex;
            std::lock_guard<std::mutex> lock(s_logMutex);

            try
            {
                SYSTEMTIME st;
                GetLocalTime(&st);
                wchar_t timeBuf[64];
                swprintf_s(timeBuf, L"[%02d:%02d:%02d.%03d] ", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);

                std::wstring fullLine = std::wstring(timeBuf) + message;
                OutputDebugStringW((fullLine + L"\n").c_str());

                // 1. Log to TEMP directory (works unconditionally in all environments)
                wchar_t tempPath[MAX_PATH];
                if (GetTempPathW(MAX_PATH, tempPath) > 0)
                {
                    std::wstring tempLog = std::wstring(tempPath) + L"widget_debug.log";
                    FILE* fTemp = nullptr;
                    _wfopen_s(&fTemp, tempLog.c_str(), L"a, ccs=UTF-8");
                    if (fTemp)
                    {
                        fputws(fullLine.c_str(), fTemp);
                        fputws(L"\n", fTemp);
                        fclose(fTemp);
                    }
                }

                // 2. Also log to LocalFolder if UWP package is initialized
                try
                {
                    auto folder = winrt::Windows::Storage::ApplicationData::Current().LocalFolder().Path();
                    std::wstring logFile = std::wstring(folder.c_str()) + L"\\widget_debug.log";
                    FILE* f = nullptr;
                    _wfopen_s(&f, logFile.c_str(), L"a, ccs=UTF-8");
                    if (f)
                    {
                        fputws(fullLine.c_str(), f);
                        fputws(L"\n", f);
                        fclose(f);
                    }
                }
                catch (...) {}
            }
            catch (...) {}
        }

        static void Log(const std::wstring& message)
        {
            Log(message.c_str());
        }

        static void Log(const winrt::hstring& message)
        {
            Log(message.c_str());
        }

        static void Log(const char* message)
        {
            if (!message) return;
            Log(Utf8ToWide(message));
        }

        static void Log(const std::string& message)
        {
            Log(Utf8ToWide(message));
        }
    };
}

#define WIDGET_LOG(msg) ::AutoTyperWidget::Logger::Log(msg)
