#include "Hooks.hpp"
#include "Logger.hpp"

#include <d3d11.h>
#include <dxgi1_2.h>

#include <cstdint>
#include <cstring>
#include <mutex>
#include <sstream>
#include <unordered_set>

extern "C" HRESULT WINAPI CreateDXGIFactory_Proxy(REFIID riid, void** factory);
extern "C" HRESULT WINAPI CreateDXGIFactory1_Proxy(REFIID riid, void** factory);
extern "C" HRESULT WINAPI CreateDXGIFactory2_Proxy(UINT flags, REFIID riid, void** factory);

namespace ts4dx11
{
    namespace
    {
        using D3D11CreateDeviceFn = HRESULT(WINAPI*)(
            IDXGIAdapter*,
            D3D_DRIVER_TYPE,
            HMODULE,
            UINT,
            const D3D_FEATURE_LEVEL*,
            UINT,
            UINT,
            ID3D11Device**,
            D3D_FEATURE_LEVEL*,
            ID3D11DeviceContext**);

        using D3D11CreateDeviceAndSwapChainFn = HRESULT(WINAPI*)(
            IDXGIAdapter*,
            D3D_DRIVER_TYPE,
            HMODULE,
            UINT,
            const D3D_FEATURE_LEVEL*,
            UINT,
            UINT,
            const DXGI_SWAP_CHAIN_DESC*,
            IDXGISwapChain**,
            ID3D11Device**,
            D3D_FEATURE_LEVEL*,
            ID3D11DeviceContext**);

        using CreateDXGIFactoryFn = HRESULT(WINAPI*)(REFIID, void**);
        using CreateDXGIFactory1Fn = HRESULT(WINAPI*)(REFIID, void**);
        using CreateDXGIFactory2Fn = HRESULT(WINAPI*)(UINT, REFIID, void**);
        using FactoryCreateSwapChainFn = HRESULT(STDMETHODCALLTYPE*)(IDXGIFactory*, IUnknown*, DXGI_SWAP_CHAIN_DESC*, IDXGISwapChain**);
        using Factory2CreateSwapChainForHwndFn = HRESULT(STDMETHODCALLTYPE*)(IDXGIFactory2*, IUnknown*, HWND, const DXGI_SWAP_CHAIN_DESC1*, const DXGI_SWAP_CHAIN_FULLSCREEN_DESC*, IDXGIOutput*, IDXGISwapChain1**);

        HMODULE g_realModule = nullptr;
        HMODULE g_realDxgiModule = nullptr;
        D3D11CreateDeviceFn g_realCreateDevice = nullptr;
        D3D11CreateDeviceAndSwapChainFn g_realCreateDeviceAndSwapChain = nullptr;
        CreateDXGIFactoryFn g_realCreateDXGIFactory = nullptr;
        CreateDXGIFactory1Fn g_realCreateDXGIFactory1 = nullptr;
        CreateDXGIFactory2Fn g_realCreateDXGIFactory2 = nullptr;
        FactoryCreateSwapChainFn g_originalFactoryCreateSwapChain = nullptr;
        Factory2CreateSwapChainForHwndFn g_originalFactory2CreateSwapChainForHwnd = nullptr;
        std::once_flag g_loadOnce;
        std::once_flag g_dxgiLoadOnce;
        std::once_flag g_sessionEventOnce;
        std::once_flag g_markerOnce;
        std::once_flag g_dxgiImportPatchOnce;
        std::mutex g_factoryPatchMutex;
        std::unordered_set<void**> g_patchedFactorySlots;

        std::wstring GetSystemD3D11Path()
        {
            wchar_t systemDirectory[MAX_PATH];
            const UINT length = GetSystemDirectoryW(systemDirectory, MAX_PATH);
            std::wstring path(systemDirectory, systemDirectory + length);
            path += L"\\d3d11.dll";
            return path;
        }

        std::wstring GetSystemDxgiPath()
        {
            wchar_t systemDirectory[MAX_PATH];
            const UINT length = GetSystemDirectoryW(systemDirectory, MAX_PATH);
            std::wstring path(systemDirectory, systemDirectory + length);
            path += L"\\dxgi.dll";
            return path;
        }

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

