#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#undef GetCurrentTime
#include <string>
#include <functional>
#include <thread>
#include <atomic>
#include <mutex>
#include <queue>
#include <condition_variable>

namespace AutoTyperWidget
{
    class IpcClient
    {
    public:
        using MessageCallback = std::function<void(const std::string& rawJson)>;
        using ConnectionCallback = std::function<void(bool isConnected)>;

        IpcClient();
        ~IpcClient();

        bool Connect();
        void Disconnect();
        bool IsConnected() const;

        bool SendCommand(const std::string& json);
        
        void SetMessageCallback(MessageCallback cb);
        void SetConnectionCallback(ConnectionCallback cb);

        void StartBackgroundListener();
        void StopBackgroundListener();

        static bool LaunchAutoTyperApp();

    private:
        void ListenerThreadProc();
        void SenderThreadProc();
        HANDLE GetPipeHandle();

        HANDLE m_hPipe;
        HANDLE m_hStopEvent;
        std::atomic<bool> m_isConnected;
        std::atomic<bool> m_isListening;
        
        std::thread m_listenerThread;
        std::thread m_senderThread;
        
        mutable std::mutex m_pipeMutex;
        std::mutex m_queueMutex;
        std::condition_variable m_queueCv;
        std::queue<std::string> m_outgoingQueue;

        MessageCallback m_messageCallback;
        ConnectionCallback m_connectionCallback;
    };
}
