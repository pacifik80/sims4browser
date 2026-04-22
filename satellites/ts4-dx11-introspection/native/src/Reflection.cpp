#include "Reflection.hpp"

#include "Logger.hpp"

#include <d3dcompiler.h>

#include <sstream>

namespace ts4dx11
{
    namespace
    {
        std::string ToString(const D3D_SHADER_INPUT_TYPE type)
        {
            switch (type)
            {
            case D3D_SIT_CBUFFER: return "cbuffer";
            case D3D_SIT_TBUFFER: return "tbuffer";
            case D3D_SIT_TEXTURE: return "texture";
            case D3D_SIT_SAMPLER: return "sampler";
            case D3D_SIT_UAV_RWTYPED: return "uav_rw_typed";
            case D3D_SIT_STRUCTURED: return "structured";
            case D3D_SIT_UAV_RWSTRUCTURED: return "uav_rw_structured";
            case D3D_SIT_BYTEADDRESS: return "byte_address";
            case D3D_SIT_UAV_RWBYTEADDRESS: return "uav_rw_byte_address";
            case D3D_SIT_UAV_APPEND_STRUCTURED: return "uav_append_structured";
            case D3D_SIT_UAV_CONSUME_STRUCTURED: return "uav_consume_structured";
            case D3D_SIT_UAV_RWSTRUCTURED_WITH_COUNTER: return "uav_rw_structured_with_counter";
            default: return "other";
            }
        }

        std::string ToString(const D3D_CBUFFER_TYPE type)
        {
            switch (type)
            {
            case D3D_CT_CBUFFER: return "cbuffer";
            case D3D_CT_TBUFFER: return "tbuffer";
            case D3D_CT_INTERFACE_POINTERS: return "interface_pointers";
            case D3D_CT_RESOURCE_BIND_INFO: return "resource_bind_info";
            default: return "other";
            }
        }

        std::string ToString(const D3D_REGISTER_COMPONENT_TYPE type)
        {
            switch (type)
            {
            case D3D_REGISTER_COMPONENT_UINT32: return "uint32";
            case D3D_REGISTER_COMPONENT_SINT32: return "sint32";
            case D3D_REGISTER_COMPONENT_FLOAT32: return "float32";
            default: return "unknown";
            }
        }

        std::string ToString(const D3D_NAME type)
        {
            switch (type)
            {
            case D3D_NAME_UNDEFINED: return "undefined";
            case D3D_NAME_POSITION: return "position";
            case D3D_NAME_CLIP_DISTANCE: return "clip_distance";
            case D3D_NAME_CULL_DISTANCE: return "cull_distance";
            case D3D_NAME_RENDER_TARGET_ARRAY_INDEX: return "render_target_array_index";
            case D3D_NAME_VIEWPORT_ARRAY_INDEX: return "viewport_array_index";
            case D3D_NAME_VERTEX_ID: return "vertex_id";
            case D3D_NAME_PRIMITIVE_ID: return "primitive_id";
            case D3D_NAME_INSTANCE_ID: return "instance_id";
            case D3D_NAME_IS_FRONT_FACE: return "is_front_face";
            case D3D_NAME_SAMPLE_INDEX: return "sample_index";
            case D3D_NAME_FINAL_QUAD_EDGE_TESSFACTOR: return "final_quad_edge_tessfactor";
            case D3D_NAME_FINAL_QUAD_INSIDE_TESSFACTOR: return "final_quad_inside_tessfactor";
            case D3D_NAME_FINAL_TRI_EDGE_TESSFACTOR: return "final_tri_edge_tessfactor";
            case D3D_NAME_FINAL_TRI_INSIDE_TESSFACTOR: return "final_tri_inside_tessfactor";
            case D3D_NAME_FINAL_LINE_DETAIL_TESSFACTOR: return "final_line_detail_tessfactor";
            case D3D_NAME_FINAL_LINE_DENSITY_TESSFACTOR: return "final_line_density_tessfactor";
            case D3D_NAME_TARGET: return "target";
            case D3D_NAME_DEPTH: return "depth";
            case D3D_NAME_COVERAGE: return "coverage";
            case D3D_NAME_DEPTH_GREATER_EQUAL: return "depth_greater_equal";
            case D3D_NAME_DEPTH_LESS_EQUAL: return "depth_less_equal";
            default: return "other";
            }
        }

        std::string EscapeOrFallback(const char* value)
        {
            return value == nullptr ? std::string() : JsonEscape(value);
        }
    }

