#pragma once

#include <Windows.h>
#include <d3d11.h>
#include <dxgi.h>
#include <dxgi1_2.h>

#include <cstdint>
#include <array>
#include <mutex>
#include <string>
#include <unordered_map>

namespace ts4dx11
{
    struct ShaderMetadata
    {
        std::string hash;
        std::string stage;
        SIZE_T bytecodeSize;
    };

    struct RuntimeState
    {
        std::mutex mutex;
        std::unordered_map<std::uintptr_t, ShaderMetadata> shadersByPointer;
        std::unordered_map<std::uintptr_t, std::string> blendStateDescriptions;
        std::unordered_map<std::uintptr_t, std::string> depthStencilStateDescriptions;
        std::unordered_map<std::uintptr_t, std::string> rasterizerStateDescriptions;
        std::uint64_t frameIndex = 0;
        std::uint64_t drawCountInFrame = 0;
        std::uint64_t commandListsRecordedInFrame = 0;
        std::uint64_t commandListsExecutedInFrame = 0;
        std::string boundVsHash;
        std::string boundPsHash;
        std::string currentBlendStateId;
        std::string currentDepthStencilStateId;
        std::string currentRasterizerStateId;
        std::array<bool, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT> vsShaderResourceSlots {};
        std::array<bool, D3D11_COMMONSHADER_INPUT_RESOURCE_SLOT_COUNT> psShaderResourceSlots {};
        std::array<bool, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> vsConstantBufferSlots {};
        std::array<bool, D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> psConstantBufferSlots {};
        std::array<bool, D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT> psSamplerSlots {};
        bool loggerActivationSignalPlayed = false;
        bool firstPresentSeen = false;
        bool firstPresent1Seen = false;
        bool firstDrawSeen = false;
        bool drawsSeenBeforeFirstPresentLogged = false;
        bool firstDeferredContextSeen = false;
        bool firstExecuteCommandListSeen = false;
        bool firstFinishCommandListSeen = false;
    };

    RuntimeState& GetRuntimeState();
    void InitializeHooksForDevice(ID3D11Device* device, ID3D11DeviceContext* immediateContext, IDXGISwapChain* swapChain);
    std::string ComputeShaderHash(const void* bytecode, SIZE_T bytecodeLength);
    std::string CaptureBlendStateSummary(ID3D11BlendState* state, const FLOAT blendFactor[4], UINT sampleMask);
    std::string CaptureDepthStencilStateSummary(ID3D11DepthStencilState* state, UINT stencilRef);
    std::string CaptureRasterizerStateSummary(ID3D11RasterizerState* state);
}
