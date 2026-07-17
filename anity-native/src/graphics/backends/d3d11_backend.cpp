#define ANITY_NATIVE_BUILD
#include "../anity_graphics_device.h"
#include "../anity_graphics_texture_internal.h"
#include "../../ui/anity_ui_renderer_internal.h"
#include <algorithm>
#include <cfloat>
#include <climits>
#include <cstddef>
#include <cmath>
#include <cstring>
#include <limits>
#include <mutex>
#include <new>
#include <unordered_map>
#include <vector>

#if defined(ANITY_HAS_D3D11) && defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dxgi.h>

struct D3D11TextureResource {
  ID3D11Texture2D* texture = nullptr;
  ID3D11ShaderResourceView* view = nullptr;
  ID3D11SamplerState* sampler = nullptr;
};

struct D3D11State {
  ID3D11Device* device = nullptr;
  ID3D11DeviceContext* context = nullptr;
  IDXGISwapChain* swapChain = nullptr;
  ID3D11RenderTargetView* rtv = nullptr;
  ID3D11Texture2D* backBuffer = nullptr;
  DXGI_FORMAT format = DXGI_FORMAT_R8G8B8A8_UNORM;
  bool hdr = false;
  ID3D11Buffer* uiVertexBuffers[3]{};
  ID3D11Buffer* uiIndexBuffers[3]{};
  UINT uiVertexCapacities[3]{};
  UINT uiIndexCapacities[3]{};
  UINT uiIndexLengths[3]{};
  ID3D11Query* uiFences[3]{};
  bool uiFencePending[3]{};
  ID3D11Texture2D* uiOffscreenTexture = nullptr;
  ID3D11RenderTargetView* uiOffscreenRTV = nullptr;
  UINT uiOffscreenWidth = 0;
  UINT uiOffscreenHeight = 0;
  ID3D11VertexShader* uiVertexShader = nullptr;
  ID3D11PixelShader* uiPixelShader = nullptr;
  ID3D11InputLayout* uiInputLayout = nullptr;
  ID3D11Buffer* uiViewportBuffer = nullptr;
  ID3D11BlendState* uiBlendState = nullptr;
  ID3D11RasterizerState* uiRasterizerState = nullptr;
  ID3D11DepthStencilState* uiDepthState = nullptr;
  std::mutex textureMutex;
  std::unordered_map<uint64_t, D3D11TextureResource> textures;
  D3D11TextureResource whiteTexture;
};

static_assert(offsetof(AnityUIPackedVertex, color) == 12,
              "D3D11 UI vertex ABI color offset changed");
static_assert(offsetof(AnityUIPackedVertex, uv0) == 16,
              "D3D11 UI vertex ABI UV0 offset changed");
static_assert(sizeof(AnityUIPackedVertex) == 108,
              "D3D11 UI vertex ABI stride changed");

static void ReleaseTextureResource(D3D11TextureResource& resource) {
  if (resource.sampler) resource.sampler->Release();
  if (resource.view) resource.view->Release();
  if (resource.texture) resource.texture->Release();
  resource = {};
}

static D3D11_TEXTURE_ADDRESS_MODE ToD3D11AddressMode(int32_t wrapMode) {
  switch (wrapMode) {
    case 1: return D3D11_TEXTURE_ADDRESS_CLAMP;
    case 2: return D3D11_TEXTURE_ADDRESS_MIRROR;
    case 3: return D3D11_TEXTURE_ADDRESS_MIRROR_ONCE;
    default: return D3D11_TEXTURE_ADDRESS_WRAP;
  }
}

static D3D11_FILTER ToD3D11Filter(int32_t filterMode) {
  switch (filterMode) {
    case 0: return D3D11_FILTER_MIN_MAG_MIP_POINT;
    case 2: return D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    default: return D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT;
  }
}