    ReflectionSummary ReflectShader(const void* bytecode, const SIZE_T bytecodeLength)
    {
        ReflectionSummary summary;
        ID3D11ShaderReflection* reflector = nullptr;
        const HRESULT hr = D3DReflect(bytecode, bytecodeLength, IID_ID3D11ShaderReflection, reinterpret_cast<void**>(&reflector));
        if (FAILED(hr) || reflector == nullptr)
        {
            summary.error = "D3DReflect failed";
            return summary;
        }

        D3D11_SHADER_DESC shaderDesc {};
        if (FAILED(reflector->GetDesc(&shaderDesc)))
        {
            reflector->Release();
            summary.error = "ID3D11ShaderReflection::GetDesc failed";
            return summary;
        }

        summary.success = true;
        summary.instructionCount = shaderDesc.InstructionCount;
        summary.tempRegisterCount = shaderDesc.TempRegisterCount;

        for (UINT index = 0; index < shaderDesc.BoundResources; ++index)
        {
            D3D11_SHADER_INPUT_BIND_DESC bindingDesc {};
            if (FAILED(reflector->GetResourceBindingDesc(index, &bindingDesc)))
            {
                continue;
            }

            summary.boundResources.push_back(ReflectionBinding {
                .name = bindingDesc.Name == nullptr ? std::string() : bindingDesc.Name,
                .type = ToString(bindingDesc.Type),
                .bindPoint = bindingDesc.BindPoint,
                .bindCount = bindingDesc.BindCount,
                .returnType = static_cast<UINT>(bindingDesc.ReturnType),
                .dimension = static_cast<UINT>(bindingDesc.Dimension),
                .sampleCount = bindingDesc.NumSamples,
            });
        }

        for (UINT bufferIndex = 0; bufferIndex < shaderDesc.ConstantBuffers; ++bufferIndex)
        {
            ID3D11ShaderReflectionConstantBuffer* constantBuffer = reflector->GetConstantBufferByIndex(bufferIndex);
            if (constantBuffer == nullptr)
            {
                continue;
            }

            D3D11_SHADER_BUFFER_DESC bufferDesc {};
            if (FAILED(constantBuffer->GetDesc(&bufferDesc)))
            {
                continue;
            }

            ReflectionConstantBuffer resultBuffer {
                .name = bufferDesc.Name == nullptr ? std::string() : bufferDesc.Name,
                .bufferType = ToString(bufferDesc.Type),
                .size = bufferDesc.Size,
                .variableCount = bufferDesc.Variables,
            };

            for (UINT variableIndex = 0; variableIndex < bufferDesc.Variables; ++variableIndex)
            {
                ID3D11ShaderReflectionVariable* variable = constantBuffer->GetVariableByIndex(variableIndex);
                if (variable == nullptr)
                {
                    continue;
                }

                D3D11_SHADER_VARIABLE_DESC variableDesc {};
                if (FAILED(variable->GetDesc(&variableDesc)))
                {
                    continue;
                }

                ID3D11ShaderReflectionType* type = variable->GetType();
                D3D11_SHADER_TYPE_DESC typeDesc {};
                if (type != nullptr)
                {
                    type->GetDesc(&typeDesc);
                }

                resultBuffer.variables.push_back(ReflectionVariable {
                    .name = variableDesc.Name == nullptr ? std::string() : variableDesc.Name,
                    .startOffset = variableDesc.StartOffset,
                    .size = variableDesc.Size,
                    .flags = variableDesc.uFlags,
                    .typeName = typeDesc.Name == nullptr ? std::string() : typeDesc.Name,
                    .rows = typeDesc.Rows,
                    .columns = typeDesc.Columns,
                    .elements = typeDesc.Elements,
                    .members = typeDesc.Members,
                });
            }

            summary.constantBuffers.push_back(std::move(resultBuffer));
        }

        for (UINT index = 0; index < shaderDesc.InputParameters; ++index)
        {
            D3D11_SIGNATURE_PARAMETER_DESC parameterDesc {};
            if (FAILED(reflector->GetInputParameterDesc(index, &parameterDesc)))
            {
                continue;
            }

            summary.inputParameters.push_back(ReflectionParameter {
                .semanticName = parameterDesc.SemanticName == nullptr ? std::string() : parameterDesc.SemanticName,
                .semanticIndex = parameterDesc.SemanticIndex,
                .registerIndex = parameterDesc.Register,
                .componentMask = parameterDesc.Mask,
                .readWriteMask = parameterDesc.ReadWriteMask,
                .systemValueType = ToString(parameterDesc.SystemValueType),
                .componentType = ToString(parameterDesc.ComponentType),
            });
        }

        for (UINT index = 0; index < shaderDesc.OutputParameters; ++index)
        {
            D3D11_SIGNATURE_PARAMETER_DESC parameterDesc {};
            if (FAILED(reflector->GetOutputParameterDesc(index, &parameterDesc)))
            {
                continue;
            }

            summary.outputParameters.push_back(ReflectionParameter {
                .semanticName = parameterDesc.SemanticName == nullptr ? std::string() : parameterDesc.SemanticName,
                .semanticIndex = parameterDesc.SemanticIndex,
                .registerIndex = parameterDesc.Register,
                .componentMask = parameterDesc.Mask,
                .readWriteMask = parameterDesc.ReadWriteMask,
                .systemValueType = ToString(parameterDesc.SystemValueType),
                .componentType = ToString(parameterDesc.ComponentType),
            });
        }

        reflector->Release();
        return summary;
    }

