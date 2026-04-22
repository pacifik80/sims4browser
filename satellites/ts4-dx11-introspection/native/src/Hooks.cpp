#include "Hooks.hpp"

#include "Logger.hpp"
#include "Reflection.hpp"

#include <bcrypt.h>

#include <array>
#include <cstdlib>
#include <iomanip>
#include <sstream>
#include <unordered_set>
#include <vector>

namespace ts4dx11
{
    namespace
    {
        constexpr std::size_t DeviceCreateVertexShaderIndex = 12;
        constexpr std::size_t DeviceCreateGeometryShaderIndex = 13;
        constexpr std::size_t DeviceCreatePixelShaderIndex = 15;
        constexpr std::size_t DeviceCreateHullShaderIndex = 16;
        constexpr std::size_t DeviceCreateDomainShaderIndex = 17;
        constexpr std::size_t DeviceCreateDeferredContextIndex = 27;

        constexpr std::size_t ContextVSSetConstantBuffersIndex = 3;
        constexpr std::size_t ContextPSSetShaderResourcesIndex = 4;
        constexpr std::size_t ContextPSSetShaderIndex = 5;
        constexpr std::size_t ContextPSSetSamplersIndex = 6;
        constexpr std::size_t ContextVSSetShaderIndex = 7;
        constexpr std::size_t ContextDrawIndexedIndex = 8;
        constexpr std::size_t ContextDrawIndex = 9;
        constexpr std::size_t ContextPSSetConstantBuffersIndex = 12;
        constexpr std::size_t ContextDrawIndexedInstancedIndex = 16;
        constexpr std::size_t ContextDrawInstancedIndex = 17;
        constexpr std::size_t ContextVSSetShaderResourcesIndex = 21;
        constexpr std::size_t ContextOMSetBlendStateIndex = 31;
        constexpr std::size_t ContextOMSetDepthStencilStateIndex = 32;
        constexpr std::size_t ContextRSSetStateIndex = 39;
        constexpr std::size_t ContextExecuteCommandListIndex = 54;
        constexpr std::size_t ContextFinishCommandListIndex = 110;

        constexpr std::size_t SwapChainPresentIndex = 8;
        constexpr std::size_t SwapChain1Present1Index = 22;

