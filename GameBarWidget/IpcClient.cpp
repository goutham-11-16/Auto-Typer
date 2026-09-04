#include "IpcClient.h"
#include "Logger.h"
#include <shellapi.h>
#include <vector>
#include <iostream>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.System.h>

namespace AutoTyperWidget
{
    static const wchar_t* PIPE_NAME = L"\\\\.\\pipe\\AutoTyper_GameBar_IPC";

    IpcClient::IpcClient()
        : m_hPipe(INVALID_HANDLE_VALUE)
        , m_hStopEvent(CreateEventW(NULL, TRUE, FALSE, NULL))
        , m_isConnected(false)
        , m_isListening(false)
    {
    }

    IpcClient::~IpcClient()
    {
        StopBackgroundListener();
        Disconnect();
        if (m_hStopEvent)
        {
            CloseHandle(m_hStopEvent);
            m_hStopEvent = NULL;
        }
    }

    HANDLE IpcClient::GetPipeHandle()
    {
        std::lock_guard<std::mutex> lock(m_pipeMutex);
        return m_hPipe;
    }

    bool IpcClient::Connect()
    {
        try
        {
            std::lock_guard<std::mutex> lock(m_pipeMutex);
            if (m_hPipe != INVALID_HANDLE_VALUE)
            {
                return true;
            }

            m_hPipe = CreateFileW(
                PIPE_NAME,
                GENERIC_READ | GENERIC_WRITE,
                0,
                NULL,
                OPEN_EXISTING,
                FILE_FLAG_OVERLAPPED,
                NULL
            );

            if (m_hPipe == INVALID_HANDLE_VALUE)
            {
                m_isConnected = false;
                return false;
            }

            DWORD dwMode = PIPE_READMODE_BYTE;
            SetNamedPipeHandleState(m_hPipe, &dwMode, NULL, NULL);

            m_isConnected = true;
            WIDGET_LOG(L"[IpcClient::Connect] Connected to named pipe with FILE_FLAG_OVERLAPPED.");
            return true;
        }
        catch (...)
        {
            m_isConnected = false;
            return false;
        }
    }

    void IpcClient::Disconnect()
    {
        try
        {
            std::lock_guard<std::mutex> lock(m_pipeMutex);
            if (m_hPipe != INVALID_HANDLE_VALUE)
            {
                CancelIoEx(m_hPipe, NULL);
                CloseHandle(m_hPipe);
                m_hPipe = INVALID_HANDLE_VALUE;
                WIDGET_LOG(L"[IpcClient::Disconnect] Pipe handle closed.");
            }
            m_isConnected = false;
        }
        catch (...)
        {
            m_isConnected = false;
        }
    }

    bool IpcClient::IsConnected() const
    {
        std::lock_guard<std::mutex> lock(m_pipeMutex);
        return m_isConnected && (m_hPipe != INVALID_HANDLE_VALUE);
    }

    bool IpcClient::SendCommand(const std::string& json)
    {
        try
        {
            std::string payload = json;
            if (payload.empty() || payload.back() != '\n')
            {
                payload.push_back('\n');
            }

            {
                std::lock_guard<std::mutex> lock(m_queueMutex);
                m_outgoingQueue.push(std::move(payload));
            }
            m_queueCv.notify_one();
            return true;
        }
        catch (...)
        {
            return false;
        }
    }

    void IpcClient::SetMessageCallback(MessageCallback cb)
    {
        m_messageCallback = cb;
    }

    void IpcClient::SetConnectionCallback(ConnectionCallback cb)
    {
        m_connectionCallback = cb;
    }

    void IpcClient::StartBackgroundListener()
    {
        if (m_isListening) return;

        m_isListening = true;
        if (m_hStopEvent) ResetEvent(m_hStopEvent);

        WIDGET_LOG(L"[IpcClient::StartBackgroundListener] Starting listener and sender threads.");
        m_listenerThread = std::thread(&IpcClient::ListenerThreadProc, this);
        m_senderThread = std::thread(&IpcClient::SenderThreadProc, this);
    }

    void IpcClient::StopBackgroundListener()
    {
        if (!m_isListening) return;

        WIDGET_LOG(L"[IpcClient::StopBackgroundListener] Stopping listener and sender.");
        m_isListening = false;
        if (m_hStopEvent) SetEvent(m_hStopEvent);
        m_queueCv.notify_all();

        Disconnect();

        if (m_listenerThread.joinable())
        {
            m_listenerThread.join();
        }
        if (m_senderThread.joinable())
        {
            m_senderThread.join();
        }

        m_connectionCallback = nullptr;
        m_messageCallback = nullptr;
    }