        std::wstring GetGlobalMarkerPath()
        {
            wchar_t buffer[MAX_PATH];
            const DWORD tempLength = GetTempPathW(MAX_PATH, buffer);
            if (tempLength == 0 || tempLength >= MAX_PATH)
            {
                return L"ts4-dx11-introspection-proxy-loads.log";
            }

            std::wstring path(buffer);
            if (!path.empty() && path.back() != L'\\')
            {
                path.push_back(L'\\');
            }

            path += L"ts4-dx11-introspection-proxy-loads.log";
            return path;
        }

        bool AppendUtf8Line(const std::wstring& path, const std::string& line)
        {
            if (path.empty())
            {
                return false;
            }

            const HANDLE handle = CreateFileW(
                path.c_str(),
                FILE_APPEND_DATA,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr,
                OPEN_ALWAYS,
                FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
                nullptr);
            if (handle == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            std::string payload = line;
            payload.push_back('\n');
            DWORD written = 0;
            const BOOL ok = WriteFile(handle, payload.data(), static_cast<DWORD>(payload.size()), &written, nullptr);
            const BOOL flushed = FlushFileBuffers(handle);
            CloseHandle(handle);
            return ok != FALSE && flushed != FALSE && written == payload.size();
        }

        void WriteProxyLoadMarkers(HMODULE module)
        {
            std::call_once(g_markerOnce, [module]
            {
                wchar_t modulePathBuffer[MAX_PATH];
                GetModuleFileNameW(module, modulePathBuffer, MAX_PATH);

                wchar_t hostPathBuffer[MAX_PATH];
                GetModuleFileNameW(nullptr, hostPathBuffer, MAX_PATH);

                std::ostringstream line;
                line << '{'
                     << "\"timestamp_utc\":\"" << EventLogger::Instance().TimestampUtc() << '"'
                     << ",\"pid\":" << EventLogger::Instance().ProcessId()
                     << ",\"session_id\":\"" << EventLogger::Instance().SessionId() << '"'
                     << ",\"event\":\"proxy_loaded_marker\""
                     << ",\"host_exe\":\"" << JsonEscape(WideToUtf8(hostPathBuffer)) << '"'
                     << ",\"module_path\":\"" << JsonEscape(WideToUtf8(modulePathBuffer)) << '"'
                     << '}';

                std::wstring sessionMarkerPath = GetEnvironmentString(L"TS4_DX11_INTROSPECTION_PROXY_MARKER_PATH");
                if (sessionMarkerPath.empty())
                {
                    sessionMarkerPath = EventLogger::Instance().SessionDirectory();
                    if (!sessionMarkerPath.empty() && sessionMarkerPath.back() != L'\\')
                    {
                        sessionMarkerPath.push_back(L'\\');
                    }

                    sessionMarkerPath += L"proxy-loaded.marker";
                }

                AppendUtf8Line(sessionMarkerPath, line.str());
                AppendUtf8Line(GetGlobalMarkerPath(), line.str());
            });
        }

        void EmitRuntimeLoadedEvent()
        {
            std::call_once(g_sessionEventOnce, []
            {
                wchar_t modulePathBuffer[MAX_PATH];
                GetModuleFileNameW(nullptr, modulePathBuffer, MAX_PATH);
                std::ostringstream trace;
                trace << "\"host_exe\":\"" << JsonEscape(WideToUtf8(modulePathBuffer)) << '"'
                      << ",\"proxy_module\":\"d3d11.dll\"";
                EventLogger::Instance().WriteSessionTrace("proxy", "runtime_loaded", trace.str());
                std::ostringstream json;
                json << '{'
                     << "\"schema_version\":1"
                     << ",\"event_type\":\"runtime_loaded\""
                     << ",\"timestamp_utc\":\"" << EventLogger::Instance().TimestampUtc() << '"'
                     << ",\"pid\":" << EventLogger::Instance().ProcessId()
                     << ",\"session_id\":\"" << EventLogger::Instance().SessionId() << '"'
                     << ",\"host_exe\":\"" << JsonEscape(WideToUtf8(modulePathBuffer)) << '"'
                     << ",\"proxy_module\":\"d3d11.dll\""
                     << '}';
                EventLogger::Instance().WriteLine(json.str());
            });
        }

        std::string MakePointerString(const void* pointer)
        {
            std::ostringstream stream;
            stream << "0x" << std::hex << reinterpret_cast<std::uintptr_t>(pointer) << std::dec;
            return stream.str();
        }

        template <typename Fn>
        void PatchComMethod(void* instance, const std::size_t slotIndex, Fn replacement, Fn& original)
        {
            if (instance == nullptr)
            {
                return;
            }

            auto** vtable = *reinterpret_cast<void***>(instance);
            void** slot = &vtable[slotIndex];

            std::scoped_lock patchLock(g_factoryPatchMutex);
            if (g_patchedFactorySlots.contains(slot))
            {
                return;
            }

            DWORD oldProtection = 0;
            if (VirtualProtect(slot, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtection) == FALSE)
            {
                return;
            }

            original = reinterpret_cast<Fn>(*slot);
            *slot = reinterpret_cast<void*>(replacement);
            DWORD ignored = 0;
            VirtualProtect(slot, sizeof(void*), oldProtection, &ignored);
            FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
            g_patchedFactorySlots.insert(slot);
        }

        void EnsureDxgiModuleLoaded()
        {
            std::call_once(g_dxgiLoadOnce, []
            {
                const auto path = GetSystemDxgiPath();
                g_realDxgiModule = LoadLibraryW(path.c_str());
                if (g_realDxgiModule == nullptr)
                {
                    std::ostringstream failed;
                    failed << "\"system_dxgi_path\":\"" << JsonEscape(WideToUtf8(path)) << '"'
                           << ",\"last_error\":" << GetLastError();
                    EventLogger::Instance().WriteSessionTrace("proxy", "load_system_dxgi_failed", failed.str());
                    return;
                }

                g_realCreateDXGIFactory = reinterpret_cast<CreateDXGIFactoryFn>(GetProcAddress(g_realDxgiModule, "CreateDXGIFactory"));
                g_realCreateDXGIFactory1 = reinterpret_cast<CreateDXGIFactory1Fn>(GetProcAddress(g_realDxgiModule, "CreateDXGIFactory1"));
                g_realCreateDXGIFactory2 = reinterpret_cast<CreateDXGIFactory2Fn>(GetProcAddress(g_realDxgiModule, "CreateDXGIFactory2"));

                std::ostringstream loaded;
                loaded << "\"system_dxgi_path\":\"" << JsonEscape(WideToUtf8(path)) << '"'
                       << ",\"has_create_dxgi_factory\":" << (g_realCreateDXGIFactory == nullptr ? "false" : "true")
                       << ",\"has_create_dxgi_factory1\":" << (g_realCreateDXGIFactory1 == nullptr ? "false" : "true")
                       << ",\"has_create_dxgi_factory2\":" << (g_realCreateDXGIFactory2 == nullptr ? "false" : "true");
                EventLogger::Instance().WriteSessionTrace("proxy", "load_system_dxgi_complete", loaded.str());
            });
        }

        void PatchSwapChainFromFactoryResult(IUnknown* deviceUnknown, IDXGISwapChain* swapChain, const char* source)
        {
            if (swapChain == nullptr)
            {
                return;
            }

            ID3D11Device* d3d11Device = nullptr;
            if (deviceUnknown != nullptr)
            {
                deviceUnknown->QueryInterface(__uuidof(ID3D11Device), reinterpret_cast<void**>(&d3d11Device));
            }

            std::ostringstream trace;
            trace << "\"source\":\"" << source << '"'
                  << ",\"swap_chain\":\"" << MakePointerString(swapChain) << '"'
                  << ",\"has_d3d11_device\":" << (d3d11Device == nullptr ? "false" : "true");
            EventLogger::Instance().WriteSessionTrace("proxy", "factory_swap_chain_created", trace.str());

            InitializeHooksForDevice(d3d11Device, nullptr, swapChain);
            if (d3d11Device != nullptr)
            {
                d3d11Device->Release();
            }
        }

        HRESULT STDMETHODCALLTYPE Hook_FactoryCreateSwapChain(
            IDXGIFactory* self,
            IUnknown* device,
            DXGI_SWAP_CHAIN_DESC* description,
            IDXGISwapChain** swapChain)
        {
            const HRESULT hr = g_originalFactoryCreateSwapChain(self, device, description, swapChain);
            if (SUCCEEDED(hr) && swapChain != nullptr && *swapChain != nullptr)
            {
                PatchSwapChainFromFactoryResult(device, *swapChain, "IDXGIFactory::CreateSwapChain");
            }

            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_Factory2CreateSwapChainForHwnd(
            IDXGIFactory2* self,
            IUnknown* device,
            HWND hwnd,
            const DXGI_SWAP_CHAIN_DESC1* description,
            const DXGI_SWAP_CHAIN_FULLSCREEN_DESC* fullscreenDescription,
            IDXGIOutput* restrictToOutput,
            IDXGISwapChain1** swapChain)
        {
            static_cast<void>(hwnd);
            static_cast<void>(description);
            static_cast<void>(fullscreenDescription);
            static_cast<void>(restrictToOutput);

            const HRESULT hr = g_originalFactory2CreateSwapChainForHwnd(self, device, hwnd, description, fullscreenDescription, restrictToOutput, swapChain);
            if (SUCCEEDED(hr) && swapChain != nullptr && *swapChain != nullptr)
            {
                PatchSwapChainFromFactoryResult(device, *swapChain, "IDXGIFactory2::CreateSwapChainForHwnd");
            }

            return hr;
        }

        void PatchFactoryInstance(IUnknown* factoryUnknown)
        {
            if (factoryUnknown == nullptr)
            {
                return;
            }

            IDXGIFactory* factory = nullptr;
            if (SUCCEEDED(factoryUnknown->QueryInterface(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&factory))) && factory != nullptr)
            {
                PatchComMethod(factory, 10, Hook_FactoryCreateSwapChain, g_originalFactoryCreateSwapChain);
                factory->Release();
            }

            IDXGIFactory2* factory2 = nullptr;
            if (SUCCEEDED(factoryUnknown->QueryInterface(__uuidof(IDXGIFactory2), reinterpret_cast<void**>(&factory2))) && factory2 != nullptr)
            {
                PatchComMethod(factory2, 15, Hook_Factory2CreateSwapChainForHwnd, g_originalFactory2CreateSwapChainForHwnd);
                factory2->Release();
            }
        }

        void PatchDxgiFactoryImports()
        {
            std::call_once(g_dxgiImportPatchOnce, []
            {
                HMODULE mainModule = GetModuleHandleW(nullptr);
                if (mainModule == nullptr)
                {
                    return;
                }

                auto* dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(mainModule);
                if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
                {
                    return;
                }

                auto* ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(reinterpret_cast<std::uint8_t*>(mainModule) + dosHeader->e_lfanew);
                const auto& importDirectory = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
                if (importDirectory.VirtualAddress == 0)
                {
                    return;
                }

                auto* importDescriptor = reinterpret_cast<PIMAGE_IMPORT_DESCRIPTOR>(reinterpret_cast<std::uint8_t*>(mainModule) + importDirectory.VirtualAddress);
                for (; importDescriptor->Name != 0; ++importDescriptor)
                {
                    const char* dllName = reinterpret_cast<const char*>(reinterpret_cast<std::uint8_t*>(mainModule) + importDescriptor->Name);
                    if (_stricmp(dllName, "dxgi.dll") != 0)
                    {
                        continue;
                    }

                    auto* originalThunk = reinterpret_cast<PIMAGE_THUNK_DATA>(reinterpret_cast<std::uint8_t*>(mainModule) + importDescriptor->OriginalFirstThunk);
                    auto* firstThunk = reinterpret_cast<PIMAGE_THUNK_DATA>(reinterpret_cast<std::uint8_t*>(mainModule) + importDescriptor->FirstThunk);
                    for (; originalThunk->u1.AddressOfData != 0; ++originalThunk, ++firstThunk)
                    {
                        if (IMAGE_SNAP_BY_ORDINAL(originalThunk->u1.Ordinal))
                        {
                            continue;
                        }

                        auto* importByName = reinterpret_cast<PIMAGE_IMPORT_BY_NAME>(reinterpret_cast<std::uint8_t*>(mainModule) + originalThunk->u1.AddressOfData);
                        void* replacement = nullptr;
                        if (strcmp(reinterpret_cast<const char*>(importByName->Name), "CreateDXGIFactory") == 0)
                        {
                            replacement = reinterpret_cast<void*>(&CreateDXGIFactory_Proxy);
                        }
                        else if (strcmp(reinterpret_cast<const char*>(importByName->Name), "CreateDXGIFactory1") == 0)
                        {
                            replacement = reinterpret_cast<void*>(&CreateDXGIFactory1_Proxy);
                        }
                        else if (strcmp(reinterpret_cast<const char*>(importByName->Name), "CreateDXGIFactory2") == 0)
                        {
                            replacement = reinterpret_cast<void*>(&CreateDXGIFactory2_Proxy);
                        }

                        if (replacement == nullptr)
                        {
                            continue;
                        }

                        DWORD oldProtection = 0;
                        if (VirtualProtect(&firstThunk->u1.Function, sizeof(void*), PAGE_READWRITE, &oldProtection) == FALSE)
                        {
                            continue;
                        }

#if defined(_WIN64)
                        firstThunk->u1.Function = reinterpret_cast<ULONGLONG>(replacement);
#else
                        firstThunk->u1.Function = reinterpret_cast<DWORD>(replacement);
#endif

                        DWORD ignored = 0;
                        VirtualProtect(&firstThunk->u1.Function, sizeof(void*), oldProtection, &ignored);
                    }

                    EventLogger::Instance().WriteSessionTrace("proxy", "dxgi_imports_patched");
                    return;
                }
            });
        }

        void EnsureRealModuleLoaded()
        {
            std::call_once(g_loadOnce, []
            {
                const auto path = GetSystemD3D11Path();
                std::ostringstream beforeLoad;
                beforeLoad << "\"system_d3d11_path\":\"" << JsonEscape(WideToUtf8(path)) << '"';
                EventLogger::Instance().WriteSessionTrace("proxy", "load_system_d3d11_begin", beforeLoad.str());
                g_realModule = LoadLibraryW(path.c_str());
                if (g_realModule == nullptr)
                {
                    std::ostringstream failed;
                    failed << "\"system_d3d11_path\":\"" << JsonEscape(WideToUtf8(path)) << '"'
                           << ",\"last_error\":" << GetLastError();
                    EventLogger::Instance().WriteSessionTrace("proxy", "load_system_d3d11_failed", failed.str());
                    return;
                }

                g_realCreateDevice = reinterpret_cast<D3D11CreateDeviceFn>(GetProcAddress(g_realModule, "D3D11CreateDevice"));
                g_realCreateDeviceAndSwapChain = reinterpret_cast<D3D11CreateDeviceAndSwapChainFn>(GetProcAddress(g_realModule, "D3D11CreateDeviceAndSwapChain"));
                std::ostringstream afterLoad;
                afterLoad << "\"system_d3d11_path\":\"" << JsonEscape(WideToUtf8(path)) << '"'
                          << ",\"module_loaded\":true"
                          << ",\"has_create_device\":" << (g_realCreateDevice == nullptr ? "false" : "true")
                          << ",\"has_create_device_and_swap_chain\":" << (g_realCreateDeviceAndSwapChain == nullptr ? "false" : "true");
                EventLogger::Instance().WriteSessionTrace("proxy", "load_system_d3d11_complete", afterLoad.str());
            });
        }
    }
}

extern "C" HRESULT WINAPI D3D11CreateDevice_Proxy(
    IDXGIAdapter* adapter,
    const D3D_DRIVER_TYPE driverType,
    HMODULE software,
    const UINT flags,
    const D3D_FEATURE_LEVEL* featureLevels,
    const UINT featureLevelsCount,
    const UINT sdkVersion,
    ID3D11Device** device,
    D3D_FEATURE_LEVEL* featureLevel,
    ID3D11DeviceContext** immediateContext)
{
    ts4dx11::EnsureRealModuleLoaded();
    if (ts4dx11::g_realCreateDevice == nullptr)
    {
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_device_unavailable");
        return E_FAIL;
    }

    ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_device_called");

    const HRESULT hr = ts4dx11::g_realCreateDevice(
        adapter,
        driverType,
        software,
        flags,
        featureLevels,
        featureLevelsCount,
        sdkVersion,
        device,
        featureLevel,
        immediateContext);

    if (SUCCEEDED(hr) && device != nullptr && *device != nullptr)
    {
        ts4dx11::EmitRuntimeLoadedEvent();
        ts4dx11::InitializeHooksForDevice(*device, immediateContext == nullptr ? nullptr : *immediateContext, nullptr);
    }
    else
    {
        std::ostringstream trace;
        trace << "\"hresult\":\"0x" << std::hex << static_cast<unsigned long>(hr) << std::dec << '"';
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_device_failed", trace.str());
    }

    return hr;
}

extern "C" HRESULT WINAPI CreateDXGIFactory_Proxy(REFIID riid, void** factory)
{
    ts4dx11::EnsureDxgiModuleLoaded();
    if (ts4dx11::g_realCreateDXGIFactory == nullptr)
    {
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_dxgi_factory_unavailable");
        return E_FAIL;
    }

    const HRESULT hr = ts4dx11::g_realCreateDXGIFactory(riid, factory);
    if (SUCCEEDED(hr) && factory != nullptr && *factory != nullptr)
    {
        ts4dx11::PatchFactoryInstance(reinterpret_cast<IUnknown*>(*factory));
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_dxgi_factory_called");
    }

    return hr;
}

extern "C" HRESULT WINAPI CreateDXGIFactory1_Proxy(REFIID riid, void** factory)
{
    ts4dx11::EnsureDxgiModuleLoaded();
    if (ts4dx11::g_realCreateDXGIFactory1 == nullptr)
    {
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_dxgi_factory1_unavailable");
        return E_FAIL;
    }

    const HRESULT hr = ts4dx11::g_realCreateDXGIFactory1(riid, factory);
    if (SUCCEEDED(hr) && factory != nullptr && *factory != nullptr)
    {
        ts4dx11::PatchFactoryInstance(reinterpret_cast<IUnknown*>(*factory));
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_dxgi_factory1_called");
    }

    return hr;
}

extern "C" HRESULT WINAPI CreateDXGIFactory2_Proxy(const UINT flags, REFIID riid, void** factory)
{
    ts4dx11::EnsureDxgiModuleLoaded();
    if (ts4dx11::g_realCreateDXGIFactory2 == nullptr)
    {
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_dxgi_factory2_unavailable");
        return E_FAIL;
    }

    const HRESULT hr = ts4dx11::g_realCreateDXGIFactory2(flags, riid, factory);
    if (SUCCEEDED(hr) && factory != nullptr && *factory != nullptr)
    {
        ts4dx11::PatchFactoryInstance(reinterpret_cast<IUnknown*>(*factory));
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_dxgi_factory2_called");
    }

    return hr;
}

extern "C" HRESULT WINAPI D3D11CreateDeviceAndSwapChain_Proxy(
    IDXGIAdapter* adapter,
    const D3D_DRIVER_TYPE driverType,
    HMODULE software,
    const UINT flags,
    const D3D_FEATURE_LEVEL* featureLevels,
    const UINT featureLevelsCount,
    const UINT sdkVersion,
    const DXGI_SWAP_CHAIN_DESC* swapChainDescription,
    IDXGISwapChain** swapChain,
    ID3D11Device** device,
    D3D_FEATURE_LEVEL* featureLevel,
    ID3D11DeviceContext** immediateContext)
{
    ts4dx11::EnsureRealModuleLoaded();
    if (ts4dx11::g_realCreateDeviceAndSwapChain == nullptr)
    {
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_device_and_swap_chain_unavailable");
        return E_FAIL;
    }

    ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_device_and_swap_chain_called");

    const HRESULT hr = ts4dx11::g_realCreateDeviceAndSwapChain(
        adapter,
        driverType,
        software,
        flags,
        featureLevels,
        featureLevelsCount,
        sdkVersion,
        swapChainDescription,
        swapChain,
        device,
        featureLevel,
        immediateContext);

    if (SUCCEEDED(hr) && device != nullptr && *device != nullptr)
    {
        ts4dx11::EmitRuntimeLoadedEvent();
        ts4dx11::InitializeHooksForDevice(
            *device,
            immediateContext == nullptr ? nullptr : *immediateContext,
            swapChain == nullptr ? nullptr : *swapChain);
    }
    else
    {
        std::ostringstream trace;
        trace << "\"hresult\":\"0x" << std::hex << static_cast<unsigned long>(hr) << std::dec << '"';
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "create_device_and_swap_chain_failed", trace.str());
    }

    return hr;
}

BOOL APIENTRY DllMain(HMODULE module, const DWORD reason, LPVOID reserved)
{
    static_cast<void>(reserved);
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        ts4dx11::WriteProxyLoadMarkers(module);
        ts4dx11::PatchDxgiFactoryImports();
        wchar_t modulePathBuffer[MAX_PATH];
        GetModuleFileNameW(module, modulePathBuffer, MAX_PATH);
        std::ostringstream extra;
        extra << "\"module_path\":\"" << ts4dx11::JsonEscape(ts4dx11::WideToUtf8(modulePathBuffer)) << '"';
        ts4dx11::EventLogger::Instance().WriteSessionTrace("proxy", "dll_process_attach", extra.str());
    }

    return TRUE;
}
