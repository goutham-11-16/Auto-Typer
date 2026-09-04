#include "WidgetView.h"
#include "Logger.h"
#include <sstream>
#include <iomanip>

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Controls::Primitives;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::UI::Xaml::Shapes;
using namespace Windows::Data::Json;
using namespace Windows::ApplicationModel::DataTransfer;
using namespace Microsoft::Gaming::XboxGameBar;

namespace AutoTyperWidget
{
    static std::wstring SafeJsonString(const JsonObject& obj, const wchar_t* key, const wchar_t* defaultVal = L"")
    {
        try
        {
            if (obj.HasKey(key))
            {
                auto val = obj.GetNamedValue(key);
                if (val.ValueType() == JsonValueType::String)
                {
                    return val.GetString().c_str();
                }
            }
        }
        catch (...) {}
        return defaultVal;
    }

    static int SafeJsonInt(const JsonObject& obj, const wchar_t* key, int defaultVal = 0)
    {
        try
        {
            if (obj.HasKey(key))
            {
                auto val = obj.GetNamedValue(key);
                if (val.ValueType() == JsonValueType::Number)
                {
                    return static_cast<int>(val.GetNumber());
                }
            }
        }
        catch (...) {}
        return defaultVal;
    }

    static bool SafeJsonBool(const JsonObject& obj, const wchar_t* key, bool defaultVal = false)
    {
        try
        {
            if (obj.HasKey(key))
            {
                auto val = obj.GetNamedValue(key);
                if (val.ValueType() == JsonValueType::Boolean)
                {
                    return val.GetBoolean();
                }
            }
        }
        catch (...) {}
        return defaultVal;
    }

    WidgetView::WidgetView(XboxGameBarWidget widget)
        : m_widget(widget)
        , m_isTyping(false)
        , m_isPaused(false)
        , m_isUpdatingCombo(false)
    {
        WIDGET_LOG(L"[WidgetView::WidgetView] Constructor started");
        try
        {
            m_dispatcher = Windows::UI::Core::CoreWindow::GetForCurrentThread().Dispatcher();
        }
        catch (...) {}
        BuildUi();
        SetupGameBarEvents();
        SetupIpc();
        IpcClient::LaunchAutoTyperApp();
        WIDGET_LOG(L"[WidgetView::WidgetView] Constructor finished");
    }

    WidgetView::~WidgetView()
    {
        WIDGET_LOG(L"[WidgetView::~WidgetView] Destructor");
        if (m_ipcClient)
        {
            m_ipcClient->StopBackgroundListener();
        }
    }

    UIElement WidgetView::GetRootElement()
    {
        return m_rootGrid;
    }

    void WidgetView::SetWidget(XboxGameBarWidget widget)
    {
        m_widget = widget;
        SetupGameBarEvents();
    }

