cmake_minimum_required(VERSION 3.28)

project(Ts4Dx11Native LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_library(ts4_d3d11_proxy SHARED
    d3d11.def
    src/Hooks.cpp
    src/Logger.cpp
    src/ProxyD3D11.cpp
    src/Reflection.cpp
)

target_include_directories(ts4_d3d11_proxy PRIVATE
    include
)

target_compile_definitions(ts4_d3d11_proxy PRIVATE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
    _CRT_SECURE_NO_WARNINGS
)

target_compile_options(ts4_d3d11_proxy PRIVATE
    /FS
    /W4
    /EHsc
)

target_link_libraries(ts4_d3d11_proxy PRIVATE
    bcrypt.lib
    d3dcompiler.lib
    dxguid.lib
    dxgi.lib
)

set_target_properties(ts4_d3d11_proxy PROPERTIES
    OUTPUT_NAME d3d11
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_DEBUG ${CMAKE_BINARY_DIR}/bin
    RUNTIME_OUTPUT_DIRECTORY_RELEASE ${CMAKE_BINARY_DIR}/bin
)
