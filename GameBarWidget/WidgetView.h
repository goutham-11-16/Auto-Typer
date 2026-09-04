#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#undef GetCurrentTime

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Text.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Controls.Primitives.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Shapes.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Xaml.Input.h>
#include <winrt/Windows.ApplicationModel.DataTransfer.h>
#include "winrt/Microsoft.Gaming.XboxGameBar.h"

#include "IpcClient.h"
#include <memory>
#include <vector>
#include <string>

namespace AutoTyperWidget
{
    struct SnippetItem
    {
        std::wstring id;
        std::wstring name;
        std::wstring text;
        std::wstring mode;
        int delayPerChar;
        int delayPerWord;
        std::wstring hotkey;
    };

    class WidgetView
    {
    public:
        WidgetView(winrt::Microsoft::Gaming::XboxGameBar::XboxGameBarWidget widget);
        ~WidgetView();

        winrt::Windows::UI::Xaml::UIElement GetRootElement();
        void SetWidget(winrt::Microsoft::Gaming::XboxGameBar::XboxGameBarWidget widget);

    private:
        void BuildUi();
        void SetupIpc();
        void SetupGameBarEvents();

        // UI Event Handlers
        void OnPauseClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnStopClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnSaveClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnAddClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnDeleteClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnClipboardClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnClearClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnConnectClicked(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OnSnippetSelected(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::SelectionChangedEventArgs const& e);
        void InsertMacroToken(const std::wstring& token);

        // IPC Message Handling
        void HandleIpcMessage(const std::string& rawJson);
        void UpdateConnectionState(bool isConnected);

        // Helpers
        void UpdateThemeAndOpacity();
        void ParseSnippetsJson(const std::string& json);
        void PopulateSnippets(const std::vector<SnippetItem>& snippets, const std::wstring& selectId);
        void PopulateSnippetDetails(int index);

        winrt::Microsoft::Gaming::XboxGameBar::XboxGameBarWidget m_widget;
        std::unique_ptr<IpcClient> m_ipcClient;

        // UI Elements
        winrt::Windows::UI::Xaml::Controls::Grid m_rootGrid{ nullptr };
        winrt::Windows::UI::Xaml::Controls::TextBlock m_statusText{ nullptr };
        winrt::Windows::UI::Xaml::Shapes::Ellipse m_statusDot{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_connectBtn{ nullptr };
        
        // Snippet management
        winrt::Windows::UI::Xaml::Controls::ComboBox m_snippetCombo{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_addBtn{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_deleteBtn{ nullptr };

        // Fields matching desktop app
        winrt::Windows::UI::Xaml::Controls::TextBox m_nameBox{ nullptr };
        winrt::Windows::UI::Xaml::Controls::TextBox m_hotkeyBox{ nullptr };
        winrt::Windows::UI::Xaml::Controls::ComboBox m_modeCombo{ nullptr };
        winrt::Windows::UI::Xaml::Controls::TextBox m_delayCharBox{ nullptr };
        winrt::Windows::UI::Xaml::Controls::TextBox m_delayWordBox{ nullptr };

        // Content editor
        winrt::Windows::UI::Xaml::Controls::TextBox m_inputTextBox{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_btnEnter{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_btnTab{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_btnDelay{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_clipboardBtn{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_clearBtn{ nullptr };

        // Actions
        winrt::Windows::UI::Xaml::Controls::Button m_saveBtn{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Grid m_typingControlGrid{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_pauseBtn{ nullptr };
        winrt::Windows::UI::Xaml::Controls::Button m_stopBtn{ nullptr };
        winrt::Windows::UI::Xaml::Controls::ProgressBar m_progressBar{ nullptr };
        winrt::Windows::UI::Xaml::Controls::TextBlock m_progressText{ nullptr };

        // State
        std::vector<SnippetItem> m_snippets;
        std::wstring m_currentSnippetId;
        bool m_isTyping;
        bool m_isPaused;
        bool m_isUpdatingCombo{ false };
        winrt::event_token m_snippetSelectionToken{};
        winrt::Windows::UI::Core::CoreDispatcher m_dispatcher{ nullptr };
    };
}
