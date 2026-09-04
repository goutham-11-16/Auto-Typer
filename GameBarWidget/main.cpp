#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#undef GetCurrentTime
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.ApplicationModel.Activation.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include "winrt/Microsoft.Gaming.XboxGameBar.h"
#include "Logger.h"
#include "WidgetView.h"
#include <memory>
#include <string>

using namespace winrt;
using namespace Windows::ApplicationModel;
using namespace Windows::ApplicationModel::Activation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Microsoft::Gaming::XboxGameBar;
using namespace AutoTyperWidget;

struct App : ApplicationT<App>
{
    XboxGameBarWidget m_widget{ nullptr };
    std::unique_ptr<WidgetView> m_view;

    void OnActivated(IActivatedEventArgs const& e)
    {
        WIDGET_LOG(L"[App::OnActivated] Start, Kind=" + std::to_wstring(static_cast<int>(e.Kind())));
        try
        {
            if (e.Kind() == ActivationKind::Protocol)
            {
                auto proto = e.try_as<IProtocolActivatedEventArgs>();
                if (proto && proto.Uri().SchemeName() == L"ms-gamebarwidget")
                {
                    WIDGET_LOG(L"[App::OnActivated] Protocol ms-gamebarwidget, Uri=" + std::wstring(proto.Uri().RawUri().c_str()));
                    auto widgetArgs = e.try_as<XboxGameBarWidgetActivatedEventArgs>();
                    if (widgetArgs)
                    {
                        WIDGET_LOG(L"[App::OnActivated] IsLaunchActivation=" + std::to_wstring(widgetArgs.IsLaunchActivation()) + 
                                 L", AppExtensionId=" + std::wstring(widgetArgs.AppExtensionId().c_str()));

                        auto rootFrame = Window::Current().Content().try_as<Frame>();
                        if (!rootFrame)
                        {
                            rootFrame = Frame();
                            Window::Current().Content(rootFrame);
                        }

                        m_widget = XboxGameBarWidget(widgetArgs, Window::Current().CoreWindow(), rootFrame);

                        if (!m_view)
                        {
                            m_view = std::make_unique<WidgetView>(m_widget);
                            auto page = Page();
                            page.Content(m_view->GetRootElement());
                            rootFrame.Content(page);
                        }
                        else
                        {
                            m_view->SetWidget(m_widget);
                            if (!rootFrame.Content())
                            {
                                auto page = Page();
                                page.Content(m_view->GetRootElement());
                                rootFrame.Content(page);
                            }
                        }

                        Window::Current().Activate();
                        WIDGET_LOG(L"[App::OnActivated] Activation handled successfully.");
                    }
                    else
                    {
                        WIDGET_LOG(L"[App::OnActivated] Failed to cast to XboxGameBarWidgetActivatedEventArgs");
                    }
                }
            }
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[App::OnActivated] HRESULT Exception: 0x" + std::to_wstring(hr.code().value) + L" - " + std::wstring(hr.message().c_str()));
        }
        catch (std::exception const& ex)
        {
            WIDGET_LOG(std::wstring(L"[App::OnActivated] std::exception: ") + Utf8ToWide(ex.what()).c_str());
        }
        catch (...)
        {
            WIDGET_LOG(L"[App::OnActivated] Unknown exception caught!");
        }
    }

    void OnLaunched(LaunchActivatedEventArgs const&)
    {
        WIDGET_LOG(L"[App::OnLaunched] Start");
        try
        {
            auto rootFrame = Window::Current().Content().try_as<Frame>();
            if (!rootFrame)
            {
                rootFrame = Frame();
                Window::Current().Content(rootFrame);
            }

            m_view = std::make_unique<WidgetView>(nullptr);
            auto page = Page();
            page.Content(m_view->GetRootElement());
            rootFrame.Content(page);

            Window::Current().Activate();
            WIDGET_LOG(L"[App::OnLaunched] Completed");
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[App::OnLaunched] Exception: " + std::wstring(hr.message().c_str()));
        }
        catch (...)
        {
            WIDGET_LOG(L"[App::OnLaunched] Unknown exception");
        }
    }

    void OnSuspending(IInspectable const&, SuspendingEventArgs const& e)
    {
        WIDGET_LOG(L"[App::OnSuspending]");
        try
        {
            auto deferral = e.SuspendingOperation().GetDeferral();
            deferral.Complete();
        }
        catch (...) {}
    }
};

int __stdcall wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    try
    {
        HMODULE hDll = LoadLibraryW(L"Microsoft.Gaming.XboxGameBar.dll");
        if (hDll)
        {
            WIDGET_LOG(L"[wWinMain] Loaded Microsoft.Gaming.XboxGameBar.dll successfully!");
        }
        else
        {
            WIDGET_LOG(L"[wWinMain] LoadLibraryW Microsoft.Gaming.XboxGameBar.dll failed: " + std::to_wstring(GetLastError()));
        }

        init_apartment(apartment_type::multi_threaded);
        Application::Start([](auto&&) { make<App>(); });
        return 0;
    }
    catch (winrt::hresult_error const& hr)
    {
        WIDGET_LOG(L"[wWinMain] Exception: " + std::wstring(hr.message().c_str()));
        return 1;
    }
    catch (...)
    {
        WIDGET_LOG(L"[wWinMain] Unknown exception");
        return 1;
    }
}
