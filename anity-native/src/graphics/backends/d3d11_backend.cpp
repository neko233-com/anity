#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include <cstring>
#include <new>

#if defined(ANITY_HAS_D3D11) && defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>

struct D3D11State {
  ID3D11Device* device = nullptr;
  ID3D11DeviceContext* context = nullptr;
  IDXGISwapChain* swapChain = nullptr;
  ID3D11RenderTargetView* rtv = nullptr;
  ID3D11Texture2D* backBuffer = nullptr;
  DXGI_FORMAT format = DXGI_FORMAT_R8G8B8A8_UNORM;
  bool hdr = false;
};

static D3D11State* GetState(AnityGraphicsDevice* dev) {
  return reinterpret_cast<D3D11State*>(dev ? dev->backend : nullptr);
}

extern "C" AnityResult AnityGraphics_CreateD3D11(
    const AnityGraphicsDeviceDesc* desc, AnityGraphicsDevice** outDevice) {
  if (!desc || !outDevice) return ANITY_ERR_INVALID_ARG;

  auto* st = new (std::nothrow) D3D11State();
  if (!st) return ANITY_ERR_OUT_OF_MEMORY;

  UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#ifdef _DEBUG
  flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

  D3D_FEATURE_LEVEL levels[] = {
    D3D_FEATURE_LEVEL_11_0,
    D3D_FEATURE_LEVEL_10_1,
    D3D_FEATURE_LEVEL_10_0
  };
  D3D_FEATURE_LEVEL got = D3D_FEATURE_LEVEL_11_0;

  HRESULT hr = D3D11CreateDevice(
      nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
      levels, 3, D3D11_SDK_VERSION,
      &st->device, &got, &st->context);

  if (FAILED(hr)) {
    // WARP software fallback (CI / no GPU)
    hr = D3D11CreateDevice(
        nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0,
        levels, 3, D3D11_SDK_VERSION,
        &st->device, &got, &st->context);
  }

  if (FAILED(hr) || !st->device) {
    delete st;
    return ANITY_ERR_DEVICE_LOST;
  }

  st->hdr = desc->hdrEnabled != 0;
  // HDR10 path prefers R10G10B10A2; fall back to 8-bit if no swap chain
  st->format = st->hdr ? DXGI_FORMAT_R10G10B10A2_UNORM : DXGI_FORMAT_R8G8B8A8_UNORM;

  HWND hwnd = reinterpret_cast<HWND>(desc->nativeWindow);
  if (hwnd) {
    IDXGIDevice* dxgiDevice = nullptr;
    hr = st->device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));
    if (SUCCEEDED(hr) && dxgiDevice) {
      IDXGIAdapter* adapter = nullptr;
      dxgiDevice->GetAdapter(&adapter);
      IDXGIFactory* factory = nullptr;
      if (adapter) {
        adapter->GetParent(__uuidof(IDXGIFactory), reinterpret_cast<void**>(&factory));
        adapter->Release();
      }
      dxgiDevice->Release();

      if (factory) {
        DXGI_SWAP_CHAIN_DESC scd{};
        scd.BufferCount = 2;
        scd.BufferDesc.Width = desc->width > 0 ? (UINT)desc->width : 1280;
        scd.BufferDesc.Height = desc->height > 0 ? (UINT)desc->height : 720;
        scd.BufferDesc.Format = st->format;
        scd.BufferDesc.RefreshRate.Numerator = 60;
        scd.BufferDesc.RefreshRate.Denominator = 1;
        scd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        scd.OutputWindow = hwnd;
        scd.SampleDesc.Count = desc->msaaSamples > 1 ? (UINT)desc->msaaSamples : 1;
        scd.SampleDesc.Quality = 0;
        scd.Windowed = TRUE;
        scd.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
        scd.Flags = 0;

        hr = factory->CreateSwapChain(st->device, &scd, &st->swapChain);
        factory->Release();

        if (SUCCEEDED(hr) && st->swapChain) {
          st->swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&st->backBuffer));
          if (st->backBuffer) {
            st->device->CreateRenderTargetView(st->backBuffer, nullptr, &st->rtv);
          }
        }
      }
    }
  }

  auto* dev = new (std::nothrow) AnityGraphicsDevice();
  if (!dev) {
    if (st->rtv) st->rtv->Release();
    if (st->backBuffer) st->backBuffer->Release();
    if (st->swapChain) st->swapChain->Release();
    if (st->context) st->context->Release();
    if (st->device) st->device->Release();
    delete st;
    return ANITY_ERR_OUT_OF_MEMORY;
  }

  dev->type = ANITY_GFX_D3D11;
  dev->width = desc->width > 0 ? desc->width : 1280;
  dev->height = desc->height > 0 ? desc->height : 720;
  dev->hdrEnabled = st->hdr ? 1 : 0;
  dev->msaaSamples = desc->msaaSamples;
  dev->vsync = desc->vsync;
  dev->supportsHdr = 1;
  dev->backend = st;
  *outDevice = dev;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_D3D11_BeginFrame(AnityGraphicsDevice* device) {
  auto* st = GetState(device);
  if (!st || !st->context) return ANITY_ERR_INVALID_ARG;
  if (st->rtv) {
    st->context->OMSetRenderTargets(1, &st->rtv, nullptr);
    float clear[4] = { 0.f, 0.f, 0.f, 1.f };
    st->context->ClearRenderTargetView(st->rtv, clear);
  }
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_D3D11_Present(AnityGraphicsDevice* device) {
  auto* st = GetState(device);
  if (!st) return ANITY_ERR_INVALID_ARG;
  if (st->swapChain) {
    UINT interval = device->vsync ? 1 : 0;
    st->swapChain->Present(interval, 0);
  }
  return ANITY_OK;
}

extern "C" void AnityGraphics_D3D11_Destroy(AnityGraphicsDevice* device) {
  auto* st = GetState(device);
  if (!st) return;
  if (st->rtv) st->rtv->Release();
  if (st->backBuffer) st->backBuffer->Release();
  if (st->swapChain) st->swapChain->Release();
  if (st->context) st->context->Release();
  if (st->device) st->device->Release();
  delete st;
  if (device) device->backend = nullptr;
}

#else

extern "C" AnityResult AnityGraphics_CreateD3D11(
    const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_D3D11_BeginFrame(AnityGraphicsDevice*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" AnityResult AnityGraphics_D3D11_Present(AnityGraphicsDevice*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" void AnityGraphics_D3D11_Destroy(AnityGraphicsDevice*) {}

#endif