    void IpcClient::SenderThreadProc()
    {
        OVERLAPPED osWrite = { 0 };
        osWrite.hEvent = CreateEventW(NULL, TRUE, FALSE, NULL);

        while (m_isListening)
        {
            std::string msg;
            {
                std::unique_lock<std::mutex> lock(m_queueMutex);
                m_queueCv.wait(lock, [this]() {
                    return !m_isListening || !m_outgoingQueue.empty();
                });

                if (!m_isListening) break;
                if (!m_outgoingQueue.empty())
                {
                    msg = std::move(m_outgoingQueue.front());
                    m_outgoingQueue.pop();
                }
            }

            if (msg.empty()) continue;

            HANDLE pipe = GetPipeHandle();
            if (pipe == INVALID_HANDLE_VALUE) continue;

            ResetEvent(osWrite.hEvent);
            DWORD bytesWritten = 0;
            BOOL success = WriteFile(pipe, msg.data(), static_cast<DWORD>(msg.size()), &bytesWritten, &osWrite);
            if (!success)
            {
                DWORD err = GetLastError();
                if (err == ERROR_IO_PENDING)
                {
                    HANDLE waitHandles[2] = { osWrite.hEvent, m_hStopEvent };
                    DWORD waitRes = WaitForMultipleObjects(2, waitHandles, FALSE, 5000);
                    if (waitRes == WAIT_OBJECT_0)
                    {
                        GetOverlappedResult(pipe, &osWrite, &bytesWritten, FALSE);
                        WIDGET_LOG(L"[IpcClient::Sender] Async write completed: " + std::to_wstring(bytesWritten) + L" bytes.");
                    }
                    else
                    {
                        WIDGET_LOG(L"[IpcClient::Sender] Write timeout or stopped.");
                        CancelIoEx(pipe, &osWrite);
                    }
                }
                else
                {
                    WIDGET_LOG(L"[IpcClient::Sender] WriteFile failed, error=" + std::to_wstring(err));
                }
            }
            else
            {
                WIDGET_LOG(L"[IpcClient::Sender] Immediate write completed: " + std::to_wstring(bytesWritten) + L" bytes.");
            }
        }

        if (osWrite.hEvent) CloseHandle(osWrite.hEvent);
    }

    void IpcClient::ListenerThreadProc()
    {
        OVERLAPPED osRead = { 0 };
        osRead.hEvent = CreateEventW(NULL, TRUE, FALSE, NULL);
        std::string buffer;
        char tempBuf[16384];

        while (m_isListening)
        {
            if (!IsConnected())
            {
                if (Connect())
                {
                    if (m_connectionCallback)
                    {
                        try { m_connectionCallback(true); } catch (...) {}
                    }
                    SendCommand("{\"command\":\"STATUS\"}");
                }
                else
                {
                    if (m_connectionCallback)
                    {
                        try { m_connectionCallback(false); } catch (...) {}
                    }
                    WaitForSingleObject(m_hStopEvent, 1500);
                    continue;
                }
            }

            HANDLE pipe = GetPipeHandle();
            if (pipe == INVALID_HANDLE_VALUE)
            {
                WaitForSingleObject(m_hStopEvent, 500);
                continue;
            }

            ResetEvent(osRead.hEvent);
            DWORD bytesRead = 0;
            BOOL success = ReadFile(pipe, tempBuf, sizeof(tempBuf) - 1, &bytesRead, &osRead);

            if (!success)
            {
                DWORD err = GetLastError();
                if (err == ERROR_IO_PENDING)
                {
                    HANDLE waitHandles[2] = { osRead.hEvent, m_hStopEvent };
                    DWORD waitRes = WaitForMultipleObjects(2, waitHandles, FALSE, INFINITE);
                    if (waitRes == WAIT_OBJECT_0)
                    {
                        if (GetOverlappedResult(pipe, &osRead, &bytesRead, FALSE) && bytesRead > 0)
                        {
                            success = TRUE;
                        }
                        else
                        {
                            success = FALSE;
                        }
                    }
                    else
                    {
                        CancelIoEx(pipe, &osRead);
                        break;
                    }
                }
                else
                {
                    success = FALSE;
                }
            }

            if (!success || bytesRead == 0)
            {
                WIDGET_LOG(L"[IpcClient::Listener] Pipe read failed or disconnected, error=" + std::to_wstring(GetLastError()));
                Disconnect();
                if (m_connectionCallback)
                {
                    try { m_connectionCallback(false); } catch (...) {}
                }
                WaitForSingleObject(m_hStopEvent, 500);
                continue;
            }

            tempBuf[bytesRead] = '\0';
            buffer.append(tempBuf, bytesRead);

            size_t pos = 0;
            while ((pos = buffer.find('\n')) != std::string::npos)
            {
                std::string line = buffer.substr(0, pos);
                buffer.erase(0, pos + 1);

                while (!line.empty() && (line.back() == '\r' || line.back() == ' '))
                {
                    line.pop_back();
                }

                if (!line.empty() && m_messageCallback)
                {
                    WIDGET_LOG(L"[IpcClient::Listener] Message received (" + std::to_wstring(line.size()) + L" chars)");
                    try
                    {
                        m_messageCallback(line);
                    }
                    catch (...) {}
                }
            }
        }

        if (osRead.hEvent) CloseHandle(osRead.hEvent);
    }