        using CreateVertexShaderFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11Device*, const void*, SIZE_T, ID3D11ClassLinkage*, ID3D11VertexShader**);
        using CreateGeometryShaderFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11Device*, const void*, SIZE_T, ID3D11ClassLinkage*, ID3D11GeometryShader**);
        using CreatePixelShaderFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11Device*, const void*, SIZE_T, ID3D11ClassLinkage*, ID3D11PixelShader**);
        using CreateHullShaderFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11Device*, const void*, SIZE_T, ID3D11ClassLinkage*, ID3D11HullShader**);
        using CreateDomainShaderFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11Device*, const void*, SIZE_T, ID3D11ClassLinkage*, ID3D11DomainShader**);
        using CreateDeferredContextFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11Device*, UINT, ID3D11DeviceContext**);

        using VSSetConstantBuffersFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, ID3D11Buffer* const*);
        using PSSetShaderResourcesFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, ID3D11ShaderResourceView* const*);
        using PSSetShaderFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, ID3D11PixelShader*, ID3D11ClassInstance* const*, UINT);
        using PSSetSamplersFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, ID3D11SamplerState* const*);
        using VSSetShaderFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, ID3D11VertexShader*, ID3D11ClassInstance* const*, UINT);
        using DrawIndexedFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, INT);
        using DrawFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT);
        using PSSetConstantBuffersFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, ID3D11Buffer* const*);
        using DrawIndexedInstancedFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, UINT, INT, UINT);
        using DrawInstancedFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, UINT, UINT);
        using VSSetShaderResourcesFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, UINT, UINT, ID3D11ShaderResourceView* const*);
        using OMSetBlendStateFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, ID3D11BlendState*, const FLOAT[4], UINT);
        using OMSetDepthStencilStateFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, ID3D11DepthStencilState*, UINT);
        using RSSetStateFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, ID3D11RasterizerState*);
        using ExecuteCommandListFn = void(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, ID3D11CommandList*, BOOL);
        using FinishCommandListFn = HRESULT(STDMETHODCALLTYPE*)(ID3D11DeviceContext*, BOOL, ID3D11CommandList**);
        using PresentFn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
        using Present1Fn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain1*, UINT, UINT, const DXGI_PRESENT_PARAMETERS*);

        struct ContextHookOriginals
        {
            VSSetConstantBuffersFn vsSetConstantBuffers = nullptr;
            PSSetShaderResourcesFn psSetShaderResources = nullptr;
            PSSetShaderFn psSetShader = nullptr;
            PSSetSamplersFn psSetSamplers = nullptr;
            VSSetShaderFn vsSetShader = nullptr;
            DrawIndexedFn drawIndexed = nullptr;
            DrawFn draw = nullptr;
            PSSetConstantBuffersFn psSetConstantBuffers = nullptr;
            DrawIndexedInstancedFn drawIndexedInstanced = nullptr;
            DrawInstancedFn drawInstanced = nullptr;
            VSSetShaderResourcesFn vsSetShaderResources = nullptr;
            OMSetBlendStateFn omSetBlendState = nullptr;
            OMSetDepthStencilStateFn omSetDepthStencilState = nullptr;
            RSSetStateFn rsSetState = nullptr;
            ExecuteCommandListFn executeCommandList = nullptr;
            FinishCommandListFn finishCommandList = nullptr;
        };

        CreateVertexShaderFn g_originalCreateVertexShader = nullptr;
        CreateGeometryShaderFn g_originalCreateGeometryShader = nullptr;
        CreatePixelShaderFn g_originalCreatePixelShader = nullptr;
        CreateHullShaderFn g_originalCreateHullShader = nullptr;
        CreateDomainShaderFn g_originalCreateDomainShader = nullptr;
        CreateDeferredContextFn g_originalCreateDeferredContext = nullptr;
        PresentFn g_originalPresent = nullptr;
        Present1Fn g_originalPresent1 = nullptr;

        std::unordered_set<void**> g_patchedSlots;
        std::unordered_map<void**, ContextHookOriginals> g_contextOriginalsByVtable;
        std::mutex g_patchMutex;

        void PatchDeviceHooks(ID3D11Device* device);
        void PatchContextHooks(ID3D11DeviceContext* context);
        void PatchDeferredContextHooks(ID3D11DeviceContext* context);
        void PatchSwapChainHooks(IDXGISwapChain* swapChain);

        template <typename Fn>
        void PatchMethod(void* instance, const std::size_t slotIndex, Fn replacement, Fn& original)
        {
            if (instance == nullptr)
            {
                return;
            }

            auto** vtable = *reinterpret_cast<void***>(instance);
            void** slot = &vtable[slotIndex];

            std::scoped_lock patchLock(g_patchMutex);
            if (g_patchedSlots.contains(slot))
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
            g_patchedSlots.insert(slot);
        }

        ContextHookOriginals& GetOrCreateContextOriginals(ID3D11DeviceContext* context)
        {
            auto** vtable = *reinterpret_cast<void***>(context);
            std::scoped_lock patchLock(g_patchMutex);
            return g_contextOriginalsByVtable[vtable];
        }

        ContextHookOriginals GetContextOriginalsSnapshot(ID3D11DeviceContext* context)
        {
            if (context == nullptr)
            {
                return {};
            }

            auto** vtable = *reinterpret_cast<void***>(context);
            std::scoped_lock patchLock(g_patchMutex);
            const auto iterator = g_contextOriginalsByVtable.find(vtable);
            if (iterator == g_contextOriginalsByVtable.end())
            {
                return {};
            }

            return iterator->second;
        }

        bool IsCaptureActiveLocked(const RuntimeState& state)
        {
            static_cast<void>(state);
            return true;
        }

        void PlaySignal(const int frequency, const int durationMs, const int repeatCount)
        {
            for (int index = 0; index < repeatCount; ++index)
            {
                Beep(frequency, durationMs);
                if (index + 1 < repeatCount)
                {
                    Sleep(35);
                }
            }
        }

        std::string BaseEventJson(const char* eventType)
        {
            auto& logger = EventLogger::Instance();
            std::ostringstream json;
            json << '{'
                 << "\"schema_version\":1"
                 << ",\"event_type\":\"" << eventType << '"'
                 << ",\"timestamp_utc\":\"" << logger.TimestampUtc() << '"'
                 << ",\"pid\":" << logger.ProcessId()
                 << ",\"session_id\":\"" << logger.SessionId() << '"';
            return json.str();
        }

        std::string MakePointerId(const void* pointer)
        {
            if (pointer == nullptr)
            {
                return {};
            }

            std::ostringstream stream;
            stream << "0x" << std::hex << reinterpret_cast<std::uintptr_t>(pointer) << std::dec;
            return stream.str();
        }

        template <std::size_t N, typename T>
        void UpdateSlotOccupancy(std::array<bool, N>& slots, const UINT startSlot, const UINT count, T* const* values)
        {
            for (UINT offset = 0; offset < count; ++offset)
            {
                const UINT slot = startSlot + offset;
                if (slot >= N)
                {
                    break;
                }

                slots[slot] = values != nullptr && values[offset] != nullptr;
            }
        }

        template <std::size_t N>
        std::string BuildSlotArrayJson(const std::array<bool, N>& slots)
        {
            std::ostringstream json;
            json << '[';
            bool first = true;
            for (std::size_t index = 0; index < N; ++index)
            {
                if (!slots[index])
                {
                    continue;
                }

                if (!first)
                {
                    json << ',';
                }

                json << index;
                first = false;
            }

            json << ']';
            return json.str();
        }

        void EmitShaderCreatedEvent(const char* stage, const std::string& hash, const SIZE_T bytecodeLength, const void* shaderPointer, const ReflectionSummary& reflection)
        {
            std::ostringstream json;
            json << BaseEventJson("shader_created")
                 << ",\"stage\":\"" << stage << '"'
                 << ",\"shader_hash\":\"" << hash << '"'
                 << ",\"bytecode_size\":" << bytecodeLength
                 << ",\"shader_pointer\":\"0x" << std::hex << reinterpret_cast<std::uintptr_t>(shaderPointer) << std::dec << '"'
                 << ",\"reflection\":" << BuildReflectionJson(reflection)
                 << '}';
            EventLogger::Instance().WriteLine(json.str());
        }

        void EnsureStateDefinitionLocked(
            RuntimeState& state,
            std::unordered_map<std::uintptr_t, std::string>& definitions,
            const std::uintptr_t key,
            const char* stateKind,
            const std::string& summary)
        {
            if (key == 0 || definitions.contains(key))
            {
                return;
            }

            std::ostringstream json;
            json << BaseEventJson("state_definition")
                 << ",\"frame_index\":" << state.frameIndex
                 << ",\"state_kind\":\"" << stateKind << '"'
                 << ",\"state_id\":\"" << MakePointerId(reinterpret_cast<const void*>(key)) << '"'
                 << ",\"summary\":" << summary
                 << '}';
            definitions.emplace(key, summary);
            EventLogger::Instance().WriteLine(json.str());
        }

        void EmitFirstHitTraceOnceLocked(RuntimeState& state, bool& flag, const char* action, const std::string& extraJson = {})
        {
            if (flag)
            {
                return;
            }

            flag = true;
            EventLogger::Instance().WriteSessionTrace("hooks", action, extraJson);
        }

        void EmitDrawPathDiagnosticsLocked(RuntimeState& state, const char* drawKind)
        {
            std::ostringstream drawTrace;
            drawTrace << "\"draw_kind\":\"" << drawKind << '"'
                      << ",\"frame_index\":" << state.frameIndex
                      << ",\"draw_count_in_frame\":" << state.drawCountInFrame;
            EmitFirstHitTraceOnceLocked(state, state.firstDrawSeen, "draw_seen", drawTrace.str());

            if (!state.firstPresentSeen && !state.drawsSeenBeforeFirstPresentLogged && state.drawCountInFrame >= 32)
            {
                state.drawsSeenBeforeFirstPresentLogged = true;
                std::ostringstream prePresentTrace;
                prePresentTrace << "\"draw_kind\":\"" << drawKind << '"'
                                << ",\"frame_index\":" << state.frameIndex
                                << ",\"draw_count_in_frame\":" << state.drawCountInFrame;
                EventLogger::Instance().WriteSessionTrace("hooks", "draws_accumulating_before_present", prePresentTrace.str());
            }
        }

        void HandlePresentBoundaryLocked(RuntimeState& state, const UINT syncInterval, const UINT flags, const char* source)
        {
            std::ostringstream presentTrace;
            presentTrace << "\"source\":\"" << source << '"'
                         << ",\"sync_interval\":" << syncInterval
                         << ",\"flags\":" << flags;

            if (source[7] == '1')
            {
                EmitFirstHitTraceOnceLocked(state, state.firstPresent1Seen, "present1_seen", presentTrace.str());
            }
            else
            {
                EmitFirstHitTraceOnceLocked(state, state.firstPresentSeen, "present_seen", presentTrace.str());
            }

            std::ostringstream frameJson;
            frameJson << BaseEventJson("frame_boundary")
                      << ",\"frame_index\":" << state.frameIndex
                      << ",\"present_flags\":" << flags
                      << ",\"sync_interval\":" << syncInterval
                      << ",\"capture_active\":true"
                      << ",\"detail_mode\":\"continuous\""
                      << ",\"present_source\":\"" << source << '"'
                      << ",\"bound_vs_hash\":\"" << state.boundVsHash << '"'
                      << ",\"bound_ps_hash\":\"" << state.boundPsHash << '"'
                      << ",\"command_lists_recorded_in_frame\":" << state.commandListsRecordedInFrame
                      << ",\"command_lists_executed_in_frame\":" << state.commandListsExecutedInFrame
                      << ",\"draw_count_in_frame\":" << state.drawCountInFrame
                      << '}';
            const bool frameLogged = EventLogger::Instance().WriteLine(frameJson.str());
            if (frameLogged && !state.loggerActivationSignalPlayed)
            {
                state.loggerActivationSignalPlayed = true;
                EventLogger::Instance().WriteSessionTrace("logger", "continuous_logging_active");
                PlaySignal(1200, 55, 2);
            }
            else if (!frameLogged)
            {
                std::ostringstream errorTrace;
                errorTrace << "\"event_type\":\"frame_boundary\""
                           << ",\"present_source\":\"" << source << '"'
                           << ",\"last_error\":" << EventLogger::Instance().LastWriteError();
                EventLogger::Instance().WriteSessionTrace("logger", "write_failed", errorTrace.str());
            }

            state.drawCountInFrame = 0;
            state.commandListsRecordedInFrame = 0;
            state.commandListsExecutedInFrame = 0;
            ++state.frameIndex;
        }

        void TryEmitBookmarkLocked(RuntimeState& state)
        {
            if ((GetAsyncKeyState(VK_F10) & 1) == 0)
            {
                return;
            }

            std::ostringstream json;
            json << BaseEventJson("bookmark")
                 << ",\"frame_index\":" << state.frameIndex
                 << ",\"bound_vs_hash\":\"" << state.boundVsHash << '"'
                 << ",\"bound_ps_hash\":\"" << state.boundPsHash << '"'
                 << '}';
            const bool bookmarkLogged = EventLogger::Instance().WriteLine(json.str());
            if (bookmarkLogged)
            {
                PlaySignal(1400, 70, 1);
            }
            else
            {
                std::ostringstream errorTrace;
                errorTrace << "\"event_type\":\"bookmark\""
                           << ",\"last_error\":" << EventLogger::Instance().LastWriteError();
                EventLogger::Instance().WriteSessionTrace("logger", "write_failed", errorTrace.str());
            }
        }

        void EmitCommandListEventLocked(
            const RuntimeState& state,
            const char* eventType,
            ID3D11CommandList* commandList,
            const BOOL restoreState,
            const std::string& extraJson = {})
        {
            std::ostringstream json;
            json << BaseEventJson(eventType)
                 << ",\"frame_index\":" << state.frameIndex
                 << ",\"command_list\":\"" << MakePointerId(commandList) << '"'
                 << ",\"restore_state\":" << (restoreState ? "true" : "false");
            if (!extraJson.empty())
            {
                json << ',' << extraJson;
            }

            json << '}';
            EventLogger::Instance().WriteLine(json.str());
        }

        template <typename T, std::size_t N>
        void ReleaseComArray(T* (&values)[N])
        {
            for (auto*& value : values)
            {
                if (value != nullptr)
                {
                    value->Release();
                    value = nullptr;
                }
            }
        }

        void RefreshImmediateStateFromSwapChain(IDXGISwapChain* swapChain)
        {
            if (swapChain == nullptr)
            {
                return;
            }

            ID3D11Device* device = nullptr;
            if (FAILED(swapChain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&device))) || device == nullptr)
            {
                return;
            }

            ID3D11DeviceContext* context = nullptr;
            device->GetImmediateContext(&context);
            if (context == nullptr)
            {
                device->Release();
                return;
            }

            ID3D11VertexShader* vertexShader = nullptr;
            ID3D11PixelShader* pixelShader = nullptr;
            context->VSGetShader(&vertexShader, nullptr, nullptr);
            context->PSGetShader(&pixelShader, nullptr, nullptr);

            ID3D11ShaderResourceView* vsSrvs[D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT] {};
            ID3D11ShaderResourceView* psSrvs[D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT] {};
            ID3D11Buffer* vsCbs[D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT] {};
            ID3D11Buffer* psCbs[D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT] {};
            ID3D11SamplerState* psSamplers[D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT] {};
            context->VSGetShaderResources(0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, vsSrvs);
            context->PSGetShaderResources(0, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT, psSrvs);
            context->VSGetConstantBuffers(0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT, vsCbs);
            context->PSGetConstantBuffers(0, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT, psCbs);
            context->PSGetSamplers(0, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT, psSamplers);

            ID3D11BlendState* blendState = nullptr;
            FLOAT blendFactor[4] {};
            UINT sampleMask = 0;
            context->OMGetBlendState(&blendState, blendFactor, &sampleMask);

            ID3D11DepthStencilState* depthStencilState = nullptr;
            UINT stencilRef = 0;
            context->OMGetDepthStencilState(&depthStencilState, &stencilRef);

            ID3D11RasterizerState* rasterizerState = nullptr;
            context->RSGetState(&rasterizerState);

            {
                auto& state = GetRuntimeState();
                std::scoped_lock lock(state.mutex);

                if (vertexShader == nullptr)
                {
                    state.boundVsHash.clear();
                }
                else
                {
                    const auto iterator = state.shadersByPointer.find(reinterpret_cast<std::uintptr_t>(vertexShader));
                    state.boundVsHash = iterator == state.shadersByPointer.end() ? std::string() : iterator->second.hash;
                }

                if (pixelShader == nullptr)
                {
                    state.boundPsHash.clear();
                }
                else
                {
                    const auto iterator = state.shadersByPointer.find(reinterpret_cast<std::uintptr_t>(pixelShader));
                    state.boundPsHash = iterator == state.shadersByPointer.end() ? std::string() : iterator->second.hash;
                }

                state.vsShaderResourceSlots.fill(false);
                state.psShaderResourceSlots.fill(false);
                state.vsConstantBufferSlots.fill(false);
                state.psConstantBufferSlots.fill(false);
                state.psSamplerSlots.fill(false);
                for (std::size_t index = 0; index < D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT; ++index)
                {
                    state.vsShaderResourceSlots[index] = vsSrvs[index] != nullptr;
                    state.psShaderResourceSlots[index] = psSrvs[index] != nullptr;
                }

                for (std::size_t index = 0; index < D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT; ++index)
                {
                    state.vsConstantBufferSlots[index] = vsCbs[index] != nullptr;
                    state.psConstantBufferSlots[index] = psCbs[index] != nullptr;
                }

                for (std::size_t index = 0; index < D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT; ++index)
                {
                    state.psSamplerSlots[index] = psSamplers[index] != nullptr;
                }

                const auto blendKey = reinterpret_cast<std::uintptr_t>(blendState);
                const auto blendSummary = CaptureBlendStateSummary(blendState, blendFactor, sampleMask);
                EnsureStateDefinitionLocked(state, state.blendStateDescriptions, blendKey, "blend", blendSummary);
                state.currentBlendStateId = MakePointerId(blendState);

                const auto depthKey = reinterpret_cast<std::uintptr_t>(depthStencilState);
                const auto depthSummary = CaptureDepthStencilStateSummary(depthStencilState, stencilRef);
                EnsureStateDefinitionLocked(state, state.depthStencilStateDescriptions, depthKey, "depth_stencil", depthSummary);
                state.currentDepthStencilStateId = MakePointerId(depthStencilState);

                const auto rasterKey = reinterpret_cast<std::uintptr_t>(rasterizerState);
                const auto rasterSummary = CaptureRasterizerStateSummary(rasterizerState);
                EnsureStateDefinitionLocked(state, state.rasterizerStateDescriptions, rasterKey, "rasterizer", rasterSummary);
                state.currentRasterizerStateId = MakePointerId(rasterizerState);
            }

            if (vertexShader != nullptr) vertexShader->Release();
            if (pixelShader != nullptr) pixelShader->Release();
            if (blendState != nullptr) blendState->Release();
            if (depthStencilState != nullptr) depthStencilState->Release();
            if (rasterizerState != nullptr) rasterizerState->Release();
            ReleaseComArray(vsSrvs);
            ReleaseComArray(psSrvs);
            ReleaseComArray(vsCbs);
            ReleaseComArray(psCbs);
            ReleaseComArray(psSamplers);
            context->Release();
            device->Release();
        }

        HRESULT STDMETHODCALLTYPE Hook_CreateVertexShader(
            ID3D11Device* self,
            const void* bytecode,
            const SIZE_T bytecodeLength,
            ID3D11ClassLinkage* linkage,
            ID3D11VertexShader** shader)
        {
            const HRESULT hr = g_originalCreateVertexShader(self, bytecode, bytecodeLength, linkage, shader);
            if (SUCCEEDED(hr) && shader != nullptr && *shader != nullptr && bytecode != nullptr && bytecodeLength > 0)
            {
                const auto hash = ComputeShaderHash(bytecode, bytecodeLength);
                const auto reflection = ReflectShader(bytecode, bytecodeLength);
                {
                    auto& state = GetRuntimeState();
                    std::scoped_lock lock(state.mutex);
                    state.shadersByPointer.emplace(reinterpret_cast<std::uintptr_t>(*shader), ShaderMetadata { hash, "vs", bytecodeLength });
                }
                EmitShaderCreatedEvent("vs", hash, bytecodeLength, *shader, reflection);
            }

            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_CreateGeometryShader(
            ID3D11Device* self,
            const void* bytecode,
            const SIZE_T bytecodeLength,
            ID3D11ClassLinkage* linkage,
            ID3D11GeometryShader** shader)
        {
            const HRESULT hr = g_originalCreateGeometryShader(self, bytecode, bytecodeLength, linkage, shader);
            if (SUCCEEDED(hr) && shader != nullptr && *shader != nullptr && bytecode != nullptr && bytecodeLength > 0)
            {
                const auto hash = ComputeShaderHash(bytecode, bytecodeLength);
                const auto reflection = ReflectShader(bytecode, bytecodeLength);
                {
                    auto& state = GetRuntimeState();
                    std::scoped_lock lock(state.mutex);
                    state.shadersByPointer.emplace(reinterpret_cast<std::uintptr_t>(*shader), ShaderMetadata { hash, "gs", bytecodeLength });
                }
                EmitShaderCreatedEvent("gs", hash, bytecodeLength, *shader, reflection);
            }

            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_CreatePixelShader(
            ID3D11Device* self,
            const void* bytecode,
            const SIZE_T bytecodeLength,
            ID3D11ClassLinkage* linkage,
            ID3D11PixelShader** shader)
        {
            const HRESULT hr = g_originalCreatePixelShader(self, bytecode, bytecodeLength, linkage, shader);
            if (SUCCEEDED(hr) && shader != nullptr && *shader != nullptr && bytecode != nullptr && bytecodeLength > 0)
            {
                const auto hash = ComputeShaderHash(bytecode, bytecodeLength);
                const auto reflection = ReflectShader(bytecode, bytecodeLength);
                {
                    auto& state = GetRuntimeState();
                    std::scoped_lock lock(state.mutex);
                    state.shadersByPointer.emplace(reinterpret_cast<std::uintptr_t>(*shader), ShaderMetadata { hash, "ps", bytecodeLength });
                }
                EmitShaderCreatedEvent("ps", hash, bytecodeLength, *shader, reflection);
            }

            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_CreateHullShader(
            ID3D11Device* self,
            const void* bytecode,
            const SIZE_T bytecodeLength,
            ID3D11ClassLinkage* linkage,
            ID3D11HullShader** shader)
        {
            const HRESULT hr = g_originalCreateHullShader(self, bytecode, bytecodeLength, linkage, shader);
            if (SUCCEEDED(hr) && shader != nullptr && *shader != nullptr && bytecode != nullptr && bytecodeLength > 0)
            {
                const auto hash = ComputeShaderHash(bytecode, bytecodeLength);
                const auto reflection = ReflectShader(bytecode, bytecodeLength);
                {
                    auto& state = GetRuntimeState();
                    std::scoped_lock lock(state.mutex);
                    state.shadersByPointer.emplace(reinterpret_cast<std::uintptr_t>(*shader), ShaderMetadata { hash, "hs", bytecodeLength });
                }
                EmitShaderCreatedEvent("hs", hash, bytecodeLength, *shader, reflection);
            }

            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_CreateDomainShader(
            ID3D11Device* self,
            const void* bytecode,
            const SIZE_T bytecodeLength,
            ID3D11ClassLinkage* linkage,
            ID3D11DomainShader** shader)
        {
            const HRESULT hr = g_originalCreateDomainShader(self, bytecode, bytecodeLength, linkage, shader);
            if (SUCCEEDED(hr) && shader != nullptr && *shader != nullptr && bytecode != nullptr && bytecodeLength > 0)
            {
                const auto hash = ComputeShaderHash(bytecode, bytecodeLength);
                const auto reflection = ReflectShader(bytecode, bytecodeLength);
                {
                    auto& state = GetRuntimeState();
                    std::scoped_lock lock(state.mutex);
                    state.shadersByPointer.emplace(reinterpret_cast<std::uintptr_t>(*shader), ShaderMetadata { hash, "ds", bytecodeLength });
                }
                EmitShaderCreatedEvent("ds", hash, bytecodeLength, *shader, reflection);
            }

            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_CreateDeferredContext(ID3D11Device* self, const UINT contextFlags, ID3D11DeviceContext** deferredContext)
        {
            const HRESULT hr = g_originalCreateDeferredContext(self, contextFlags, deferredContext);
            if (SUCCEEDED(hr) && deferredContext != nullptr && *deferredContext != nullptr)
            {
                auto& state = GetRuntimeState();
                {
                    std::scoped_lock lock(state.mutex);
                    std::ostringstream trace;
                    trace << "\"context_flags\":" << contextFlags;
                    EmitFirstHitTraceOnceLocked(state, state.firstDeferredContextSeen, "deferred_context_created", trace.str());
                }

                PatchDeferredContextHooks(*deferredContext);
                const auto deferredOriginals = GetContextOriginalsSnapshot(*deferredContext);
                EventLogger::Instance().WriteSessionTrace(
                    "hooks",
                    "deferred_context_state_hook_attempt",
                    std::string("\"finish_hooked\":") + (deferredOriginals.finishCommandList != nullptr ? "true" : "false") +
                        ",\"vs_hooked\":false" +
                        ",\"ps_hooked\":false" +
                        ",\"blend_hooked\":false" +
                        ",\"depth_hooked\":false" +
                        ",\"raster_hooked\":false");
            }

            return hr;
        }

        void STDMETHODCALLTYPE Hook_VSSetConstantBuffers(ID3D11DeviceContext* self, const UINT startSlot, const UINT count, ID3D11Buffer* const* buffers)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.vsSetConstantBuffers == nullptr)
            {
                return;
            }

            originals.vsSetConstantBuffers(self, startSlot, count, buffers);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            UpdateSlotOccupancy(state.vsConstantBufferSlots, startSlot, count, buffers);
        }

        void STDMETHODCALLTYPE Hook_PSSetShaderResources(ID3D11DeviceContext* self, const UINT startSlot, const UINT count, ID3D11ShaderResourceView* const* resources)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.psSetShaderResources == nullptr)
            {
                return;
            }

            originals.psSetShaderResources(self, startSlot, count, resources);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            UpdateSlotOccupancy(state.psShaderResourceSlots, startSlot, count, resources);
        }

        void STDMETHODCALLTYPE Hook_PSSetShader(ID3D11DeviceContext* self, ID3D11PixelShader* shader, ID3D11ClassInstance* const* classInstances, const UINT classInstanceCount)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.psSetShader == nullptr)
            {
                return;
            }

            originals.psSetShader(self, shader, classInstances, classInstanceCount);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            if (shader == nullptr)
            {
                state.boundPsHash.clear();
            }
            else
            {
                const auto iterator = state.shadersByPointer.find(reinterpret_cast<std::uintptr_t>(shader));
                state.boundPsHash = iterator == state.shadersByPointer.end() ? std::string() : iterator->second.hash;
            }

        }

        void STDMETHODCALLTYPE Hook_PSSetSamplers(ID3D11DeviceContext* self, const UINT startSlot, const UINT count, ID3D11SamplerState* const* samplers)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.psSetSamplers == nullptr)
            {
                return;
            }

            originals.psSetSamplers(self, startSlot, count, samplers);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            UpdateSlotOccupancy(state.psSamplerSlots, startSlot, count, samplers);
        }

        void STDMETHODCALLTYPE Hook_VSSetShader(ID3D11DeviceContext* self, ID3D11VertexShader* shader, ID3D11ClassInstance* const* classInstances, const UINT classInstanceCount)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.vsSetShader == nullptr)
            {
                return;
            }

            originals.vsSetShader(self, shader, classInstances, classInstanceCount);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            if (shader == nullptr)
            {
                state.boundVsHash.clear();
            }
            else
            {
                const auto iterator = state.shadersByPointer.find(reinterpret_cast<std::uintptr_t>(shader));
                state.boundVsHash = iterator == state.shadersByPointer.end() ? std::string() : iterator->second.hash;
            }
        }

        void EmitDrawEventLocked(const RuntimeState& state, const char* drawKind, const UINT vertexCount, const UINT indexCount, const UINT instanceCount, const UINT startVertexLocation, const INT startIndexLocation)
        {
            std::ostringstream json;
            json << BaseEventJson("draw_call")
                 << ",\"frame_index\":" << state.frameIndex
                 << ",\"draw_index\":" << state.drawCountInFrame
                 << ",\"draw_kind\":\"" << drawKind << '"'
                 << ",\"vertex_count\":" << vertexCount
                 << ",\"index_count\":" << indexCount
                 << ",\"instance_count\":" << instanceCount
                 << ",\"start_vertex_location\":" << startVertexLocation
                 << ",\"start_index_location\":" << startIndexLocation
                 << ",\"bound_vs_hash\":\"" << state.boundVsHash << '"'
                 << ",\"bound_ps_hash\":\"" << state.boundPsHash << '"'
                 << ",\"vs_srv_slots\":" << BuildSlotArrayJson(state.vsShaderResourceSlots)
                 << ",\"ps_srv_slots\":" << BuildSlotArrayJson(state.psShaderResourceSlots)
                 << ",\"vs_cb_slots\":" << BuildSlotArrayJson(state.vsConstantBufferSlots)
                 << ",\"ps_cb_slots\":" << BuildSlotArrayJson(state.psConstantBufferSlots)
                 << ",\"ps_sampler_slots\":" << BuildSlotArrayJson(state.psSamplerSlots)
                 << ",\"blend_state_id\":\"" << state.currentBlendStateId << '"'
                 << ",\"depth_stencil_state_id\":\"" << state.currentDepthStencilStateId << '"'
                 << ",\"rasterizer_state_id\":\"" << state.currentRasterizerStateId << '"'
                 << '}';
            EventLogger::Instance().WriteLine(json.str());
        }

        void STDMETHODCALLTYPE Hook_DrawIndexed(ID3D11DeviceContext* self, const UINT indexCount, const UINT startIndexLocation, const INT baseVertexLocation)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.drawIndexed == nullptr)
            {
                return;
            }

            originals.drawIndexed(self, indexCount, startIndexLocation, baseVertexLocation);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            ++state.drawCountInFrame;
            EmitDrawPathDiagnosticsLocked(state, "draw_indexed");
            if (IsCaptureActiveLocked(state))
            {
                EmitDrawEventLocked(state, "draw_indexed", 0, indexCount, 1, 0, static_cast<INT>(startIndexLocation));
            }
        }

        void STDMETHODCALLTYPE Hook_Draw(ID3D11DeviceContext* self, const UINT vertexCount, const UINT startVertexLocation)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.draw == nullptr)
            {
                return;
            }

            originals.draw(self, vertexCount, startVertexLocation);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            ++state.drawCountInFrame;
            EmitDrawPathDiagnosticsLocked(state, "draw");
            if (IsCaptureActiveLocked(state))
            {
                EmitDrawEventLocked(state, "draw", vertexCount, 0, 1, startVertexLocation, 0);
            }
        }

        void STDMETHODCALLTYPE Hook_PSSetConstantBuffers(ID3D11DeviceContext* self, const UINT startSlot, const UINT count, ID3D11Buffer* const* buffers)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.psSetConstantBuffers == nullptr)
            {
                return;
            }

            originals.psSetConstantBuffers(self, startSlot, count, buffers);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            UpdateSlotOccupancy(state.psConstantBufferSlots, startSlot, count, buffers);
        }

        void STDMETHODCALLTYPE Hook_DrawIndexedInstanced(ID3D11DeviceContext* self, const UINT indexCountPerInstance, const UINT instanceCount, const UINT startIndexLocation, const INT baseVertexLocation, const UINT startInstanceLocation)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.drawIndexedInstanced == nullptr)
            {
                return;
            }

            originals.drawIndexedInstanced(self, indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            ++state.drawCountInFrame;
            EmitDrawPathDiagnosticsLocked(state, "draw_indexed_instanced");
            if (IsCaptureActiveLocked(state))
            {
                EmitDrawEventLocked(state, "draw_indexed_instanced", 0, indexCountPerInstance, instanceCount, 0, static_cast<INT>(startIndexLocation));
            }
        }

        void STDMETHODCALLTYPE Hook_DrawInstanced(ID3D11DeviceContext* self, const UINT vertexCountPerInstance, const UINT instanceCount, const UINT startVertexLocation, const UINT startInstanceLocation)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.drawInstanced == nullptr)
            {
                return;
            }

            originals.drawInstanced(self, vertexCountPerInstance, instanceCount, startVertexLocation, startInstanceLocation);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            ++state.drawCountInFrame;
            EmitDrawPathDiagnosticsLocked(state, "draw_instanced");
            if (IsCaptureActiveLocked(state))
            {
                EmitDrawEventLocked(state, "draw_instanced", vertexCountPerInstance, 0, instanceCount, startVertexLocation, 0);
            }
        }

        void STDMETHODCALLTYPE Hook_VSSetShaderResources(ID3D11DeviceContext* self, const UINT startSlot, const UINT count, ID3D11ShaderResourceView* const* resources)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.vsSetShaderResources == nullptr)
            {
                return;
            }

            originals.vsSetShaderResources(self, startSlot, count, resources);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            UpdateSlotOccupancy(state.vsShaderResourceSlots, startSlot, count, resources);
        }

        void STDMETHODCALLTYPE Hook_OMSetBlendState(ID3D11DeviceContext* self, ID3D11BlendState* blendState, const FLOAT blendFactor[4], const UINT sampleMask)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.omSetBlendState == nullptr)
            {
                return;
            }

            originals.omSetBlendState(self, blendState, blendFactor, sampleMask);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            const auto key = reinterpret_cast<std::uintptr_t>(blendState);
            const auto summary = CaptureBlendStateSummary(blendState, blendFactor, sampleMask);
            EnsureStateDefinitionLocked(state, state.blendStateDescriptions, key, "blend", summary);
            state.currentBlendStateId = MakePointerId(blendState);
        }

        void STDMETHODCALLTYPE Hook_OMSetDepthStencilState(ID3D11DeviceContext* self, ID3D11DepthStencilState* depthStencilState, const UINT stencilRef)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.omSetDepthStencilState == nullptr)
            {
                return;
            }

            originals.omSetDepthStencilState(self, depthStencilState, stencilRef);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            const auto key = reinterpret_cast<std::uintptr_t>(depthStencilState);
            const auto summary = CaptureDepthStencilStateSummary(depthStencilState, stencilRef);
            EnsureStateDefinitionLocked(state, state.depthStencilStateDescriptions, key, "depth_stencil", summary);
            state.currentDepthStencilStateId = MakePointerId(depthStencilState);
        }

        void STDMETHODCALLTYPE Hook_RSSetState(ID3D11DeviceContext* self, ID3D11RasterizerState* rasterizerState)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.rsSetState == nullptr)
            {
                return;
            }

            originals.rsSetState(self, rasterizerState);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            const auto key = reinterpret_cast<std::uintptr_t>(rasterizerState);
            const auto summary = CaptureRasterizerStateSummary(rasterizerState);
            EnsureStateDefinitionLocked(state, state.rasterizerStateDescriptions, key, "rasterizer", summary);
            state.currentRasterizerStateId = MakePointerId(rasterizerState);
        }

        void STDMETHODCALLTYPE Hook_ExecuteCommandList(ID3D11DeviceContext* self, ID3D11CommandList* commandList, const BOOL restoreContextState)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.executeCommandList == nullptr)
            {
                return;
            }

            originals.executeCommandList(self, commandList, restoreContextState);
            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            ++state.commandListsExecutedInFrame;
            EmitCommandListEventLocked(state, "command_list_submitted", commandList, restoreContextState);
            std::ostringstream trace;
            trace << "\"command_list\":\"" << MakePointerId(commandList) << '"'
                  << ",\"restore_context_state\":" << (restoreContextState ? "true" : "false");
            EmitFirstHitTraceOnceLocked(state, state.firstExecuteCommandListSeen, "execute_command_list_seen", trace.str());
        }

        HRESULT STDMETHODCALLTYPE Hook_FinishCommandList(ID3D11DeviceContext* self, const BOOL restoreDeferredContextState, ID3D11CommandList** commandList)
        {
            const auto originals = GetContextOriginalsSnapshot(self);
            if (originals.finishCommandList == nullptr)
            {
                return E_FAIL;
            }

            const HRESULT hr = originals.finishCommandList(self, restoreDeferredContextState, commandList);
            if (FAILED(hr))
            {
                return hr;
            }

            auto& state = GetRuntimeState();
            std::scoped_lock lock(state.mutex);
            ++state.commandListsRecordedInFrame;
            EmitCommandListEventLocked(
                state,
                "command_list_recorded",
                (commandList != nullptr) ? *commandList : nullptr,
                restoreDeferredContextState,
                "\"hresult\":\"0x0\"");

            std::ostringstream trace;
            trace << "\"command_list\":\"" << MakePointerId((commandList != nullptr) ? *commandList : nullptr) << '"'
                  << ",\"restore_deferred_context_state\":" << (restoreDeferredContextState ? "true" : "false");
            EmitFirstHitTraceOnceLocked(state, state.firstFinishCommandListSeen, "finish_command_list_seen", trace.str());
            return hr;
        }

        HRESULT STDMETHODCALLTYPE Hook_Present(IDXGISwapChain* self, const UINT syncInterval, const UINT flags)
        {
            RefreshImmediateStateFromSwapChain(self);
            auto& state = GetRuntimeState();
            {
                std::scoped_lock lock(state.mutex);
                TryEmitBookmarkLocked(state);
                HandlePresentBoundaryLocked(state, syncInterval, flags, "Present");
            }

            return g_originalPresent(self, syncInterval, flags);
        }

        HRESULT STDMETHODCALLTYPE Hook_Present1(IDXGISwapChain1* self, const UINT syncInterval, const UINT flags, const DXGI_PRESENT_PARAMETERS* presentParameters)
        {
            static_cast<void>(presentParameters);

            RefreshImmediateStateFromSwapChain(self);
            auto& state = GetRuntimeState();
            {
                std::scoped_lock lock(state.mutex);
                TryEmitBookmarkLocked(state);
                HandlePresentBoundaryLocked(state, syncInterval, flags, "Present1");
            }

            return g_originalPresent1(self, syncInterval, flags, presentParameters);
        }

        void PatchDeviceHooks(ID3D11Device* device)
        {
            PatchMethod(device, DeviceCreateVertexShaderIndex, Hook_CreateVertexShader, g_originalCreateVertexShader);
            PatchMethod(device, DeviceCreateGeometryShaderIndex, Hook_CreateGeometryShader, g_originalCreateGeometryShader);
            PatchMethod(device, DeviceCreatePixelShaderIndex, Hook_CreatePixelShader, g_originalCreatePixelShader);
            PatchMethod(device, DeviceCreateHullShaderIndex, Hook_CreateHullShader, g_originalCreateHullShader);
            PatchMethod(device, DeviceCreateDomainShaderIndex, Hook_CreateDomainShader, g_originalCreateDomainShader);
            PatchMethod(device, DeviceCreateDeferredContextIndex, Hook_CreateDeferredContext, g_originalCreateDeferredContext);
        }

        void PatchContextHooks(ID3D11DeviceContext* context)
        {
            if (context == nullptr)
            {
                return;
            }

            auto& originals = GetOrCreateContextOriginals(context);
            PatchMethod(context, ContextVSSetConstantBuffersIndex, Hook_VSSetConstantBuffers, originals.vsSetConstantBuffers);
            PatchMethod(context, ContextPSSetShaderResourcesIndex, Hook_PSSetShaderResources, originals.psSetShaderResources);
            PatchMethod(context, ContextPSSetShaderIndex, Hook_PSSetShader, originals.psSetShader);
            PatchMethod(context, ContextPSSetSamplersIndex, Hook_PSSetSamplers, originals.psSetSamplers);
            PatchMethod(context, ContextVSSetShaderIndex, Hook_VSSetShader, originals.vsSetShader);
            PatchMethod(context, ContextDrawIndexedIndex, Hook_DrawIndexed, originals.drawIndexed);
            PatchMethod(context, ContextDrawIndex, Hook_Draw, originals.draw);
            PatchMethod(context, ContextPSSetConstantBuffersIndex, Hook_PSSetConstantBuffers, originals.psSetConstantBuffers);
            PatchMethod(context, ContextDrawIndexedInstancedIndex, Hook_DrawIndexedInstanced, originals.drawIndexedInstanced);
            PatchMethod(context, ContextDrawInstancedIndex, Hook_DrawInstanced, originals.drawInstanced);
            PatchMethod(context, ContextVSSetShaderResourcesIndex, Hook_VSSetShaderResources, originals.vsSetShaderResources);
            PatchMethod(context, ContextOMSetBlendStateIndex, Hook_OMSetBlendState, originals.omSetBlendState);
            PatchMethod(context, ContextOMSetDepthStencilStateIndex, Hook_OMSetDepthStencilState, originals.omSetDepthStencilState);
            PatchMethod(context, ContextRSSetStateIndex, Hook_RSSetState, originals.rsSetState);
            PatchMethod(context, ContextExecuteCommandListIndex, Hook_ExecuteCommandList, originals.executeCommandList);
            PatchMethod(context, ContextFinishCommandListIndex, Hook_FinishCommandList, originals.finishCommandList);
        }

        void PatchDeferredContextHooks(ID3D11DeviceContext* context)
        {
            if (context == nullptr)
            {
                return;
            }

            auto& originals = GetOrCreateContextOriginals(context);
            PatchMethod(context, ContextFinishCommandListIndex, Hook_FinishCommandList, originals.finishCommandList);
        }

        void PatchSwapChainHooks(IDXGISwapChain* swapChain)
        {
            PatchMethod(swapChain, SwapChainPresentIndex, Hook_Present, g_originalPresent);

            IDXGISwapChain1* swapChain1 = nullptr;
            if (SUCCEEDED(swapChain->QueryInterface(__uuidof(IDXGISwapChain1), reinterpret_cast<void**>(&swapChain1))) && swapChain1 != nullptr)
            {
                PatchMethod(swapChain1, SwapChain1Present1Index, Hook_Present1, g_originalPresent1);
                swapChain1->Release();
            }
        }
    }

    RuntimeState& GetRuntimeState()
    {
        static RuntimeState state;
        return state;
    }

    void InitializeHooksForDevice(ID3D11Device* device, ID3D11DeviceContext* immediateContext, IDXGISwapChain* swapChain)
    {
        std::ostringstream trace;
        trace << "\"device_present\":" << (device == nullptr ? "false" : "true")
              << ",\"context_present\":" << (immediateContext == nullptr ? "false" : "true")
              << ",\"swap_chain_present\":" << (swapChain == nullptr ? "false" : "true");
        EventLogger::Instance().WriteSessionTrace("hooks", "initialize_begin", trace.str());

        PatchDeviceHooks(device);
        PatchContextHooks(immediateContext);

        if (swapChain != nullptr)
        {
            PatchSwapChainHooks(swapChain);
        }

        const auto contextOriginals = GetContextOriginalsSnapshot(immediateContext);
        std::ostringstream complete;
        complete << "\"present_hooked\":" << ((swapChain != nullptr && g_originalPresent != nullptr) ? "true" : "false")
                 << ",\"present1_hooked\":" << (g_originalPresent1 != nullptr ? "true" : "false")
                 << ",\"deferred_context_hooked\":" << (g_originalCreateDeferredContext != nullptr ? "true" : "false")
                 << ",\"execute_command_list_hooked\":" << (contextOriginals.executeCommandList != nullptr ? "true" : "false")
                 << ",\"draw_hooked\":" << (contextOriginals.draw != nullptr ? "true" : "false")
                 << ",\"draw_indexed_hooked\":" << (contextOriginals.drawIndexed != nullptr ? "true" : "false")
                 << ",\"vs_hooked\":" << (contextOriginals.vsSetShader != nullptr ? "true" : "false")
                 << ",\"ps_hooked\":" << (contextOriginals.psSetShader != nullptr ? "true" : "false");
        EventLogger::Instance().WriteSessionTrace("hooks", "initialize_complete", complete.str());
    }

    std::string ComputeShaderHash(const void* bytecode, const SIZE_T bytecodeLength)
    {
        BCRYPT_ALG_HANDLE algorithm = nullptr;
        BCRYPT_HASH_HANDLE hash = nullptr;
        DWORD objectLength = 0;
        DWORD bytesWritten = 0;
        DWORD hashLength = 0;

        if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0)
        {
            return {};
        }

        BCryptGetProperty(algorithm, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&objectLength), sizeof(objectLength), &bytesWritten, 0);
        BCryptGetProperty(algorithm, BCRYPT_HASH_LENGTH, reinterpret_cast<PUCHAR>(&hashLength), sizeof(hashLength), &bytesWritten, 0);

        std::vector<BYTE> objectBuffer(objectLength);
        std::vector<BYTE> hashBytes(hashLength);

        if (BCryptCreateHash(algorithm, &hash, objectBuffer.data(), objectLength, nullptr, 0, 0) < 0)
        {
            BCryptCloseAlgorithmProvider(algorithm, 0);
            return {};
        }

        BCryptHashData(hash, const_cast<PUCHAR>(reinterpret_cast<const BYTE*>(bytecode)), static_cast<ULONG>(bytecodeLength), 0);
        BCryptFinishHash(hash, hashBytes.data(), hashLength, 0);
        BCryptDestroyHash(hash);
        BCryptCloseAlgorithmProvider(algorithm, 0);

        std::ostringstream result;
        result << std::hex << std::setfill('0');
        for (const BYTE value : hashBytes)
        {
            result << std::setw(2) << static_cast<int>(value);
        }

        return result.str();
    }

    std::string CaptureBlendStateSummary(ID3D11BlendState* state, const FLOAT blendFactor[4], const UINT sampleMask)
    {
        std::ostringstream json;
        json << '{'
             << "\"sample_mask\":" << sampleMask;
        if (blendFactor != nullptr)
        {
            json << ",\"blend_factor\":[" << blendFactor[0] << ',' << blendFactor[1] << ',' << blendFactor[2] << ',' << blendFactor[3] << ']';
        }

        if (state == nullptr)
        {
            json << ",\"state_pointer\":null}";
            return json.str();
        }

        D3D11_BLEND_DESC desc {};
        state->GetDesc(&desc);
        json << ",\"state_pointer\":\"0x" << std::hex << reinterpret_cast<std::uintptr_t>(state) << std::dec << '"'
             << ",\"alpha_to_coverage\":" << (desc.AlphaToCoverageEnable ? "true" : "false")
             << ",\"independent_blend\":" << (desc.IndependentBlendEnable ? "true" : "false")
             << ",\"targets\":[";
        for (int index = 0; index < 8; ++index)
        {
            if (index > 0)
            {
                json << ',';
            }

            const auto& target = desc.RenderTarget[index];
            json << '{'
                 << "\"blend_enable\":" << (target.BlendEnable ? "true" : "false")
                 << ",\"src_blend\":" << target.SrcBlend
                 << ",\"dest_blend\":" << target.DestBlend
                 << ",\"blend_op\":" << target.BlendOp
                 << ",\"src_blend_alpha\":" << target.SrcBlendAlpha
                 << ",\"dest_blend_alpha\":" << target.DestBlendAlpha
                 << ",\"blend_op_alpha\":" << target.BlendOpAlpha
                 << ",\"write_mask\":" << static_cast<unsigned>(target.RenderTargetWriteMask)
                 << '}';
        }

        json << "]}";
        return json.str();
    }

    std::string CaptureDepthStencilStateSummary(ID3D11DepthStencilState* state, const UINT stencilRef)
    {
        std::ostringstream json;
        json << '{'
             << "\"stencil_ref\":" << stencilRef;
        if (state == nullptr)
        {
            json << ",\"state_pointer\":null}";
            return json.str();
        }

        D3D11_DEPTH_STENCIL_DESC desc {};
        state->GetDesc(&desc);
        json << ",\"state_pointer\":\"0x" << std::hex << reinterpret_cast<std::uintptr_t>(state) << std::dec << '"'
             << ",\"depth_enable\":" << (desc.DepthEnable ? "true" : "false")
             << ",\"depth_write_mask\":" << desc.DepthWriteMask
             << ",\"depth_func\":" << desc.DepthFunc
             << ",\"stencil_enable\":" << (desc.StencilEnable ? "true" : "false")
             << ",\"stencil_read_mask\":" << static_cast<unsigned>(desc.StencilReadMask)
             << ",\"stencil_write_mask\":" << static_cast<unsigned>(desc.StencilWriteMask)
             << '}';
        return json.str();
    }

    std::string CaptureRasterizerStateSummary(ID3D11RasterizerState* state)
    {
        std::ostringstream json;
        json << '{';
        if (state == nullptr)
        {
            json << "\"state_pointer\":null}";
            return json.str();
        }

        D3D11_RASTERIZER_DESC desc {};
        state->GetDesc(&desc);
        json << "\"state_pointer\":\"0x" << std::hex << reinterpret_cast<std::uintptr_t>(state) << std::dec << '"'
             << ",\"fill_mode\":" << desc.FillMode
             << ",\"cull_mode\":" << desc.CullMode
             << ",\"front_counter_clockwise\":" << (desc.FrontCounterClockwise ? "true" : "false")
             << ",\"depth_bias\":" << desc.DepthBias
             << ",\"depth_clip_enable\":" << (desc.DepthClipEnable ? "true" : "false")
             << ",\"scissor_enable\":" << (desc.ScissorEnable ? "true" : "false")
             << ",\"multisample_enable\":" << (desc.MultisampleEnable ? "true" : "false")
             << ",\"antialiased_line_enable\":" << (desc.AntialiasedLineEnable ? "true" : "false")
             << '}';
        return json.str();
    }
}
