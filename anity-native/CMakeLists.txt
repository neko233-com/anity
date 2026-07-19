# Anity Native Engine — C++ core aligning with Unity 2022.3 Pro native modules
# Build: cmake -S anity-native -B anity-native/build && cmake --build anity-native/build
cmake_minimum_required(VERSION 3.20)
project(anity_native VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

option(ANITY_NATIVE_SHARED "Build shared library" ON)
if(APPLE)
  # Apple player/editor builds use Metal. Keeping Vulkan enabled by default here
  # can accidentally bind the engine dylib to an Android Emulator copy of
  # libvulkan, which is not a redistributable runtime dependency. Developers
  # can still opt in explicitly for MoltenVK validation.
  set(ANITY_ENABLE_VULKAN_DEFAULT OFF)
else()
  set(ANITY_ENABLE_VULKAN_DEFAULT ON)
endif()
option(ANITY_ENABLE_VULKAN "Enable Vulkan backend" ${ANITY_ENABLE_VULKAN_DEFAULT})
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
  src/math/anity_matrix.cpp
  src/transform/anity_transform.cpp
  src/animation/anity_avatar.cpp
  src/animation/anity_avatar_mask.cpp
  src/ui/anity_ui_renderer.cpp
  src/graphics/anity_graphics.cpp
  src/graphics/anity_graphics_ui.cpp
  src/graphics/anity_graphics_texture.cpp
  src/graphics/anity_graphics_vfx.cpp
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
else()
  # Windows/Linux: Metal symbols as stubs so CreateDevice(Metal) soft-fails cleanly
  target_sources(anity_native PRIVATE src/graphics/backends/metal_backend_stub.cpp)
endif()

if(WIN32 AND ANITY_ENABLE_D3D11)
  target_compile_definitions(anity_native PRIVATE ANITY_HAS_D3D11=1)
  target_link_libraries(anity_native PRIVATE d3d11 dxgi d3dcompiler)
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
  # ANativeWindow surface for Vulkan
  find_library(ANDROID_LIB android)
  if(ANDROID_LIB)
    target_link_libraries(anity_native PRIVATE ${ANDROID_LIB})
  endif()
elseif(UNIX)
  target_compile_definitions(anity_native PRIVATE ANITY_OS_LINUX=1)
  # Optional X11 surface for Vulkan (desktop Linux)
  find_package(X11 QUIET)
  if(X11_FOUND)
    target_compile_definitions(anity_native PRIVATE ANITY_HAS_X11=1)
    target_include_directories(anity_native PRIVATE ${X11_INCLUDE_DIR})
    target_link_libraries(anity_native PRIVATE ${X11_LIBRARIES})
    message(STATUS "Vulkan X11 surface enabled")
  else()
    message(STATUS "X11 not found — Vulkan X11 surface disabled")
  endif()
  # Optional Wayland surface
  find_package(PkgConfig QUIET)
  if(PkgConfig_FOUND)
    pkg_check_modules(WAYLAND_CLIENT QUIET wayland-client)
    if(WAYLAND_CLIENT_FOUND)
      target_compile_definitions(anity_native PRIVATE ANITY_HAS_WAYLAND=1)
      target_include_directories(anity_native PRIVATE ${WAYLAND_CLIENT_INCLUDE_DIRS})
      target_link_libraries(anity_native PRIVATE ${WAYLAND_CLIENT_LIBRARIES})
      message(STATUS "Vulkan Wayland surface enabled")
    endif()
  endif()
endif()

set_target_properties(anity_native PROPERTIES
  OUTPUT_NAME "anity_native"
  VERSION ${PROJECT_VERSION}
  SOVERSION 0
  WINDOWS_EXPORT_ALL_SYMBOLS ON
)

if(WIN32)
  # MinGW defaults to lib*.dll; Unity-style Windows load name is anity_native.dll.
  set_target_properties(anity_native PROPERTIES PREFIX "")
endif()

install(TARGETS anity_native
  RUNTIME DESTINATION bin
  LIBRARY DESTINATION lib
  ARCHIVE DESTINATION lib
)
install(DIRECTORY include/ DESTINATION include)
