# Anity Native Engine — C++ core aligning with Unity 2022.3 Pro native modules
# Build: cmake -S anity-native -B anity-native/build && cmake --build anity-native/build
cmake_minimum_required(VERSION 3.20)
project(anity_native VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

option(ANITY_NATIVE_SHARED "Build shared library" ON)
option(ANITY_ENABLE_VULKAN "Enable Vulkan backend" ON)
option(ANITY_ENABLE_D3D11 "Enable D3D11 backend (Windows)" ON)
option(ANITY_ENABLE_METAL "Enable Metal backend (Apple)" ON)
option(ANITY_ENABLE_HDR "Enable HDR pipeline" ON)

if(ANITY_NATIVE_SHARED)
  add_library(anity_native SHARED)
else()
  add_library(anity_native STATIC)
endif()

target_include_directories(anity_native
  PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>
  PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_sources(anity_native PRIVATE
  src/core/anity_core.cpp
  src/graphics/anity_graphics.cpp
  src/graphics/anity_hdr.cpp
  src/graphics/backends/null_backend.cpp
  src/graphics/backends/vulkan_backend.cpp
  src/graphics/backends/d3d11_backend.cpp
  src/physics/anity_physics.cpp
  src/audio/anity_audio.cpp
  src/media/anity_media.cpp
  src/jobs/anity_jobs.cpp
  src/texture/anity_texture_compress.cpp
)

if(APPLE AND ANITY_ENABLE_METAL)
  enable_language(OBJCXX)
  target_sources(anity_native PRIVATE src/graphics/backends/metal_backend.mm)
  target_compile_definitions(anity_native PRIVATE ANITY_HAS_METAL=1)
  target_link_libraries(anity_native PRIVATE "-framework Metal" "-framework Foundation" "-framework QuartzCore")
endif()

if(WIN32 AND ANITY_ENABLE_D3D11)
  target_compile_definitions(anity_native PRIVATE ANITY_HAS_D3D11=1)
  target_link_libraries(anity_native PRIVATE d3d11 dxgi)
endif()

if(ANITY_ENABLE_VULKAN)
  find_package(Vulkan QUIET)
  if(Vulkan_FOUND)
    target_compile_definitions(anity_native PRIVATE ANITY_HAS_VULKAN=1)
    target_link_libraries(anity_native PRIVATE Vulkan::Vulkan)
  else()
    message(STATUS "Vulkan SDK not found — Vulkan backend compiled as stub")
  endif()
endif()

if(ANITY_ENABLE_HDR)
  target_compile_definitions(anity_native PRIVATE ANITY_HAS_HDR=1)
endif()

# Use ANITY_OS_* macros — do not clash with AnityPlatform enum ANITY_PLATFORM_* values
if(WIN32)
  target_compile_definitions(anity_native PRIVATE ANITY_OS_WINDOWS=1)
elseif(APPLE)
  target_compile_definitions(anity_native PRIVATE ANITY_OS_APPLE=1)
elseif(ANDROID)
  target_compile_definitions(anity_native PRIVATE ANITY_OS_ANDROID=1)
elseif(UNIX)
  target_compile_definitions(anity_native PRIVATE ANITY_OS_LINUX=1)
endif()

set_target_properties(anity_native PROPERTIES
  OUTPUT_NAME "anity_native"
  VERSION ${PROJECT_VERSION}
  SOVERSION 0
  WINDOWS_EXPORT_ALL_SYMBOLS ON
  # MinGW defaults to lib*.dll; Unity-style load name is anity_native.dll
  PREFIX ""
)

install(TARGETS anity_native
  RUNTIME DESTINATION bin
  LIBRARY DESTINATION lib
  ARCHIVE DESTINATION lib
)
install(DIRECTORY include/ DESTINATION include)
