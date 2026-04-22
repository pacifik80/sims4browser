#pragma once

#include <d3d11.h>

#include <string>
#include <vector>

namespace ts4dx11
{
    struct ReflectionBinding
    {
        std::string name;
        std::string type;
        UINT bindPoint;
        UINT bindCount;
        UINT returnType;
        UINT dimension;
        UINT sampleCount;
    };

    struct ReflectionVariable
    {
        std::string name;
        UINT startOffset;
        UINT size;
        UINT flags;
        std::string typeName;
        UINT rows;
        UINT columns;
        UINT elements;
        UINT members;
    };

    struct ReflectionConstantBuffer
    {
        std::string name;
        std::string bufferType;
        UINT size;
        UINT variableCount;
        std::vector<ReflectionVariable> variables;
    };

    struct ReflectionParameter
    {
        std::string semanticName;
        UINT semanticIndex;
        UINT registerIndex;
        UINT componentMask;
        UINT readWriteMask;
        std::string systemValueType;
        std::string componentType;
    };

    struct ReflectionSummary
    {
        bool success = false;
        std::string error;
        UINT instructionCount = 0;
        UINT tempRegisterCount = 0;
        std::vector<ReflectionBinding> boundResources;
        std::vector<ReflectionConstantBuffer> constantBuffers;
        std::vector<ReflectionParameter> inputParameters;
        std::vector<ReflectionParameter> outputParameters;
    };

    ReflectionSummary ReflectShader(const void* bytecode, SIZE_T bytecodeLength);
    std::string BuildReflectionJson(const ReflectionSummary& summary);
}