    std::string BuildReflectionJson(const ReflectionSummary& summary)
    {
        std::ostringstream json;
        json << '{'
             << "\"success\":" << (summary.success ? "true" : "false");

        if (!summary.error.empty())
        {
            json << ",\"error\":\"" << JsonEscape(summary.error) << '"';
        }

        json << ",\"instruction_count\":" << summary.instructionCount
             << ",\"temp_register_count\":" << summary.tempRegisterCount;

        json << ",\"bound_resources\":[";
        for (std::size_t index = 0; index < summary.boundResources.size(); ++index)
        {
            const auto& binding = summary.boundResources[index];
            if (index > 0)
            {
                json << ',';
            }

            json << '{'
                 << "\"name\":\"" << JsonEscape(binding.name) << '"'
                 << ",\"type\":\"" << JsonEscape(binding.type) << '"'
                 << ",\"bind_point\":" << binding.bindPoint
                 << ",\"bind_count\":" << binding.bindCount
                 << ",\"return_type\":" << binding.returnType
                 << ",\"dimension\":" << binding.dimension
                 << ",\"sample_count\":" << binding.sampleCount
                 << '}';
        }
        json << ']';

        json << ",\"constant_buffers\":[";
        for (std::size_t bufferIndex = 0; bufferIndex < summary.constantBuffers.size(); ++bufferIndex)
        {
            const auto& buffer = summary.constantBuffers[bufferIndex];
            if (bufferIndex > 0)
            {
                json << ',';
            }

            json << '{'
                 << "\"name\":\"" << JsonEscape(buffer.name) << '"'
                 << ",\"buffer_type\":\"" << JsonEscape(buffer.bufferType) << '"'
                 << ",\"size\":" << buffer.size
                 << ",\"variable_count\":" << buffer.variableCount
                 << ",\"variables\":[";

            for (std::size_t variableIndex = 0; variableIndex < buffer.variables.size(); ++variableIndex)
            {
                const auto& variable = buffer.variables[variableIndex];
                if (variableIndex > 0)
                {
                    json << ',';
                }

                json << '{'
                     << "\"name\":\"" << JsonEscape(variable.name) << '"'
                     << ",\"start_offset\":" << variable.startOffset
                     << ",\"size\":" << variable.size
                     << ",\"flags\":" << variable.flags
                     << ",\"type_name\":\"" << JsonEscape(variable.typeName) << '"'
                     << ",\"rows\":" << variable.rows
                     << ",\"columns\":" << variable.columns
                     << ",\"elements\":" << variable.elements
                     << ",\"members\":" << variable.members
                     << '}';
            }

            json << "]}";
        }
        json << ']';

        auto appendParameters = [&json](const char* propertyName, const std::vector<ReflectionParameter>& parameters)
        {
            json << ",\"" << propertyName << "\":[";
            for (std::size_t index = 0; index < parameters.size(); ++index)
            {
                const auto& parameter = parameters[index];
                if (index > 0)
                {
                    json << ',';
                }

                json << '{'
                     << "\"semantic_name\":\"" << JsonEscape(parameter.semanticName) << '"'
                     << ",\"semantic_index\":" << parameter.semanticIndex
                     << ",\"register_index\":" << parameter.registerIndex
                     << ",\"component_mask\":" << parameter.componentMask
                     << ",\"read_write_mask\":" << parameter.readWriteMask
                     << ",\"system_value_type\":\"" << JsonEscape(parameter.systemValueType) << '"'
                     << ",\"component_type\":\"" << JsonEscape(parameter.componentType) << '"'
                     << '}';
            }

            json << ']';
        };

        appendParameters("input_parameters", summary.inputParameters);
        appendParameters("output_parameters", summary.outputParameters);
        json << '}';
        return json.str();
    }
}