    bool IpcClient::LaunchAutoTyperApp()
    {
        WIDGET_LOG(L"[IpcClient::LaunchAutoTyperApp] Attempting to launch Auto-Typer desktop application");
        try
        {
            // 1. Launch via Windows protocol launcher (Works natively from UWP AppContainer!)
            winrt::Windows::Foundation::Uri uri(L"autotyper:start");
            winrt::Windows::System::Launcher::LaunchUriAsync(uri);
            WIDGET_LOG(L"[IpcClient::LaunchAutoTyperApp] LaunchUriAsync called for autotyper:start");
        }
        catch (...) {}

        // 2. Also try direct paths if accessible
        wchar_t localApp[MAX_PATH];
        if (GetEnvironmentVariableW(L"LOCALAPPDATA", localApp, MAX_PATH) > 0)
        {
            std::wstring progPath = std::wstring(localApp) + L"\\Programs\\Auto Typer byGo\\AutoTyper-byGo.exe";
            DWORD attr = GetFileAttributesW(progPath.c_str());
            if (attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY))
            {
                HINSTANCE hInst = ShellExecuteW(NULL, L"open", progPath.c_str(), NULL, NULL, SW_SHOWNORMAL);
                if (reinterpret_cast<INT_PTR>(hInst) > 32)
                {
                    WIDGET_LOG(L"[IpcClient::LaunchAutoTyperApp] ShellExecuteW succeeded for installed path");
                    return true;
                }
            }
        }

        const wchar_t* candidates[] = {
            L"AutoTyper-byGo.exe",
            L"..\\AutoTyper\\bin\\Release\\net8.0-windows\\win-x64\\AutoTyper-byGo.exe",
            L"..\\AutoTyper\\bin\\Debug\\net8.0-windows\\win-x64\\AutoTyper-byGo.exe",
            L"..\\AutoTyper\\bin\\Release\\net8.0-windows\\win-x64\\publish\\AutoTyper-byGo.exe",
            L"..\\AutoTyper\\bin\\Release\\net8.0-windows\\AutoTyper-byGo.exe",
            L"..\\AutoTyper\\bin\\Debug\\net8.0-windows\\AutoTyper-byGo.exe",
            L"D:\\project files\\Auto typer\\Auto-Typer\\AutoTyper\\bin\\Release\\net8.0-windows\\win-x64\\AutoTyper-byGo.exe",
            L"D:\\project files\\Auto typer\\Auto-Typer\\AutoTyper\\bin\\Release\\net8.0-windows\\win-x64\\publish\\AutoTyper-byGo.exe",
            L"D:\\project files\\Auto typer\\Auto-Typer\\AutoTyper\\bin\\Debug\\net8.0-windows\\win-x64\\AutoTyper-byGo.exe"
        };

        for (const auto& path : candidates)
        {
            DWORD attrib = GetFileAttributesW(path);
            if (attrib != INVALID_FILE_ATTRIBUTES && !(attrib & FILE_ATTRIBUTE_DIRECTORY))
            {
                HINSTANCE hInst = ShellExecuteW(NULL, L"open", path, NULL, NULL, SW_SHOWNORMAL);
                if (reinterpret_cast<INT_PTR>(hInst) > 32)
                {
                    WIDGET_LOG(L"[IpcClient::LaunchAutoTyperApp] ShellExecuteW succeeded for candidate: " + std::wstring(path));
                    return true;
                }
            }
        }

        return false;
    }
}