static AnityResult CreateTextureResource(
    D3D11State* st, const AnityGraphicsTextureDesc& desc,
    const uint8_t* pixels, D3D11TextureResource& outResource) {
  if (!st || !st->device || !pixels || desc.width <= 0 || desc.height <= 0)
    return ANITY_ERR_INVALID_ARG;
  outResource = {};
  D3D11_TEXTURE2D_DESC textureDesc{};
  textureDesc.Width = static_cast<UINT>(desc.width);
  textureDesc.Height = static_cast<UINT>(desc.height);
  textureDesc.MipLevels = static_cast<UINT>(desc.mipCount);
  textureDesc.ArraySize = 1;
  textureDesc.Format = desc.linear != 0
      ? DXGI_FORMAT_R8G8B8A8_UNORM : DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
  textureDesc.SampleDesc.Count = 1;
  textureDesc.Usage = D3D11_USAGE_IMMUTABLE;
  textureDesc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
  std::vector<D3D11_SUBRESOURCE_DATA> initialData(static_cast<size_t>(desc.mipCount));
  size_t byteOffset = 0;
  int32_t mipWidth = desc.width;
  int32_t mipHeight = desc.height;
  for (int32_t mip = 0; mip < desc.mipCount; ++mip) {
    initialData[static_cast<size_t>(mip)].pSysMem = pixels + byteOffset;
    initialData[static_cast<size_t>(mip)].SysMemPitch = static_cast<UINT>(mipWidth) * 4u;
    byteOffset += static_cast<size_t>(mipWidth) * static_cast<size_t>(mipHeight) * 4u;
    mipWidth = std::max(1, mipWidth >> 1);
    mipHeight = std::max(1, mipHeight >> 1);
  }
  HRESULT hr = st->device->CreateTexture2D(
      &textureDesc, initialData.data(), &outResource.texture);
  if (FAILED(hr)) return ANITY_ERR_OUT_OF_MEMORY;
  hr = st->device->CreateShaderResourceView(
      outResource.texture, nullptr, &outResource.view);
  if (FAILED(hr)) {
    ReleaseTextureResource(outResource);
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  D3D11_SAMPLER_DESC samplerDesc{};
  samplerDesc.Filter = ToD3D11Filter(desc.filterMode);
  samplerDesc.AddressU = ToD3D11AddressMode(desc.wrapU);
  samplerDesc.AddressV = ToD3D11AddressMode(desc.wrapV);
  samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
  samplerDesc.MaxAnisotropy = 1;
  samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
  samplerDesc.MinLOD = 0.0f;
  samplerDesc.MaxLOD = FLT_MAX;
  hr = st->device->CreateSamplerState(&samplerDesc, &outResource.sampler);
  if (FAILED(hr)) {
    ReleaseTextureResource(outResource);
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  return ANITY_OK;
}

static AnityResult EnsureWhiteTexture(D3D11State* st) {
  if (!st) return ANITY_ERR_INVALID_ARG;
  if (st->whiteTexture.texture) return ANITY_OK;
  AnityGraphicsTextureDesc desc{};
  desc.width = 1;
  desc.height = 1;
  desc.mipCount = 1;
  desc.filterMode = 1;
  desc.wrapU = 1;
  desc.wrapV = 1;
  desc.linear = 1;
  const uint8_t white[4] = {255, 255, 255, 255};
  return CreateTextureResource(st, desc, white, st->whiteTexture);
}

static AnityResult WaitUIFence(D3D11State* st, int32_t ringIndex) {
  if (!st || ringIndex < 0 || ringIndex >= 3) return ANITY_ERR_INVALID_ARG;
  if (!st->uiFencePending[ringIndex] || !st->uiFences[ringIndex]) return ANITY_OK;
  for (;;) {
    HRESULT result = st->context->GetData(st->uiFences[ringIndex], nullptr, 0, 0);
    if (result == S_OK) break;
    if (result != S_FALSE) return ANITY_ERR_DEVICE_LOST;
    Sleep(0);
  }
  st->uiFencePending[ringIndex] = false;
  return ANITY_OK;
}

static AnityResult EnsureUIFence(D3D11State* st, int32_t ringIndex) {
  if (st->uiFences[ringIndex]) return ANITY_OK;
  D3D11_QUERY_DESC desc{};
  desc.Query = D3D11_QUERY_EVENT;
  return SUCCEEDED(st->device->CreateQuery(&desc, &st->uiFences[ringIndex]))
      ? ANITY_OK : ANITY_ERR_OUT_OF_MEMORY;
}

static AnityResult EnsureUIRenderTarget(D3D11State* st, UINT width, UINT height,
                                        ID3D11RenderTargetView** outRTV) {
  if (!st || !outRTV || width == 0 || height == 0) return ANITY_ERR_INVALID_ARG;
  if (st->rtv) {
    *outRTV = st->rtv;
    return ANITY_OK;
  }
  if (st->uiOffscreenRTV && st->uiOffscreenWidth == width &&
      st->uiOffscreenHeight == height) {
    *outRTV = st->uiOffscreenRTV;
    return ANITY_OK;
  }
  if (st->uiOffscreenRTV) st->uiOffscreenRTV->Release();
  if (st->uiOffscreenTexture) st->uiOffscreenTexture->Release();
  st->uiOffscreenRTV = nullptr;
  st->uiOffscreenTexture = nullptr;
  D3D11_TEXTURE2D_DESC desc{};
  desc.Width = width;
  desc.Height = height;
  desc.MipLevels = 1;
  desc.ArraySize = 1;
  desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
  desc.SampleDesc.Count = 1;
  desc.Usage = D3D11_USAGE_DEFAULT;
  desc.BindFlags = D3D11_BIND_RENDER_TARGET;
  if (FAILED(st->device->CreateTexture2D(&desc, nullptr, &st->uiOffscreenTexture)))
    return ANITY_ERR_OUT_OF_MEMORY;
  if (FAILED(st->device->CreateRenderTargetView(
          st->uiOffscreenTexture, nullptr, &st->uiOffscreenRTV))) {
    st->uiOffscreenTexture->Release();
    st->uiOffscreenTexture = nullptr;
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  st->uiOffscreenWidth = width;
  st->uiOffscreenHeight = height;
  *outRTV = st->uiOffscreenRTV;
  return ANITY_OK;
}

static AnityResult EnsureUIPipeline(D3D11State* st) {
  if (st->uiVertexShader && st->uiPixelShader && st->uiInputLayout)
    return ANITY_OK;
  static const char* source =
      "cbuffer Viewport : register(b0) { float2 viewportSize; float2 padding; };"
      "Texture2D mainTexture : register(t0); Texture2D alphaTexture : register(t1);"
      "SamplerState mainSampler : register(s0); SamplerState alphaSampler : register(s1);"
      "struct VSInput { float3 position : POSITION; float4 color : COLOR0; float4 uv0 : TEXCOORD0; };"
      "struct VSOutput { float4 position : SV_POSITION; float4 color : COLOR0; float2 uv : TEXCOORD0; };"
      "VSOutput VSMain(VSInput input) { VSOutput output;"
      "float2 ndc=float2((input.position.x/viewportSize.x)*2.0-1.0,"
      "1.0-(input.position.y/viewportSize.y)*2.0);"
      "output.position=float4(ndc,0.0,1.0); output.color=input.color; output.uv=input.uv0.xy; return output; }"
      "float4 PSMain(VSOutput input) : SV_TARGET {"
      "float4 color=input.color*mainTexture.Sample(mainSampler,input.uv);"
      "color.a*=alphaTexture.Sample(alphaSampler,input.uv).r; return color; }";
  ID3DBlob* vertexBlob = nullptr;
  ID3DBlob* pixelBlob = nullptr;
  ID3DBlob* errors = nullptr;
  HRESULT hr = D3DCompile(source, std::strlen(source), "AnityUI", nullptr, nullptr,
      "VSMain", "vs_4_0", 0, 0, &vertexBlob, &errors);
  if (errors) errors->Release();
  if (FAILED(hr) || !vertexBlob) return ANITY_ERR_NOT_SUPPORTED;
  errors = nullptr;
  hr = D3DCompile(source, std::strlen(source), "AnityUI", nullptr, nullptr,
      "PSMain", "ps_4_0", 0, 0, &pixelBlob, &errors);
  if (errors) errors->Release();
  if (FAILED(hr) || !pixelBlob) {
    vertexBlob->Release();
    return ANITY_ERR_NOT_SUPPORTED;
  }
  hr = st->device->CreateVertexShader(vertexBlob->GetBufferPointer(),
      vertexBlob->GetBufferSize(), nullptr, &st->uiVertexShader);
  if (SUCCEEDED(hr))
    hr = st->device->CreatePixelShader(pixelBlob->GetBufferPointer(),
        pixelBlob->GetBufferSize(), nullptr, &st->uiPixelShader);
  D3D11_INPUT_ELEMENT_DESC layout[] = {
    { "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0,
      D3D11_INPUT_PER_VERTEX_DATA, 0 },
    { "COLOR", 0, DXGI_FORMAT_R8G8B8A8_UNORM, 0, 12,
      D3D11_INPUT_PER_VERTEX_DATA, 0 },
    { "TEXCOORD", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 16,
      D3D11_INPUT_PER_VERTEX_DATA, 0 }
  };
  if (SUCCEEDED(hr))
    hr = st->device->CreateInputLayout(layout, 3, vertexBlob->GetBufferPointer(),
        vertexBlob->GetBufferSize(), &st->uiInputLayout);
  vertexBlob->Release();
  pixelBlob->Release();
  if (FAILED(hr)) return ANITY_ERR_NOT_SUPPORTED;

  D3D11_BUFFER_DESC cb{};
  cb.ByteWidth = 16;
  cb.Usage = D3D11_USAGE_DYNAMIC;
  cb.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
  cb.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
  if (FAILED(st->device->CreateBuffer(&cb, nullptr, &st->uiViewportBuffer)))
    return ANITY_ERR_OUT_OF_MEMORY;
  D3D11_BLEND_DESC blend{};
  blend.RenderTarget[0].BlendEnable = TRUE;
  blend.RenderTarget[0].SrcBlend = D3D11_BLEND_SRC_ALPHA;
  blend.RenderTarget[0].DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
  blend.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
  blend.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
  blend.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
  blend.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
  blend.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
  if (FAILED(st->device->CreateBlendState(&blend, &st->uiBlendState)))
    return ANITY_ERR_OUT_OF_MEMORY;
  D3D11_RASTERIZER_DESC raster{};
  raster.FillMode = D3D11_FILL_SOLID;
  raster.CullMode = D3D11_CULL_NONE;
  raster.DepthClipEnable = TRUE;
  raster.ScissorEnable = TRUE;
  if (FAILED(st->device->CreateRasterizerState(&raster, &st->uiRasterizerState)))
    return ANITY_ERR_OUT_OF_MEMORY;
  D3D11_DEPTH_STENCIL_DESC depth{};
  depth.DepthEnable = FALSE;
  depth.StencilEnable = FALSE;
  if (FAILED(st->device->CreateDepthStencilState(&depth, &st->uiDepthState)))
    return ANITY_ERR_OUT_OF_MEMORY;
  return ANITY_OK;
}

static UINT GrowCapacity(UINT required) {
  UINT capacity = 4096;
  while (capacity < required && capacity <= UINT_MAX / 2) capacity *= 2;
  return capacity < required ? required : capacity;
}

static AnityResult UploadDynamicBuffer(
    D3D11State* st, ID3D11Buffer** buffer, UINT* capacity,
    const void* data, UINT bytes, UINT bindFlags) {
  if (bytes == 0) return ANITY_OK;
  if (!data || !st || !st->device || !st->context) return ANITY_ERR_INVALID_ARG;
  if (!*buffer || *capacity < bytes) {
    if (*buffer) { (*buffer)->Release(); *buffer = nullptr; }
    *capacity = GrowCapacity(bytes);
    D3D11_BUFFER_DESC desc{};
    desc.ByteWidth = *capacity;
    desc.Usage = D3D11_USAGE_DYNAMIC;
    desc.BindFlags = bindFlags;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
    if (FAILED(st->device->CreateBuffer(&desc, nullptr, buffer)))
      return ANITY_ERR_OUT_OF_MEMORY;
  }
  D3D11_MAPPED_SUBRESOURCE mapped{};
  if (FAILED(st->context->Map(*buffer, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped)))
    return ANITY_ERR_DEVICE_LOST;
  std::memcpy(mapped.pData, data, bytes);
  st->context->Unmap(*buffer, 0);
  return ANITY_OK;
}

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
  for (int i = 0; i < 3; ++i) {
    WaitUIFence(st, i);
    if (st->uiVertexBuffers[i]) st->uiVertexBuffers[i]->Release();
    if (st->uiIndexBuffers[i]) st->uiIndexBuffers[i]->Release();
    if (st->uiFences[i]) st->uiFences[i]->Release();
  }
  if (st->uiVertexShader) st->uiVertexShader->Release();
  if (st->uiPixelShader) st->uiPixelShader->Release();
  if (st->uiInputLayout) st->uiInputLayout->Release();
  if (st->uiViewportBuffer) st->uiViewportBuffer->Release();
  if (st->uiBlendState) st->uiBlendState->Release();
  if (st->uiRasterizerState) st->uiRasterizerState->Release();
  if (st->uiDepthState) st->uiDepthState->Release();
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    for (auto& entry : st->textures) ReleaseTextureResource(entry.second);
    st->textures.clear();
    ReleaseTextureResource(st->whiteTexture);
  }
  if (st->uiOffscreenRTV) st->uiOffscreenRTV->Release();
  if (st->uiOffscreenTexture) st->uiOffscreenTexture->Release();
  if (st->rtv) st->rtv->Release();
  if (st->backBuffer) st->backBuffer->Release();
  if (st->swapChain) st->swapChain->Release();
  if (st->context) st->context->Release();
  if (st->device) st->device->Release();
  delete st;
  if (device) device->backend = nullptr;
}

extern "C" AnityResult AnityGraphics_D3D11_UploadUI(
    AnityGraphicsDevice* device, int32_t ringIndex,
    const void* vertices, int32_t vertexBytes,
    const void* indices, int32_t indexBytes) {
  auto* st = GetState(device);
  if (!st || ringIndex < 0 || ringIndex >= 3 || vertexBytes < 0 || indexBytes < 0)
    return ANITY_ERR_INVALID_ARG;
  AnityResult result = WaitUIFence(st, ringIndex);
  if (result != ANITY_OK) return result;
  result = UploadDynamicBuffer(st, &st->uiVertexBuffers[ringIndex],
      &st->uiVertexCapacities[ringIndex], vertices, static_cast<UINT>(vertexBytes),
      D3D11_BIND_VERTEX_BUFFER);
  if (result != ANITY_OK) return result;
  result = UploadDynamicBuffer(st, &st->uiIndexBuffers[ringIndex],
      &st->uiIndexCapacities[ringIndex], indices, static_cast<UINT>(indexBytes),
      D3D11_BIND_INDEX_BUFFER);
  if (result == ANITY_OK) st->uiIndexLengths[ringIndex] = static_cast<UINT>(indexBytes);
  return result;
}

extern "C" AnityResult AnityGraphics_D3D11_DrawUI(
    AnityGraphicsDevice* device, int32_t ringIndex,
    const AnityUIDrawPacket* packets, int32_t packetCount, int32_t* outDrawCount) {
  auto* st = GetState(device);
  if (!st || ringIndex < 0 || ringIndex >= 3 || packetCount < 0 ||
      (packetCount > 0 && !packets) || !outDrawCount) return ANITY_ERR_INVALID_ARG;
  *outDrawCount = 0;
  const UINT width = static_cast<UINT>(device->swapchain && device->swapchain->width > 0
      ? device->swapchain->width : device->width);
  const UINT height = static_cast<UINT>(device->swapchain && device->swapchain->height > 0
      ? device->swapchain->height : device->height);
  ID3D11RenderTargetView* target = nullptr;
  AnityResult result = EnsureUIRenderTarget(st, width, height, &target);
  if (result != ANITY_OK) return result;
  result = EnsureUIPipeline(st);
  if (result != ANITY_OK) return result;
  result = EnsureWhiteTexture(st);
  if (result != ANITY_OK) return result;
  const uint64_t indexCapacity = st->uiIndexLengths[ringIndex] / sizeof(uint32_t);
  for (int32_t index = 0; index < packetCount; ++index) {
    const AnityUIDrawPacket& packet = packets[index];
    if ((packet.info.flags & (ANITY_UI_COMMAND_MASK | ANITY_UI_COMMAND_POP)) != 0)
      continue;
    const uint64_t end = static_cast<uint64_t>(packet.firstIndex) +
        static_cast<uint64_t>(packet.info.indexCount);
    if (packet.info.indexCount <= 0 || end > indexCapacity)
      return ANITY_ERR_INVALID_ARG;
  }

  float clear[4] = { 0.f, 0.f, 0.f, 0.f };
  st->context->OMSetRenderTargets(1, &target, nullptr);
  st->context->ClearRenderTargetView(target, clear);
  D3D11_MAPPED_SUBRESOURCE mapped{};
  if (FAILED(st->context->Map(st->uiViewportBuffer, 0,
          D3D11_MAP_WRITE_DISCARD, 0, &mapped))) return ANITY_ERR_DEVICE_LOST;
  float viewportData[4] = { static_cast<float>(width), static_cast<float>(height), 0, 0 };
  std::memcpy(mapped.pData, viewportData, sizeof(viewportData));
  st->context->Unmap(st->uiViewportBuffer, 0);
  UINT stride = sizeof(AnityUIPackedVertex);
  UINT offset = 0;
  st->context->IASetInputLayout(st->uiInputLayout);
  st->context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
  st->context->IASetVertexBuffers(0, 1, &st->uiVertexBuffers[ringIndex], &stride, &offset);
  st->context->IASetIndexBuffer(st->uiIndexBuffers[ringIndex], DXGI_FORMAT_R32_UINT, 0);
  st->context->VSSetShader(st->uiVertexShader, nullptr, 0);
  st->context->VSSetConstantBuffers(0, 1, &st->uiViewportBuffer);
  st->context->PSSetShader(st->uiPixelShader, nullptr, 0);
  st->context->RSSetState(st->uiRasterizerState);
  st->context->OMSetBlendState(st->uiBlendState, nullptr, 0xffffffffu);
  st->context->OMSetDepthStencilState(st->uiDepthState, 0);
  D3D11_VIEWPORT viewport{};
  viewport.Width = static_cast<float>(width);
  viewport.Height = static_cast<float>(height);
  viewport.MaxDepth = 1.f;
  st->context->RSSetViewports(1, &viewport);
  for (int32_t index = 0; index < packetCount; ++index) {
    const AnityUIDrawPacket& packet = packets[index];
    if ((packet.info.flags & (ANITY_UI_COMMAND_MASK | ANITY_UI_COMMAND_POP)) != 0)
      continue;
    D3D11_RECT scissor{0, 0, static_cast<LONG>(width), static_cast<LONG>(height)};
    if ((packet.info.flags & ANITY_UI_COMMAND_RECT_CLIP) != 0) {
      scissor.left = std::max<LONG>(0, static_cast<LONG>(std::floor(packet.clipXMin)));
      scissor.top = std::max<LONG>(0, static_cast<LONG>(std::floor(packet.clipYMin)));
      scissor.right = std::min<LONG>(static_cast<LONG>(width),
          static_cast<LONG>(std::ceil(packet.clipXMax)));
      scissor.bottom = std::min<LONG>(static_cast<LONG>(height),
          static_cast<LONG>(std::ceil(packet.clipYMax)));
      if (scissor.right <= scissor.left || scissor.bottom <= scissor.top) continue;
    }
    st->context->RSSetScissorRects(1, &scissor);
    ID3D11ShaderResourceView* views[2] = {
      st->whiteTexture.view, st->whiteTexture.view
    };
    ID3D11SamplerState* samplers[2] = {
      st->whiteTexture.sampler, st->whiteTexture.sampler
    };
    {
      std::lock_guard<std::mutex> lock(st->textureMutex);
      auto main = st->textures.find(packet.info.textureId);
      if (main != st->textures.end()) {
        views[0] = main->second.view;
        samplers[0] = main->second.sampler;
      }
      auto alpha = st->textures.find(packet.info.alphaTextureId);
      if (alpha != st->textures.end()) {
        views[1] = alpha->second.view;
        samplers[1] = alpha->second.sampler;
      }
      st->context->PSSetShaderResources(0, 2, views);
      st->context->PSSetSamplers(0, 2, samplers);
    }
    st->context->DrawIndexed(static_cast<UINT>(packet.info.indexCount),
        packet.firstIndex, 0);
    (*outDrawCount)++;
  }
  result = EnsureUIFence(st, ringIndex);
  if (result != ANITY_OK) return result;
  st->context->End(st->uiFences[ringIndex]);
  st->uiFencePending[ringIndex] = true;
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_D3D11_SyncTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  auto* st = GetState(device);
  if (!st || textureId == 0) return ANITY_ERR_INVALID_ARG;
  AnityGraphicsTextureSnapshot snapshot;
  if (!AnityGraphics_CopyTextureSnapshot(device, textureId, snapshot))
    return ANITY_ERR_INVALID_ARG;
  D3D11TextureResource replacement;
  AnityResult result = CreateTextureResource(
      st, snapshot.info.desc, snapshot.rgba8.data(), replacement);
  if (result != ANITY_OK) return result;
  ID3D11Texture2D* nativeHandle = replacement.texture;
  D3D11TextureResource previous;
  try {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    auto entry = st->textures.try_emplace(textureId);
    previous = entry.first->second;
    entry.first->second = replacement;
    replacement = {};
  } catch (const std::bad_alloc&) {
    ReleaseTextureResource(replacement);
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  ReleaseTextureResource(previous);
  AnityGraphics_SetTextureBackendState(
      device, textureId, nativeHandle, 3);
  return ANITY_OK;
}

extern "C" void AnityGraphics_D3D11_DestroyTexture(
    AnityGraphicsDevice* device, uint64_t textureId) {
  auto* st = GetState(device);
  if (!st || textureId == 0) return;
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    auto found = st->textures.find(textureId);
    if (found != st->textures.end()) {
      ReleaseTextureResource(found->second);
      st->textures.erase(found);
    }
  }
  AnityGraphics_SetTextureBackendState(device, textureId, nullptr, 0);
}

extern "C" AnityResult AnityGraphics_D3D11_ReadbackSwapchainRGBA8(
    AnitySwapchain* swapchain, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten) {
  if (!swapchain || !swapchain->device || pixelCapacity < 0 || !outWritten)
    return ANITY_ERR_INVALID_ARG;
  *outWritten = 0;
  auto* st = GetState(swapchain->device);
  if (!st || !st->uiOffscreenTexture) return ANITY_ERR_NOT_SUPPORTED;
  const uint64_t required64 = static_cast<uint64_t>(st->uiOffscreenWidth) *
      static_cast<uint64_t>(st->uiOffscreenHeight) * 4u;
  if (required64 > static_cast<uint64_t>(std::numeric_limits<int32_t>::max()))
    return ANITY_ERR_OUT_OF_MEMORY;
  const int32_t required = static_cast<int32_t>(required64);
  *outWritten = required;
  if (pixelCapacity < required || (required > 0 && !pixels))
    return ANITY_ERR_INVALID_ARG;
  for (int i = 0; i < 3; ++i) {
    AnityResult result = WaitUIFence(st, i);
    if (result != ANITY_OK) return result;
  }
  D3D11_TEXTURE2D_DESC sourceDesc{};
  st->uiOffscreenTexture->GetDesc(&sourceDesc);
  sourceDesc.Usage = D3D11_USAGE_STAGING;
  sourceDesc.BindFlags = 0;
  sourceDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
  ID3D11Texture2D* staging = nullptr;
  if (FAILED(st->device->CreateTexture2D(&sourceDesc, nullptr, &staging)))
    return ANITY_ERR_OUT_OF_MEMORY;
  st->context->CopyResource(staging, st->uiOffscreenTexture);
  D3D11_MAPPED_SUBRESOURCE mapped{};
  if (FAILED(st->context->Map(staging, 0, D3D11_MAP_READ, 0, &mapped))) {
    staging->Release();
    return ANITY_ERR_DEVICE_LOST;
  }
  const size_t rowBytes = static_cast<size_t>(st->uiOffscreenWidth) * 4u;
  for (UINT row = 0; row < st->uiOffscreenHeight; ++row)
    std::memcpy(pixels + static_cast<size_t>(row) * rowBytes,
        static_cast<const uint8_t*>(mapped.pData) + static_cast<size_t>(row) * mapped.RowPitch,
        rowBytes);
  st->context->Unmap(staging, 0);
  staging->Release();
  return ANITY_OK;
}

#else

extern "C" AnityResult AnityGraphics_CreateD3D11(
    const AnityGraphicsDeviceDesc*, AnityGraphicsDevice**) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_D3D11_BeginFrame(AnityGraphicsDevice*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" AnityResult AnityGraphics_D3D11_Present(AnityGraphicsDevice*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" void AnityGraphics_D3D11_Destroy(AnityGraphicsDevice*) {}
extern "C" AnityResult AnityGraphics_D3D11_UploadUI(
    AnityGraphicsDevice*, int32_t, const void*, int32_t, const void*, int32_t) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_D3D11_DrawUI(
    AnityGraphicsDevice*, int32_t, const AnityUIDrawPacket*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_D3D11_ReadbackSwapchainRGBA8(
    AnitySwapchain*, uint8_t*, int32_t, int32_t*) {
  return ANITY_ERR_NOT_SUPPORTED;
}
extern "C" AnityResult AnityGraphics_D3D11_SyncTexture(
    AnityGraphicsDevice*, uint64_t) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" void AnityGraphics_D3D11_DestroyTexture(
    AnityGraphicsDevice*, uint64_t) {}

#endif