    void WidgetView::BuildUi()
    {
        WIDGET_LOG(L"[WidgetView::BuildUi] Building UI controls");
        try
        {
            m_rootGrid = Grid();
            m_rootGrid.Background(SolidColorBrush(Color{ 255, 24, 24, 28 }));
            m_rootGrid.HorizontalAlignment(HorizontalAlignment::Stretch);
            m_rootGrid.VerticalAlignment(VerticalAlignment::Stretch);
            m_rootGrid.RequestedTheme(ElementTheme::Dark);

            auto row0 = RowDefinition();
            row0.Height(GridLength{ 1, GridUnitType::Star });
            m_rootGrid.RowDefinitions().Append(row0);

            auto scroll = ScrollViewer();
            scroll.VerticalScrollBarVisibility(ScrollBarVisibility::Auto);
            scroll.VerticalScrollMode(ScrollMode::Enabled);
            scroll.HorizontalScrollBarVisibility(ScrollBarVisibility::Disabled);
            scroll.HorizontalScrollMode(ScrollMode::Disabled);
            scroll.ZoomMode(ZoomMode::Disabled);
            scroll.HorizontalAlignment(HorizontalAlignment::Stretch);
            scroll.VerticalAlignment(VerticalAlignment::Stretch);
            Grid::SetRow(scroll, 0);

            auto stack = StackPanel();
            stack.Padding(Thickness{ 12, 10, 12, 12 });
            stack.Spacing(8);
            stack.HorizontalAlignment(HorizontalAlignment::Stretch);
            stack.VerticalAlignment(VerticalAlignment::Top);

            // -------------------------------------------------------------
            // Row 0: Header (Title, Connection dot, Status, Reconnect button)
            // -------------------------------------------------------------
            auto headerGrid = Grid();
            auto colH0 = ColumnDefinition(); colH0.Width(GridLength{ 1, GridUnitType::Star });
            auto colH1 = ColumnDefinition(); colH1.Width(GridLength{ 0, GridUnitType::Auto });
            headerGrid.ColumnDefinitions().Append(colH0);
            headerGrid.ColumnDefinitions().Append(colH1);

            auto title = TextBlock();
            title.Text(L"AUTO-TYPER byGo");
            title.FontWeight(Windows::UI::Text::FontWeights::Bold());
            title.FontSize(13);
            title.Foreground(SolidColorBrush(Color{ 255, 76, 175, 80 })); // Green
            title.VerticalAlignment(VerticalAlignment::Center);
            Grid::SetColumn(title, 0);

            auto statusStack = StackPanel();
            statusStack.Orientation(Orientation::Horizontal);
            statusStack.Spacing(6);
            statusStack.VerticalAlignment(VerticalAlignment::Center);

            m_statusDot = Windows::UI::Xaml::Shapes::Ellipse();
            m_statusDot.Width(8);
            m_statusDot.Height(8);
            m_statusDot.Fill(SolidColorBrush(Color{ 255, 239, 83, 80 })); // Red default
            m_statusDot.VerticalAlignment(VerticalAlignment::Center);

            m_statusText = TextBlock();
            m_statusText.Text(L"Disconnected");
            m_statusText.FontSize(11);
            m_statusText.Foreground(SolidColorBrush(Color{ 200, 200, 200, 200 }));
            m_statusText.VerticalAlignment(VerticalAlignment::Center);

            m_connectBtn = Button();
            m_connectBtn.Content(box_value(L"Reconnect"));
            m_connectBtn.FontSize(11);
            m_connectBtn.Padding(Thickness{ 8, 2, 8, 2 });
            m_connectBtn.Click({ this, &WidgetView::OnConnectClicked });

            statusStack.Children().Append(m_statusDot);
            statusStack.Children().Append(m_statusText);
            statusStack.Children().Append(m_connectBtn);
            Grid::SetColumn(statusStack, 1);

            headerGrid.Children().Append(title);
            headerGrid.Children().Append(statusStack);
            stack.Children().Append(headerGrid);

            // -------------------------------------------------------------
            // Row 1: Snippet Selector + Add + Delete Buttons
            // -------------------------------------------------------------
            auto snipGrid = Grid();
            auto colS0 = ColumnDefinition(); colS0.Width(GridLength{ 1, GridUnitType::Star });
            auto colS1 = ColumnDefinition(); colS1.Width(GridLength{ 0, GridUnitType::Auto });
            auto colS2 = ColumnDefinition(); colS2.Width(GridLength{ 0, GridUnitType::Auto });
            snipGrid.ColumnDefinitions().Append(colS0);
            snipGrid.ColumnDefinitions().Append(colS1);
            snipGrid.ColumnDefinitions().Append(colS2);

            m_snippetCombo = ComboBox();
            m_snippetCombo.HorizontalAlignment(HorizontalAlignment::Stretch);
            m_snippetCombo.FontSize(12);
            m_snippetCombo.Items().Append(box_value(L"[ + Custom Text ]"));
            m_snippetCombo.SelectedIndex(0);
            m_snippetSelectionToken = m_snippetCombo.SelectionChanged({ this, &WidgetView::OnSnippetSelected });
            Grid::SetColumn(m_snippetCombo, 0);

            m_addBtn = Button();
            m_addBtn.Content(box_value(L"+ Add"));
            m_addBtn.FontSize(11);
            m_addBtn.Margin(Thickness{ 6, 0, 0, 0 });
            m_addBtn.Padding(Thickness{ 8, 4, 8, 4 });
            m_addBtn.Click({ this, &WidgetView::OnAddClicked });
            Grid::SetColumn(m_addBtn, 1);

            m_deleteBtn = Button();
            m_deleteBtn.Content(box_value(L"Delete"));
            m_deleteBtn.FontSize(11);
            m_deleteBtn.Foreground(SolidColorBrush(Color{ 255, 239, 83, 80 }));
            m_deleteBtn.Margin(Thickness{ 4, 0, 0, 0 });
            m_deleteBtn.Padding(Thickness{ 8, 4, 8, 4 });
            m_deleteBtn.Click({ this, &WidgetView::OnDeleteClicked });
            Grid::SetColumn(m_deleteBtn, 2);

            snipGrid.Children().Append(m_snippetCombo);
            snipGrid.Children().Append(m_addBtn);
            snipGrid.Children().Append(m_deleteBtn);
            stack.Children().Append(snipGrid);

            // -------------------------------------------------------------
            // Row 2: Snippet Properties (Name & Hotkey Trigger)
            // -------------------------------------------------------------
            auto nameHotGrid = Grid();
            auto colNH0 = ColumnDefinition(); colNH0.Width(GridLength{ 1, GridUnitType::Star });
            auto colNH1 = ColumnDefinition(); colNH1.Width(GridLength{ 1, GridUnitType::Star });
            nameHotGrid.ColumnDefinitions().Append(colNH0);
            nameHotGrid.ColumnDefinitions().Append(colNH1);

            auto nameStack = StackPanel();
            nameStack.Spacing(2);
            nameStack.Margin(Thickness{ 0, 0, 4, 0 });
            auto lblName = TextBlock(); lblName.Text(L"Name"); lblName.FontSize(11); lblName.Foreground(SolidColorBrush(Color{ 180, 200, 200, 200 }));
            m_nameBox = TextBox();
            m_nameBox.PlaceholderText(L"Snippet name");
            m_nameBox.FontSize(12);
            nameStack.Children().Append(lblName);
            nameStack.Children().Append(m_nameBox);
            Grid::SetColumn(nameStack, 0);

            auto hotStack = StackPanel();
            hotStack.Spacing(2);
            hotStack.Margin(Thickness{ 4, 0, 0, 0 });
            auto lblHot = TextBlock(); lblHot.Text(L"Hotkey Trigger"); lblHot.FontSize(11); lblHot.Foreground(SolidColorBrush(Color{ 180, 200, 200, 200 }));
            m_hotkeyBox = TextBox();
            m_hotkeyBox.PlaceholderText(L"e.g. Control, Shift + X");
            m_hotkeyBox.FontSize(12);
            hotStack.Children().Append(lblHot);
            hotStack.Children().Append(m_hotkeyBox);
            Grid::SetColumn(hotStack, 1);

            nameHotGrid.Children().Append(nameStack);
            nameHotGrid.Children().Append(hotStack);
            stack.Children().Append(nameHotGrid);

            // -------------------------------------------------------------
            // Row 3: Typing Mode & Timing Options
            // -------------------------------------------------------------
            auto modeDelayGrid = Grid();
            auto colMD0 = ColumnDefinition(); colMD0.Width(GridLength{ 1, GridUnitType::Star });
            auto colMD1 = ColumnDefinition(); colMD1.Width(GridLength{ 1, GridUnitType::Star });
            modeDelayGrid.ColumnDefinitions().Append(colMD0);
            modeDelayGrid.ColumnDefinitions().Append(colMD1);

            auto modeStack = StackPanel();
            modeStack.Spacing(2);
            modeStack.Margin(Thickness{ 0, 0, 4, 0 });
            auto lblMode = TextBlock(); lblMode.Text(L"Typing Mode"); lblMode.FontSize(11); lblMode.Foreground(SolidColorBrush(Color{ 180, 200, 200, 200 }));
            m_modeCombo = ComboBox();
            m_modeCombo.HorizontalAlignment(HorizontalAlignment::Stretch);
            m_modeCombo.FontSize(12);
            m_modeCombo.Items().Append(box_value(L"HumanLike"));
            m_modeCombo.Items().Append(box_value(L"Paste"));
            m_modeCombo.Items().Append(box_value(L"Fast"));
            m_modeCombo.Items().Append(box_value(L"Macro"));
            m_modeCombo.SelectedIndex(0);
            modeStack.Children().Append(lblMode);
            modeStack.Children().Append(m_modeCombo);
            Grid::SetColumn(modeStack, 0);

            auto delaysStack = StackPanel();
            delaysStack.Spacing(2);
            delaysStack.Margin(Thickness{ 4, 0, 0, 0 });
            auto lblDelays = TextBlock(); lblDelays.Text(L"Delays: Letter / Word (ms)"); lblDelays.FontSize(11); lblDelays.Foreground(SolidColorBrush(Color{ 180, 200, 200, 200 }));

            auto delayInputs = StackPanel();
            delayInputs.Orientation(Orientation::Horizontal);
            delayInputs.Spacing(6);

            m_delayCharBox = TextBox();
            m_delayCharBox.Text(L"1");
            m_delayCharBox.Width(60);
            m_delayCharBox.FontSize(12);

            m_delayWordBox = TextBox();
            m_delayWordBox.Text(L"1");
            m_delayWordBox.Width(60);
            m_delayWordBox.FontSize(12);

            delayInputs.Children().Append(m_delayCharBox);
            delayInputs.Children().Append(m_delayWordBox);
            delaysStack.Children().Append(lblDelays);
            delaysStack.Children().Append(delayInputs);
            Grid::SetColumn(delaysStack, 1);

            modeDelayGrid.Children().Append(modeStack);
            modeDelayGrid.Children().Append(delaysStack);
            stack.Children().Append(modeDelayGrid);

            // -------------------------------------------------------------
            // Row 4: Macro Buttons & Clipboard Paste / Clear
            // -------------------------------------------------------------
            auto macroRow = Grid();
            auto colM0 = ColumnDefinition(); colM0.Width(GridLength{ 1, GridUnitType::Star });
            auto colM1 = ColumnDefinition(); colM1.Width(GridLength{ 0, GridUnitType::Auto });
            macroRow.ColumnDefinitions().Append(colM0);
            macroRow.ColumnDefinitions().Append(colM1);

            auto macroBtns = StackPanel();
            macroBtns.Orientation(Orientation::Horizontal);
            macroBtns.Spacing(4);

            auto lblMac = TextBlock(); lblMac.Text(L"Macros:"); lblMac.FontSize(11); lblMac.Foreground(SolidColorBrush(Color{ 180, 200, 200, 200 })); lblMac.VerticalAlignment(VerticalAlignment::Center);
            macroBtns.Children().Append(lblMac);

            m_btnEnter = Button(); m_btnEnter.Content(box_value(L"{ENTER}")); m_btnEnter.FontSize(10); m_btnEnter.Padding(Thickness{ 5, 2, 5, 2 });
            m_btnEnter.Click([this](auto&&, auto&&) { InsertMacroToken(L"{ENTER}"); });
            macroBtns.Children().Append(m_btnEnter);

            m_btnTab = Button(); m_btnTab.Content(box_value(L"{TAB}")); m_btnTab.FontSize(10); m_btnTab.Padding(Thickness{ 5, 2, 5, 2 });
            m_btnTab.Click([this](auto&&, auto&&) { InsertMacroToken(L"{TAB}"); });
            macroBtns.Children().Append(m_btnTab);

            m_btnDelay = Button(); m_btnDelay.Content(box_value(L"{DELAY}")); m_btnDelay.FontSize(10); m_btnDelay.Padding(Thickness{ 5, 2, 5, 2 });
            m_btnDelay.Click([this](auto&&, auto&&) { InsertMacroToken(L"{DELAY 500}"); });
            macroBtns.Children().Append(m_btnDelay);

            Grid::SetColumn(macroBtns, 0);

            auto utilBtns = StackPanel();
            utilBtns.Orientation(Orientation::Horizontal);
            utilBtns.Spacing(4);

            m_clipboardBtn = Button();
            m_clipboardBtn.Content(box_value(L"Clipboard"));
            m_clipboardBtn.FontSize(10);
            m_clipboardBtn.Padding(Thickness{ 6, 2, 6, 2 });
            m_clipboardBtn.Click({ this, &WidgetView::OnClipboardClicked });

            m_clearBtn = Button();
            m_clearBtn.Content(box_value(L"Clear"));
            m_clearBtn.FontSize(10);
            m_clearBtn.Padding(Thickness{ 6, 2, 6, 2 });
            m_clearBtn.Click({ this, &WidgetView::OnClearClicked });

            utilBtns.Children().Append(m_clipboardBtn);
            utilBtns.Children().Append(m_clearBtn);
            Grid::SetColumn(utilBtns, 1);

            macroRow.Children().Append(macroBtns);
            macroRow.Children().Append(utilBtns);
            stack.Children().Append(macroRow);

            // -------------------------------------------------------------
            // Row 5: Content Text Area
            // -------------------------------------------------------------
            m_inputTextBox = TextBox();
            m_inputTextBox.AcceptsReturn(true);
            m_inputTextBox.TextWrapping(TextWrapping::Wrap);
            m_inputTextBox.Height(130);
            m_inputTextBox.FontSize(12);
            m_inputTextBox.FontFamily(Media::FontFamily(L"Consolas, Cascadia Code, Courier New"));
            m_inputTextBox.PlaceholderText(L"Snippet content / code to type...");
            stack.Children().Append(m_inputTextBox);

            // -------------------------------------------------------------
            // Row 6: Save Snippet Button (Full Width, Fluent Primary Accent)
            // -------------------------------------------------------------
            m_saveBtn = Button();
            m_saveBtn.Content(box_value(L"Save Snippet"));
            m_saveBtn.HorizontalAlignment(HorizontalAlignment::Stretch);
            m_saveBtn.FontSize(12);
            m_saveBtn.FontWeight(Windows::UI::Text::FontWeights::SemiBold());
            m_saveBtn.Background(SolidColorBrush(Color{ 255, 0, 120, 215 })); // Fluent Blue
            m_saveBtn.Foreground(SolidColorBrush(Colors::White()));
            m_saveBtn.Padding(Thickness{ 12, 6, 12, 6 });
            m_saveBtn.Margin(Thickness{ 0, 4, 0, 2 });
            m_saveBtn.Click({ this, &WidgetView::OnSaveClicked });
            stack.Children().Append(m_saveBtn);

            // -------------------------------------------------------------
            // Row 7: Active Typing Session Controls (Pause & STOP, visible when typing)
            // -------------------------------------------------------------
            m_typingControlGrid = Grid();
            auto colTG0 = ColumnDefinition(); colTG0.Width(GridLength{ 1, GridUnitType::Star });
            auto colTG1 = ColumnDefinition(); colTG1.Width(GridLength{ 1, GridUnitType::Star });
            m_typingControlGrid.ColumnDefinitions().Append(colTG0);
            m_typingControlGrid.ColumnDefinitions().Append(colTG1);
            m_typingControlGrid.Visibility(Visibility::Collapsed);
            m_typingControlGrid.Margin(Thickness{ 0, 2, 0, 2 });

            m_pauseBtn = Button();
            m_pauseBtn.Content(box_value(L"Pause"));
            m_pauseBtn.FontSize(11);
            m_pauseBtn.HorizontalAlignment(HorizontalAlignment::Stretch);
            m_pauseBtn.Margin(Thickness{ 0, 0, 3, 0 });
            m_pauseBtn.Click({ this, &WidgetView::OnPauseClicked });
            Grid::SetColumn(m_pauseBtn, 0);

            m_stopBtn = Button();
            m_stopBtn.Content(box_value(L"STOP"));
            m_stopBtn.FontWeight(Windows::UI::Text::FontWeights::Bold());
            m_stopBtn.FontSize(11);
            m_stopBtn.HorizontalAlignment(HorizontalAlignment::Stretch);
            m_stopBtn.Background(SolidColorBrush(Color{ 255, 198, 40, 40 })); // Red
            m_stopBtn.Foreground(SolidColorBrush(Colors::White()));
            m_stopBtn.Margin(Thickness{ 3, 0, 0, 0 });
            m_stopBtn.Click({ this, &WidgetView::OnStopClicked });
            Grid::SetColumn(m_stopBtn, 1);

            m_typingControlGrid.Children().Append(m_pauseBtn);
            m_typingControlGrid.Children().Append(m_stopBtn);
            stack.Children().Append(m_typingControlGrid);

            // -------------------------------------------------------------
            // Row 8: Progress Bar & Status Text
            // -------------------------------------------------------------
            m_progressBar = ProgressBar();
            m_progressBar.Minimum(0);
            m_progressBar.Maximum(100);
            m_progressBar.Value(0);
            m_progressBar.Height(3);
            stack.Children().Append(m_progressBar);

            m_progressText = TextBlock();
            m_progressText.Text(L"Ready. Trigger your hotkey to type anywhere.");
            m_progressText.FontSize(11);
            m_progressText.Foreground(SolidColorBrush(Color{ 180, 200, 200, 200 }));
            stack.Children().Append(m_progressText);

            scroll.Content(stack);
            m_rootGrid.Children().Append(scroll);
            WIDGET_LOG(L"[WidgetView::BuildUi] Successfully constructed root visual tree.");
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[WidgetView::BuildUi] HRESULT Error: " + std::wstring(hr.message().c_str()));
        }
        catch (std::exception const& ex)
        {
            WIDGET_LOG(std::wstring(L"[WidgetView::BuildUi] Exception: ") + Utf8ToWide(ex.what()).c_str());
        }
    }

