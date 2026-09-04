#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <unknwn.h>
#include <winstring.h>
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
            auto rootFrame = Window::Current().Content().try_as<Frame>();
            if (!rootFrame)
            {
                rootFrame = Frame();
                Window::Current().Content(rootFrame);
            }

            if (e.Kind() == ActivationKind::Protocol)
            {
                auto proto = e.try_as<IProtocolActivatedEventArgs>();
                if (proto && proto.Uri().SchemeName() == L"ms-gamebarwidget")
                {
                    WIDGET_LOG(L"[App::OnActivated] Protocol ms-gamebarwidget, Uri=" + std::wstring(proto.Uri().RawUri().c_str()));
                    XboxGameBarWidgetActivatedEventArgs widgetArgs{ nullptr };
                    try
                    {
                        widgetArgs = e.try_as<XboxGameBarWidgetActivatedEventArgs>();
                    }
                    catch (...)
                    {
                        WIDGET_LOG(L"[App::OnActivated] Exception querying XboxGameBarWidgetActivatedEventArgs");
                    }

                    if (widgetArgs)
                    {
                        WIDGET_LOG(L"[App::OnActivated] IsLaunchActivation=" + std::to_wstring(widgetArgs.IsLaunchActivation()) + 
                                 L", AppExtensionId=" + std::wstring(widgetArgs.AppExtensionId().c_str()));

                        if (widgetArgs.IsLaunchActivation())
                        {
                            try
                            {
                                m_widget = XboxGameBarWidget(widgetArgs, Window::Current().CoreWindow(), rootFrame);
                            }
                            catch (winrt::hresult_error const& hr)
                            {
                                WIDGET_LOG(L"[App::OnActivated] XboxGameBarWidget constructor failed: 0x" + std::to_wstring(hr.code().value) + L" - " + std::wstring(hr.message().c_str()));
                                m_widget = nullptr;
                            }
                            catch (...)
                            {
                                WIDGET_LOG(L"[App::OnActivated] XboxGameBarWidget constructor unknown exception");
                                m_widget = nullptr;
                            }

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
                            WIDGET_LOG(L"[App::OnActivated] Initial launch activation handled successfully.");
                        }
                        else
                        {
                            if (m_view && m_widget)
                            {
                                m_view->SetWidget(m_widget);
                            }
                            Window::Current().Activate();
                            WIDGET_LOG(L"[App::OnActivated] Subsequent activation handled.");
                        }
                    }
                    else
                    {
                        WIDGET_LOG(L"[App::OnActivated] widgetArgs null - performing fallback view initialization");
                        if (!m_view)
                        {
                            m_view = std::make_unique<WidgetView>(nullptr);
                            auto page = Page();
                            page.Content(m_view->GetRootElement());
                            rootFrame.Content(page);
                        }
                        Window::Current().Activate();
                    }
                }
                else
                {
                    if (!m_view)
                    {
                        m_view = std::make_unique<WidgetView>(nullptr);
                        auto page = Page();
                        page.Content(m_view->GetRootElement());
                        rootFrame.Content(page);
                    }
                    Window::Current().Activate();
                }
            }
            else
            {
                if (!m_view)
                {
                    m_view = std::make_unique<WidgetView>(nullptr);
                    auto page = Page();
                    page.Content(m_view->GetRootElement());
                    rootFrame.Content(page);
                }
                Window::Current().Activate();
            }
        }
        catch (winrt::hresult_error const& hr)
        {
            WIDGET_LOG(L"[App::OnActivated] HRESULT Exception: 0x" + std::to_wstring(hr.code().value) + L" - " + std::wstring(hr.message().c_str()));
            try
            {
                auto rootFrame = Window::Current().Content().try_as<Frame>();
                if (!rootFrame)
                {
                    rootFrame = Frame();
                    Window::Current().Content(rootFrame);
                }
                if (!m_view)
                {
                    m_view = std::make_unique<WidgetView>(nullptr);
                    auto page = Page();
                    page.Content(m_view->GetRootElement());
                    rootFrame.Content(page);
                }
                Window::Current().Activate();
            }
            catch (...) {}
        }
        catch (std::exception const& ex)
        {
            WIDGET_LOG(std::wstring(L"[App::OnActivated] std::exception: ") + Utf8ToWide(ex.what()).c_str());
            try { Window::Current().Activate(); } catch (...) {}
        }
        catch (...)
        {
            WIDGET_LOG(L"[App::OnActivated] Unknown exception caught!");
            try { Window::Current().Activate(); } catch (...) {}
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

static HMODULE g_hGameBarDll = nullptr;
static int32_t(__stdcall* g_pfnDllGetActivationFactory)(void*, void**) = nullptr;

int32_t __stdcall CustomActivationHandler(void* classId, winrt::guid const& iid, void** factory) noexcept
{
    if (!factory) return E_POINTER;
    *factory = nullptr;

    try
    {
        HSTRING hstr = reinterpret_cast<HSTRING>(classId);
        UINT32 len = 0;
        PCWSTR nameBuf = WindowsGetStringRawBuffer(hstr, &len);
        std::wstring className = (nameBuf && len > 0) ? std::wstring(nameBuf, len) : L"";

        // If it is a Game Bar SDK class, route directly to Microsoft.Gaming.XboxGameBar.dll
        if (!className.empty() && className.find(L"Microsoft.Gaming.XboxGameBar") != std::wstring::npos)
        {
            if (!g_hGameBarDll)
            {
                wchar_t exePath[MAX_PATH];
                if (GetModuleFileNameW(NULL, exePath, MAX_PATH) > 0)
                {
                    wchar_t* lastSlash = wcsrchr(exePath, L'\\');
                    if (lastSlash)
                    {
                        *(lastSlash + 1) = L'\0';
                        std::wstring dllPath = std::wstring(exePath) + L"Microsoft.Gaming.XboxGameBar.dll";
                        g_hGameBarDll = LoadLibraryW(dllPath.c_str());
                    }
                }
                if (!g_hGameBarDll)
                {
                    g_hGameBarDll = LoadLibraryW(L"Microsoft.Gaming.XboxGameBar.dll");
                }
            }

            if (g_hGameBarDll && !g_pfnDllGetActivationFactory)
            {
                g_pfnDllGetActivationFactory = reinterpret_cast<int32_t(__stdcall*)(void*, void**)>(
                    GetProcAddress(g_hGameBarDll, "DllGetActivationFactory")
                );
            }

            if (g_pfnDllGetActivationFactory)
            {
                IUnknown* pUnk = nullptr;
                int32_t hr = g_pfnDllGetActivationFactory(classId, reinterpret_cast<void**>(&pUnk));
                if (hr == 0 && pUnk)
                {
                    hr = pUnk->QueryInterface(reinterpret_cast<GUID const&>(iid), factory);
                    pUnk->Release();
                    return hr;
                }
            }
        }

        // Standard Windows system types (Windows.UI.Xaml.*, etc.)
        static int32_t(__stdcall* s_pfnRoGet)(void*, winrt::guid const&, void**) = nullptr;
        if (!s_pfnRoGet)
        {
            HMODULE hComBase = GetModuleHandleW(L"combase.dll");
            if (!hComBase) hComBase = LoadLibraryW(L"combase.dll");
            if (hComBase)
            {
                s_pfnRoGet = reinterpret_cast<int32_t(__stdcall*)(void*, winrt::guid const&, void**)>(
                    GetProcAddress(hComBase, "RoGetActivationFactory")
                );
            }
        }

        if (s_pfnRoGet)
        {
            return s_pfnRoGet(classId, iid, factory);
        }
    }
    catch (...) {}

    return -2147483633; // RO_E_METADATA_NAME_NOT_FOUND (0x8000000F)
}

int __stdcall wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    try
    {
        winrt_activation_handler = CustomActivationHandler;

        HMODULE hDll = LoadLibraryW(L"Microsoft.Gaming.XboxGameBar.dll");
        if (hDll)
        {
            WIDGET_LOG(L"[wWinMain] Loaded Microsoft.Gaming.XboxGameBar.dll successfully!");
        }
        else
        {
            WIDGET_LOG(L"[wWinMain] LoadLibraryW Microsoft.Gaming.XboxGameBar.dll failed: " + std::to_wstring(GetLastError()));
        }

        init_apartment();
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