    void WidgetView::SetupGameBarEvents()
    {
        if (!m_widget) return;

        UpdateThemeAndOpacity();

        try
        {
            m_widget.RequestedOpacityChanged([this](auto&&, auto&&) {
                try
                {
                    m_rootGrid.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this]() {
                        UpdateThemeAndOpacity();
                    });
                }
                catch (...) {}
            });
        }
        catch (...) {}

        try
        {
            m_widget.RequestedThemeChanged([this](auto&&, auto&&) {
                try
                {
                    m_rootGrid.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this]() {
                        UpdateThemeAndOpacity();
                    });
                }
                catch (...) {}
            });
        }
        catch (...) {}

        try
        {
            m_widget.PinnedChanged([this](auto&&, auto&&) {
                try
                {
                    m_rootGrid.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this]() {
                        if (m_widget.Pinned())
                        {
                            m_progressText.Text(L"Pinned. Focus your target window to type.");
                        }
                    });
                }
                catch (...) {}
            });
        }
        catch (...) {}
    }

    void WidgetView::UpdateThemeAndOpacity()
    {
        if (!m_widget) return;

        try
        {
            auto theme = m_widget.RequestedTheme();
            m_rootGrid.RequestedTheme(theme);
        }
        catch (...) {}
    }

    void WidgetView::SetupIpc()
    {
        WIDGET_LOG(L"[WidgetView::SetupIpc] Initializing IPC client");
        m_ipcClient = std::make_unique<IpcClient>();

        m_ipcClient->SetConnectionCallback([this](bool connected) {
            WIDGET_LOG(L"[WidgetView] Connection changed: " + std::to_wstring(connected));
            if (m_dispatcher)
            {
                try
                {
                    m_dispatcher.RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this, connected]() {
                        UpdateConnectionState(connected);
                    });
                }
                catch (...) {}
            }
        });

        m_ipcClient->SetMessageCallback([this](const std::string& line) {
            WIDGET_LOG(L"[WidgetView] Message received: " + Utf8ToWide(line));
            if (m_dispatcher)
            {
                try
                {
                    m_dispatcher.RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this, line]() {
                        HandleIpcMessage(line);
                    });
                }
                catch (...) {}
            }
        });

        m_ipcClient->StartBackgroundListener();
    }

    void WidgetView::UpdateConnectionState(bool isConnected)
    {
        WIDGET_LOG(L"[WidgetView::UpdateConnectionState] isConnected=" + std::to_wstring(isConnected));
        try
        {
            if (isConnected)
            {
                m_statusDot.Fill(SolidColorBrush(Color{ 255, 76, 175, 80 })); // Green
                m_statusText.Text(L"Connected");
                m_connectBtn.Visibility(Visibility::Collapsed);
                m_progressText.Text(L"Connected to Auto-Typer byGo. Ready.");

                // Request snippets immediately
                m_ipcClient->SendCommand("{\"command\":\"GET_SNIPPETS\"}");
            }
            else
            {
                m_statusDot.Fill(SolidColorBrush(Color{ 255, 239, 83, 80 })); // Red
                m_statusText.Text(L"Disconnected");
                m_connectBtn.Visibility(Visibility::Visible);
                m_connectBtn.Content(box_value(L"Launch App"));
                m_progressText.Text(L"Auto-Typer is starting... Connecting automatically.");

                // Auto-launch the desktop app in the background
                IpcClient::LaunchAutoTyperApp();
            }
        }
        catch (...) {}
    }

    void WidgetView::HandleIpcMessage(const std::string& rawJson)
    {
        WIDGET_LOG(L"[WidgetView::HandleIpcMessage] Processing message");
        try
        {
            std::string cleanJson = rawJson;
            // Strip UTF-8 BOM if present
            if (cleanJson.size() >= 3 &&
                static_cast<unsigned char>(cleanJson[0]) == 0xEF &&
                static_cast<unsigned char>(cleanJson[1]) == 0xBB &&
                static_cast<unsigned char>(cleanJson[2]) == 0xBF)
            {
                cleanJson = cleanJson.substr(3);
            }
            // Strip any leading non-JSON bytes until '{'
            size_t jsonStart = cleanJson.find('{');
            if (jsonStart != std::string::npos && jsonStart > 0)
            {
                cleanJson = cleanJson.substr(jsonStart);
            }

            JsonObject root;
            if (!JsonObject::TryParse(winrt::hstring(Utf8ToWide(cleanJson)), root))
            {
                WIDGET_LOG(L"[WidgetView::HandleIpcMessage] JsonObject::TryParse returned false for: " + Utf8ToWide(cleanJson));
                return;
            }

            std::wstring type = SafeJsonString(root, L"type");
            WIDGET_LOG(L"[WidgetView::HandleIpcMessage] Handling type=" + type);

            if (type == L"STATUS")
            {
                if (root.HasKey(L"snippets"))
                {
                    ParseSnippetsJson(cleanJson);
                }

                if (root.HasKey(L"state"))
                {
                    std::wstring state = SafeJsonString(root, L"state");
                    if (!state.empty()) m_progressText.Text(state);
                }

                if (root.HasKey(L"isPaused"))
                {
                    m_isPaused = SafeJsonBool(root, L"isPaused");
                    m_pauseBtn.Content(box_value(m_isPaused ? L"Resume" : L"Pause"));
                }
            }
            else if (type == L"STATE_CHANGED")
            {
                std::wstring state = SafeJsonString(root, L"state");

                if (state == L"Countdown")
                {
                    int cd = SafeJsonInt(root, L"countdown");
                    m_progressText.Text(L"Starting in " + to_hstring(cd) + L"s... Focus target input!");
                    m_isTyping = true;
                    if (m_typingControlGrid) m_typingControlGrid.Visibility(Visibility::Visible);
                }
                else if (state == L"Typing")
                {
                    m_isTyping = true;
                    if (m_typingControlGrid) m_typingControlGrid.Visibility(Visibility::Visible);

                    int progress = SafeJsonInt(root, L"progress");
                    int total = SafeJsonInt(root, L"total", 1);

                    m_progressBar.Maximum(total > 0 ? total : 100);
                    m_progressBar.Value(progress);

                    std::wstring activeSnip = SafeJsonString(root, L"activeSnippet");
                    m_progressText.Text(L"Typing " + activeSnip + L"... " + to_hstring(progress) + L"/" + to_hstring(total));
                }
                else if (state == L"Ready" || state == L"Stopped")
                {
                    m_isTyping = false;
                    if (m_typingControlGrid) m_typingControlGrid.Visibility(Visibility::Collapsed);
                    m_progressBar.Value(0);

                    std::wstring msg = SafeJsonString(root, L"message", L"Ready. Trigger hotkey to type.");
                    m_progressText.Text(msg);
                }
            }
            else if (type == L"COUNTDOWN")
            {
                int count = SafeJsonInt(root, L"countdown");
                m_progressText.Text(L"Typing begins in " + to_hstring(count) + L"s... Click your target window!");
            }
            else if (type == L"SAVE_RESULT")
            {
                m_progressText.Text(L"Snippet saved to Auto-Typer byGo!");
                std::wstring activeId = SafeJsonString(root, L"activeSnippet");
                if (!activeId.empty())
                {
                    m_currentSnippetId = activeId;
                }

                if (root.HasKey(L"snippets"))
                {
                    ParseSnippetsJson(cleanJson);
                }
                WIDGET_LOG(L"[WidgetView::HandleIpcMessage] SAVE_RESULT handled successfully, activeId=" + m_currentSnippetId);
            }
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[WidgetView::HandleIpcMessage] HRESULT Exception: " + std::wstring(hr.message().c_str()));
        }
        catch (std::exception const& ex)
        {
            WIDGET_LOG(L"[WidgetView::HandleIpcMessage] Exception: " + Utf8ToWide(ex.what()));
        }
        catch (...)
        {
            WIDGET_LOG(L"[WidgetView::HandleIpcMessage] Unknown exception");
        }
    }

    void WidgetView::ParseSnippetsJson(const std::string& rawJson)
    {
        try
        {
            JsonObject root;
            if (!JsonObject::TryParse(winrt::hstring(Utf8ToWide(rawJson)), root)) return;
            if (!root.HasKey(L"snippets")) return;

            auto val = root.GetNamedValue(L"snippets");
            if (val.ValueType() != JsonValueType::Array) return;

            auto array = val.GetArray();
            std::vector<SnippetItem> newSnippets;

            for (uint32_t i = 0; i < array.Size(); i++)
            {
                auto itemVal = array.GetAt(i);
                if (itemVal.ValueType() != JsonValueType::Object) continue;
                auto obj = itemVal.GetObject();
                SnippetItem item;
                item.id = SafeJsonString(obj, L"id");
                item.name = SafeJsonString(obj, L"name", L"New Snippet");
                item.text = SafeJsonString(obj, L"text");
                item.mode = SafeJsonString(obj, L"mode", L"HumanLike");
                item.delayPerChar = SafeJsonInt(obj, L"delayPerChar", 1);
                item.delayPerWord = SafeJsonInt(obj, L"delayPerWord", 1);
                item.hotkey = SafeJsonString(obj, L"hotkey");
                newSnippets.push_back(item);
            }

            std::wstring activeId = SafeJsonString(root, L"activeSnippet");
            if (!activeId.empty())
            {
                m_currentSnippetId = activeId;
            }

            PopulateSnippets(newSnippets, m_currentSnippetId);
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[WidgetView::ParseSnippetsJson] Exception: " + std::wstring(hr.message().c_str()));
        }
        catch (...)
        {
            WIDGET_LOG(L"[WidgetView::ParseSnippetsJson] Unknown exception");
        }
    }

    void WidgetView::PopulateSnippets(const std::vector<SnippetItem>& snippets, const std::wstring& targetId)
    {
        try
        {
            m_isUpdatingCombo = true;
            m_snippets = snippets;

            // Unhook event handler to prevent spurious SelectionChanged during rebuild
            if (m_snippetSelectionToken.value != 0)
            {
                m_snippetCombo.SelectionChanged(m_snippetSelectionToken);
                m_snippetSelectionToken.value = 0;
            }

            m_snippetCombo.Items().Clear();
            m_snippetCombo.Items().Append(box_value(L"[ + Custom Text ]"));

            int targetIndex = 0;
            for (size_t i = 0; i < m_snippets.size(); i++)
            {
                const auto& s = m_snippets[i];
                std::wstring label = s.name;
                if (!s.hotkey.empty())
                {
                    label += L" (" + s.hotkey + L")";
                }
                m_snippetCombo.Items().Append(box_value(label));

                if (!targetId.empty() && s.id == targetId)
                {
                    targetIndex = static_cast<int>(i + 1);
                }
            }

            if (targetIndex > 0 && targetIndex < static_cast<int>(m_snippetCombo.Items().Size()))
            {
                m_snippetCombo.SelectedIndex(targetIndex);
                PopulateSnippetDetails(targetIndex);
            }
            else if (!m_snippets.empty())
            {
                m_snippetCombo.SelectedIndex(1);
                PopulateSnippetDetails(1);
            }
            else
            {
                m_snippetCombo.SelectedIndex(0);
                PopulateSnippetDetails(0);
            }

            // Rehook event handler
            m_snippetSelectionToken = m_snippetCombo.SelectionChanged({ this, &WidgetView::OnSnippetSelected });
            m_isUpdatingCombo = false;
            WIDGET_LOG(L"[WidgetView::PopulateSnippets] Snippets refreshed, count=" + std::to_wstring(m_snippets.size()) + L", selectedIndex=" + std::to_wstring(m_snippetCombo.SelectedIndex()));
        }
        catch (winrt::hresult_error const& hr)
        {
            m_isUpdatingCombo = false;
            WIDGET_LOG(L"[WidgetView::PopulateSnippets] Exception: " + std::wstring(hr.message().c_str()));
        }
        catch (...)
        {
            m_isUpdatingCombo = false;
            WIDGET_LOG(L"[WidgetView::PopulateSnippets] Unknown exception");
        }
    }

    void WidgetView::PopulateSnippetDetails(int index)
    {
        try
        {
            if (index < 0) return; // Ignore transient invalid selection from XAML layout

            if (index == 0 || index > static_cast<int>(m_snippets.size()))
            {
                // Custom text mode
                m_currentSnippetId = L"";
                m_nameBox.Text(L"");
                m_nameBox.IsEnabled(true);
                m_nameBox.PlaceholderText(L"New Snippet");
                m_hotkeyBox.Text(L"");
                m_modeCombo.SelectedIndex(0); // HumanLike
                m_delayCharBox.Text(L"1");
                m_delayWordBox.Text(L"1");
                m_saveBtn.Content(box_value(L"Save as New Snippet"));
                return;
            }

            const auto& s = m_snippets[index - 1];
            m_currentSnippetId = s.id;
            m_nameBox.Text(s.name);
            m_nameBox.IsEnabled(true);
            m_hotkeyBox.Text(s.hotkey);

            // Select mode
            if (s.mode == L"Paste") m_modeCombo.SelectedIndex(1);
            else if (s.mode == L"Fast") m_modeCombo.SelectedIndex(2);
            else if (s.mode == L"Macro") m_modeCombo.SelectedIndex(3);
            else m_modeCombo.SelectedIndex(0); // HumanLike

            m_delayCharBox.Text(to_hstring(s.delayPerChar));
            m_delayWordBox.Text(to_hstring(s.delayPerWord));
            m_inputTextBox.Text(s.text);

            m_saveBtn.Content(box_value(L"Save Snippet"));
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[WidgetView::PopulateSnippetDetails] Exception: " + std::wstring(hr.message().c_str()));
        }
        catch (...) {}
    }

    void WidgetView::OnSnippetSelected(winrt::Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&)
    {
        if (m_isUpdatingCombo) return;
        int index = m_snippetCombo.SelectedIndex();
        if (index < 0) return; // Guard against intermediate invalid selection
        PopulateSnippetDetails(index);
    }

    void WidgetView::OnSaveClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        WIDGET_LOG(L"[WidgetView::OnSaveClicked] Invoked");
        try
        {
            if (!m_ipcClient || !m_ipcClient->IsConnected())
            {
                m_progressText.Text(L"Cannot save: Not connected to Auto-Typer byGo.");
                return;
            }

            std::wstring name = m_nameBox.Text().c_str();
            if (name.empty()) name = L"New Snippet";
            std::wstring hotkey = m_hotkeyBox.Text().c_str();
            std::wstring text = m_inputTextBox.Text().c_str();

            std::wstring mode = L"HumanLike";
            int modeIdx = m_modeCombo.SelectedIndex();
            if (modeIdx == 1) mode = L"Paste";
            else if (modeIdx == 2) mode = L"Fast";
            else if (modeIdx == 3) mode = L"Macro";

            int delayChar = 1;
            int delayWord = 1;
            try { delayChar = std::stoi(m_delayCharBox.Text().c_str()); } catch (...) {}
            try { delayWord = std::stoi(m_delayWordBox.Text().c_str()); } catch (...) {}

            JsonObject cmd;
            cmd.SetNamedValue(L"command", JsonValue::CreateStringValue(L"SAVE_SNIPPET"));
            if (!m_currentSnippetId.empty())
            {
                cmd.SetNamedValue(L"snippetId", JsonValue::CreateStringValue(m_currentSnippetId));
            }
            cmd.SetNamedValue(L"name", JsonValue::CreateStringValue(name));
            cmd.SetNamedValue(L"hotkey", JsonValue::CreateStringValue(hotkey));
            cmd.SetNamedValue(L"mode", JsonValue::CreateStringValue(mode));
            cmd.SetNamedValue(L"delayPerChar", JsonValue::CreateNumberValue(delayChar));
            cmd.SetNamedValue(L"delayPerWord", JsonValue::CreateNumberValue(delayWord));
            cmd.SetNamedValue(L"text", JsonValue::CreateStringValue(text));

            std::string jsonStr = WideToUtf8(std::wstring(cmd.Stringify().c_str()));
            m_ipcClient->SendCommand(jsonStr);

            m_progressText.Text(L"Snippet saved to Auto-Typer byGo!");
            WIDGET_LOG(L"[WidgetView::OnSaveClicked] Save command dispatched successfully.");
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[WidgetView::OnSaveClicked] HRESULT Error: " + std::wstring(hr.message().c_str()));
        }
        catch (std::exception const& ex)
        {
            WIDGET_LOG(std::wstring(L"[WidgetView::OnSaveClicked] Exception: ") + Utf8ToWide(ex.what()).c_str());
        }
        catch (...)
        {
            WIDGET_LOG(L"[WidgetView::OnSaveClicked] Unknown exception");
        }
    }

    void WidgetView::OnAddClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        WIDGET_LOG(L"[WidgetView::OnAddClicked] Invoked");
        try
        {
            if (!m_ipcClient || !m_ipcClient->IsConnected())
            {
                m_progressText.Text(L"Cannot add: Not connected to Auto-Typer byGo.");
                return;
            }

            JsonObject cmd;
            cmd.SetNamedValue(L"command", JsonValue::CreateStringValue(L"ADD_SNIPPET"));
            cmd.SetNamedValue(L"name", JsonValue::CreateStringValue(L"New Snippet"));
            cmd.SetNamedValue(L"text", JsonValue::CreateStringValue(L""));
            cmd.SetNamedValue(L"mode", JsonValue::CreateStringValue(L"HumanLike"));
            cmd.SetNamedValue(L"delayPerChar", JsonValue::CreateNumberValue(1));
            cmd.SetNamedValue(L"delayPerWord", JsonValue::CreateNumberValue(1));

            std::string jsonStr = WideToUtf8(std::wstring(cmd.Stringify().c_str()));
            m_ipcClient->SendCommand(jsonStr);

            m_progressText.Text(L"Created new snippet in Auto-Typer byGo.");
        }
        catch (...) {}
    }

    void WidgetView::OnDeleteClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        WIDGET_LOG(L"[WidgetView::OnDeleteClicked] Invoked");
        try
        {
            if (!m_ipcClient || !m_ipcClient->IsConnected() || m_currentSnippetId.empty())
            {
                return;
            }

            JsonObject cmd;
            cmd.SetNamedValue(L"command", JsonValue::CreateStringValue(L"DELETE_SNIPPET"));
            cmd.SetNamedValue(L"snippetId", JsonValue::CreateStringValue(m_currentSnippetId));

            std::string jsonStr = WideToUtf8(std::wstring(cmd.Stringify().c_str()));
            m_ipcClient->SendCommand(jsonStr);

            m_progressText.Text(L"Snippet deleted.");
        }
        catch (...) {}
    }

    void WidgetView::InsertMacroToken(const std::wstring& token)
    {
        try
        {
            std::wstring current = m_inputTextBox.Text().c_str();
            current += token;
            m_inputTextBox.Text(current);
        }
        catch (...) {}
    }

    void WidgetView::OnPauseClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        try
        {
            if (!m_ipcClient || !m_ipcClient->IsConnected()) return;

            JsonObject cmd;
            cmd.SetNamedValue(L"command", JsonValue::CreateStringValue(m_isPaused ? L"RESUME" : L"PAUSE"));
            m_ipcClient->SendCommand(WideToUtf8(std::wstring(cmd.Stringify().c_str())));
        }
        catch (...) {}
    }

    void WidgetView::OnStopClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        try
        {
            if (!m_ipcClient || !m_ipcClient->IsConnected()) return;

            JsonObject cmd;
            cmd.SetNamedValue(L"command", JsonValue::CreateStringValue(L"STOP"));
            m_ipcClient->SendCommand(WideToUtf8(std::wstring(cmd.Stringify().c_str())));
        }
        catch (...) {}
    }

    void WidgetView::OnClipboardClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        try
        {
            auto package = Clipboard::GetContent();
            if (package && package.Contains(StandardDataFormats::Text()))
            {
                package.GetTextAsync().Completed([this](auto&& info, auto&&) {
                    try
                    {
                        hstring text = info.GetResults();
                        m_rootGrid.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this, text]() {
                            m_inputTextBox.Text(text);
                            m_progressText.Text(L"Clipboard text pasted!");
                        });
                    }
                    catch (...) {}
                });
            }
            else
            {
                m_progressText.Text(L"Clipboard does not contain text.");
            }
        }
        catch (...) {}
    }

    void WidgetView::OnClearClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        try
        {
            m_inputTextBox.Text(L"");
        }
        catch (...) {}
    }

    void WidgetView::OnConnectClicked(winrt::Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
    {
        WIDGET_LOG(L"[WidgetView::OnConnectClicked] Invoked");
        try
        {
            IpcClient::LaunchAutoTyperApp();
            if (m_ipcClient->Connect())
            {
                UpdateConnectionState(true);
            }
            else
            {
                m_progressText.Text(L"Starting Auto-Typer byGo... Waiting for connection.");
            }
        }
        catch (...) {}
    }
}
