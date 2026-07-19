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
#include <d3d11_1.h>
#include <d3dcompiler.h>
#include <dxgi.h>

struct D3D11TextureResource {
  ID3D11Texture2D* texture = nullptr;
  ID3D11ShaderResourceView* view = nullptr;
  ID3D11SamplerState* sampler = nullptr;
};

struct D3D11CameraRenderTarget {
  int32_t width = 0;
  int32_t height = 0;
  int32_t msaaSamples = 1;
  int32_t hdrEnabled = 0;
  ID3D11Texture2D* resolveTexture = nullptr;
  ID3D11RenderTargetView* resolveRtv = nullptr;
  ID3D11Texture2D* msaaTexture = nullptr;
  ID3D11RenderTargetView* msaaRtv = nullptr;
  ID3D11Texture2D* depthTexture = nullptr;
  ID3D11DepthStencilView* depthDsv = nullptr;
  ID3D11ShaderResourceView* depthSrv = nullptr;
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
  ID3D11ComputeShader* depthCopyShader = nullptr;
  ID3D11ComputeShader* depthCopyMsaaShader = nullptr;
  std::mutex textureMutex;
  std::unordered_map<uint64_t, D3D11TextureResource> textures;
  D3D11TextureResource whiteTexture;
  std::mutex cameraTargetMutex;
  std::unordered_map<uint64_t, D3D11CameraRenderTarget> cameraTargets;
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

static void ReleaseCameraRenderTarget(D3D11CameraRenderTarget& target) {
  if (target.msaaRtv) target.msaaRtv->Release();
  if (target.msaaTexture) target.msaaTexture->Release();
  if (target.resolveRtv) target.resolveRtv->Release();
  if (target.resolveTexture) target.resolveTexture->Release();
  if (target.depthDsv) target.depthDsv->Release();
  if (target.depthSrv) target.depthSrv->Release();
  if (target.depthTexture) target.depthTexture->Release();
  target = {};
}

static D3D11_TEXTURE_ADDRESS_MODE ToD3D11AddressMode(int32_t wrapMode) {
  switch (wrapMode) {
    case 1: return D3D11_TEXTURE_ADDRESS_CLAMP;
    case 2: return D3D11_TEXTURE_ADDRESS_MIRROR;
    case 3: return D3D11_TEXTURE_ADDRESS_MIRROR_ONCE;
    default: return D3D11_TEXTURE_ADDRESS_WRAP;
  }
}

static D3D11_FILTER ToD3D11Filter(int32_t filterMode, int32_t anisoLevel) {
  if (anisoLevel > 1 && filterMode != 0)
    return D3D11_FILTER_ANISOTROPIC;
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
  samplerDesc.Filter = ToD3D11Filter(desc.filterMode, desc.anisoLevel);
  samplerDesc.AddressU = ToD3D11AddressMode(desc.wrapU);
  samplerDesc.AddressV = ToD3D11AddressMode(desc.wrapV);
  samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
  samplerDesc.MipLODBias = desc.mipMapBias;
  samplerDesc.MaxAnisotropy = static_cast<UINT>(std::max(1, desc.anisoLevel));
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

static AnityResult CreateCameraRenderTarget(D3D11State* st,
    const AnityGraphicsCameraRenderTargetDesc& desc, D3D11CameraRenderTarget& target) {
  if (!st || !st->device) return ANITY_ERR_INVALID_ARG;
  const DXGI_FORMAT format = desc.hdrEnabled != 0
      ? DXGI_FORMAT_R16G16B16A16_FLOAT : DXGI_FORMAT_R8G8B8A8_UNORM;
  D3D11_TEXTURE2D_DESC textureDesc{};
  textureDesc.Width = static_cast<UINT>(desc.width);
  textureDesc.Height = static_cast<UINT>(desc.height);
  textureDesc.MipLevels = 1;
  textureDesc.ArraySize = 1;
  textureDesc.Format = format;
  textureDesc.SampleDesc.Count = 1;
  textureDesc.Usage = D3D11_USAGE_DEFAULT;
  textureDesc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE |
      D3D11_BIND_UNORDERED_ACCESS;
  if (FAILED(st->device->CreateTexture2D(&textureDesc, nullptr, &target.resolveTexture)))
    return ANITY_ERR_OUT_OF_MEMORY;
  if (FAILED(st->device->CreateRenderTargetView(target.resolveTexture, nullptr, &target.resolveRtv))) {
    ReleaseCameraRenderTarget(target);
    return ANITY_ERR_OUT_OF_MEMORY;
  }
  if (desc.msaaSamples > 1) {
    textureDesc.SampleDesc.Count = static_cast<UINT>(desc.msaaSamples);
    textureDesc.BindFlags = D3D11_BIND_RENDER_TARGET;
    if (FAILED(st->device->CreateTexture2D(&textureDesc, nullptr, &target.msaaTexture)) ||
        FAILED(st->device->CreateRenderTargetView(target.msaaTexture, nullptr, &target.msaaRtv))) {
      ReleaseCameraRenderTarget(target);
      return ANITY_ERR_NOT_SUPPORTED;
    }
  }
  D3D11_TEXTURE2D_DESC depthDesc{};
  depthDesc.Width = static_cast<UINT>(desc.width);
  depthDesc.Height = static_cast<UINT>(desc.height);
  depthDesc.MipLevels = 1;
  depthDesc.ArraySize = 1;
  depthDesc.Format = DXGI_FORMAT_R24G8_TYPELESS;
  depthDesc.SampleDesc.Count = static_cast<UINT>(desc.msaaSamples);
  depthDesc.Usage = D3D11_USAGE_DEFAULT;
  depthDesc.BindFlags = D3D11_BIND_DEPTH_STENCIL | D3D11_BIND_SHADER_RESOURCE;
  if (FAILED(st->device->CreateTexture2D(&depthDesc, nullptr, &target.depthTexture))) {
    ReleaseCameraRenderTarget(target);
    return ANITY_ERR_NOT_SUPPORTED;
  }
  D3D11_DEPTH_STENCIL_VIEW_DESC dsvDesc{};
  dsvDesc.Format = DXGI_FORMAT_D24_UNORM_S8_UINT;
  dsvDesc.ViewDimension = desc.msaaSamples > 1 ? D3D11_DSV_DIMENSION_TEXTURE2DMS :
      D3D11_DSV_DIMENSION_TEXTURE2D;
  if (dsvDesc.ViewDimension == D3D11_DSV_DIMENSION_TEXTURE2D)
    dsvDesc.Texture2D.MipSlice = 0;
  D3D11_SHADER_RESOURCE_VIEW_DESC depthSrvDesc{};
  depthSrvDesc.Format = DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
  depthSrvDesc.ViewDimension = desc.msaaSamples > 1 ? D3D11_SRV_DIMENSION_TEXTURE2DMS :
      D3D11_SRV_DIMENSION_TEXTURE2D;
  if (depthSrvDesc.ViewDimension == D3D11_SRV_DIMENSION_TEXTURE2D) {
    depthSrvDesc.Texture2D.MostDetailedMip = 0;
    depthSrvDesc.Texture2D.MipLevels = 1;
  }
  if (FAILED(st->device->CreateDepthStencilView(target.depthTexture, &dsvDesc, &target.depthDsv)) ||
      FAILED(st->device->CreateShaderResourceView(target.depthTexture, &depthSrvDesc, &target.depthSrv))) {
    ReleaseCameraRenderTarget(target);
    return ANITY_ERR_NOT_SUPPORTED;
  }
  target.width = desc.width;
  target.height = desc.height;
  target.msaaSamples = desc.msaaSamples;
  target.hdrEnabled = desc.hdrEnabled;
  return ANITY_OK;
}

static AnityResult CopyRenderTargetRGBA8(D3D11State* st, ID3D11Texture2D* source,
    int32_t width, int32_t height, uint8_t* pixels, int32_t pixelCapacity,
    int32_t* outWritten) {
  if (!st || !source || !outWritten || pixelCapacity < 0) return ANITY_ERR_INVALID_ARG;
  const uint64_t byteCount = static_cast<uint64_t>(width) * height * 4u;
  if (byteCount > static_cast<uint64_t>(INT_MAX)) return ANITY_ERR_OUT_OF_MEMORY;
  const int32_t required = static_cast<int32_t>(byteCount);
  *outWritten = required;
  if (pixelCapacity < required || (required > 0 && !pixels)) return ANITY_ERR_INVALID_ARG;
  D3D11_TEXTURE2D_DESC desc{};
  source->GetDesc(&desc);
  if (desc.Format != DXGI_FORMAT_R8G8B8A8_UNORM || desc.SampleDesc.Count != 1)
    return ANITY_ERR_NOT_SUPPORTED;
  desc.Usage = D3D11_USAGE_STAGING;
  desc.BindFlags = 0;
  desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
  desc.MiscFlags = 0;
  ID3D11Texture2D* staging = nullptr;
  if (FAILED(st->device->CreateTexture2D(&desc, nullptr, &staging))) return ANITY_ERR_OUT_OF_MEMORY;
  st->context->CopyResource(staging, source);
  D3D11_MAPPED_SUBRESOURCE mapped{};
  if (FAILED(st->context->Map(staging, 0, D3D11_MAP_READ, 0, &mapped))) {
    staging->Release();
    return ANITY_ERR_DEVICE_LOST;
  }
  const size_t rowBytes = static_cast<size_t>(width) * 4u;
  for (int32_t row = 0; row < height; ++row)
    std::memcpy(pixels + static_cast<size_t>(row) * rowBytes,
        static_cast<const uint8_t*>(mapped.pData) + static_cast<size_t>(row) * mapped.RowPitch,
        rowBytes);
  st->context->Unmap(staging, 0);
  staging->Release();
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

/* Converts the typeless D24 camera attachment to the URP depth-texture
 * convention (linear depth in R).  The source variants deliberately use
 * Load, matching Metal/Vulkan's sample-zero MSAA contract rather than an
 * implicit resolve. */
static AnityResult EnsureDepthCopyPipelines(D3D11State* st) {
  if (!st || !st->device) return ANITY_ERR_INVALID_ARG;
  if (st->depthCopyShader && st->depthCopyMsaaShader) return ANITY_OK;
  static const char* singleSource =
      "Texture2D<float> inputDepth : register(t0);"
      "RWTexture2D<float4> outputColor : register(u0);"
      "[numthreads(8,8,1)] void CSMain(uint3 id : SV_DispatchThreadID) {"
      "uint w,h; outputColor.GetDimensions(w,h); if(id.x>=w||id.y>=h)return;"
      "float d=inputDepth.Load(int3(id.xy,0)); outputColor[id.xy]=float4(d,0,0,1); }";
  static const char* msaaSource =
      "Texture2DMS<float> inputDepth : register(t0);"
      "RWTexture2D<float4> outputColor : register(u0);"
      "[numthreads(8,8,1)] void CSMain(uint3 id : SV_DispatchThreadID) {"
      "uint w,h; outputColor.GetDimensions(w,h); if(id.x>=w||id.y>=h)return;"
      "float d=inputDepth.Load(id.xy,0); outputColor[id.xy]=float4(d,0,0,1); }";
  ID3DBlob* singleBlob = nullptr;
  ID3DBlob* msaaBlob = nullptr;
  ID3DBlob* errors = nullptr;
  HRESULT hr = D3DCompile(singleSource, std::strlen(singleSource), "AnityDepthCopy",
      nullptr, nullptr, "CSMain", "cs_5_0", 0, 0, &singleBlob, &errors);
  if (errors) errors->Release();
  if (FAILED(hr) || !singleBlob) return ANITY_ERR_NOT_SUPPORTED;
  errors = nullptr;
  hr = D3DCompile(msaaSource, std::strlen(msaaSource), "AnityDepthCopyMSAA",
      nullptr, nullptr, "CSMain", "cs_5_0", 0, 0, &msaaBlob, &errors);
  if (errors) errors->Release();
  if (FAILED(hr) || !msaaBlob) {
    singleBlob->Release();
    return ANITY_ERR_NOT_SUPPORTED;
  }
  ID3D11ComputeShader* single = nullptr;
  ID3D11ComputeShader* msaa = nullptr;
  hr = st->device->CreateComputeShader(singleBlob->GetBufferPointer(),
      singleBlob->GetBufferSize(), nullptr, &single);
  if (SUCCEEDED(hr)) hr = st->device->CreateComputeShader(msaaBlob->GetBufferPointer(),
      msaaBlob->GetBufferSize(), nullptr, &msaa);
  singleBlob->Release();
  msaaBlob->Release();
  if (FAILED(hr) || !single || !msaa) {
    if (single) single->Release();
    if (msaa) msaa->Release();
    return ANITY_ERR_NOT_SUPPORTED;
  }
  st->depthCopyShader = single;
  st->depthCopyMsaaShader = msaa;
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

extern "C" AnityResult AnityGraphics_D3D11_EnsureCameraRenderTarget(
    AnityGraphicsDevice* device, const AnityGraphicsCameraRenderTargetDesc* desc) {
  auto* st = GetState(device);
  if (!st || !desc || desc->targetId == 0) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
  auto existing = st->cameraTargets.find(desc->targetId);
  if (existing != st->cameraTargets.end() &&
      existing->second.width == desc->width && existing->second.height == desc->height &&
      existing->second.msaaSamples == desc->msaaSamples &&
      existing->second.hdrEnabled == desc->hdrEnabled)
    return ANITY_OK;
  D3D11CameraRenderTarget replacement;
  AnityResult result = CreateCameraRenderTarget(st, *desc, replacement);
  if (result != ANITY_OK) return result;
  if (existing != st->cameraTargets.end()) {
    ReleaseCameraRenderTarget(existing->second);
    existing->second = replacement;
  } else {
    st->cameraTargets.emplace(desc->targetId, replacement);
  }
  return ANITY_OK;
}

extern "C" void AnityGraphics_D3D11_DestroyCameraRenderTarget(
    AnityGraphicsDevice* device, uint64_t targetId) {
  auto* st = GetState(device);
  if (!st || targetId == 0) return;
  std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
  auto existing = st->cameraTargets.find(targetId);
  if (existing == st->cameraTargets.end()) return;
  ReleaseCameraRenderTarget(existing->second);
  st->cameraTargets.erase(existing);
}

extern "C" AnityResult AnityGraphics_D3D11_ExecuteCameraPass(
    AnityGraphicsDevice* device, const AnityGraphicsCameraPassDesc* desc) {
  auto* st = GetState(device);
  if (!st || !desc) return ANITY_ERR_INVALID_ARG;
  if ((desc->flags & ANITY_CAMERA_PASS_TARGET_IS_CAMERA_TARGET) != 0)
    return ANITY_ERR_NOT_SUPPORTED;
  std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
  auto found = st->cameraTargets.find(desc->targetId);
  if (found == st->cameraTargets.end()) return ANITY_ERR_INVALID_ARG;
  D3D11CameraRenderTarget& target = found->second;
  if (target.width != desc->targetWidth || target.height != desc->targetHeight ||
      target.msaaSamples != desc->msaaSamples ||
      (target.hdrEnabled != 0) != ((desc->flags & ANITY_CAMERA_PASS_HDR) != 0))
    return ANITY_ERR_INVALID_ARG;
  const bool fullViewport = desc->viewportX == 0.f && desc->viewportY == 0.f &&
      desc->viewportWidth == static_cast<float>(target.width) &&
      desc->viewportHeight == static_cast<float>(target.height);
  if (!fullViewport && (desc->flags & ANITY_CAMERA_PASS_CLEAR_DEPTH) != 0)
    return ANITY_ERR_NOT_SUPPORTED;
  ID3D11RenderTargetView* activeTarget = target.msaaRtv ? target.msaaRtv : target.resolveRtv;
  st->context->OMSetRenderTargets(1, &activeTarget, target.depthDsv);
  D3D11_VIEWPORT viewport{};
  viewport.TopLeftX = desc->viewportX;
  viewport.TopLeftY = desc->viewportY;
  viewport.Width = desc->viewportWidth;
  viewport.Height = desc->viewportHeight;
  viewport.MaxDepth = 1.f;
  st->context->RSSetViewports(1, &viewport);
  if ((desc->flags & ANITY_CAMERA_PASS_CLEAR_COLOR) != 0) {
    const float clear[4] = {desc->clearR, desc->clearG, desc->clearB, desc->clearA};
    if (fullViewport) {
      st->context->ClearRenderTargetView(activeTarget, clear);
    } else {
      ID3D11DeviceContext1* context1 = nullptr;
      if (FAILED(st->context->QueryInterface(__uuidof(ID3D11DeviceContext1),
              reinterpret_cast<void**>(&context1))) || !context1)
        return ANITY_ERR_NOT_SUPPORTED;
      D3D11_RECT clearRect{};
      clearRect.left = std::max<LONG>(0, static_cast<LONG>(std::floor(desc->viewportX)));
      clearRect.top = std::max<LONG>(0, static_cast<LONG>(std::floor(desc->viewportY)));
      clearRect.right = std::min<LONG>(target.width,
          static_cast<LONG>(std::ceil(desc->viewportX + desc->viewportWidth)));
      clearRect.bottom = std::min<LONG>(target.height,
          static_cast<LONG>(std::ceil(desc->viewportY + desc->viewportHeight)));
      if (clearRect.right > clearRect.left && clearRect.bottom > clearRect.top)
        context1->ClearView(activeTarget, clear, &clearRect, 1);
      context1->Release();
    }
  }
  if ((desc->flags & ANITY_CAMERA_PASS_CLEAR_DEPTH) != 0)
    st->context->ClearDepthStencilView(target.depthDsv, D3D11_CLEAR_DEPTH,
        desc->clearDepth, 0);
  if (target.msaaTexture && (desc->flags & ANITY_CAMERA_PASS_STORE_COLOR) != 0)
    st->context->ResolveSubresource(target.resolveTexture, 0, target.msaaTexture, 0,
        target.hdrEnabled ? DXGI_FORMAT_R16G16B16A16_FLOAT : DXGI_FORMAT_R8G8B8A8_UNORM);
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_D3D11_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice* device, uint64_t targetId, uint8_t* pixels,
    int32_t pixelCapacity, int32_t* outWritten) {
  auto* st = GetState(device);
  if (!st || targetId == 0) return ANITY_ERR_INVALID_ARG;
  std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
  auto found = st->cameraTargets.find(targetId);
  if (found == st->cameraTargets.end()) return ANITY_ERR_INVALID_ARG;
  D3D11CameraRenderTarget& target = found->second;
  return CopyRenderTargetRGBA8(st, target.resolveTexture, target.width, target.height,
      pixels, pixelCapacity, outWritten);
}

extern "C" AnityResult AnityGraphics_D3D11_CopyCameraRenderTargetColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId) {
  auto* st = GetState(device);
  if (!st || destinationTargetId == 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
  // D3D's swapchain CameraTarget does not yet have the required shader-copy
  // control plane. Never silently copy the backbuffer or a stale resource.
  if (sourceIsCameraTarget != 0) return ANITY_ERR_NOT_SUPPORTED;
  std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
  const auto source = st->cameraTargets.find(sourceTargetId);
  const auto destination = st->cameraTargets.find(destinationTargetId);
  if (source == st->cameraTargets.end() || destination == st->cameraTargets.end())
    return ANITY_ERR_INVALID_ARG;
  const D3D11CameraRenderTarget& sourceTarget = source->second;
  const D3D11CameraRenderTarget& destinationTarget = destination->second;
  if (!sourceTarget.resolveTexture || !destinationTarget.resolveTexture ||
      sourceTarget.width != destinationTarget.width ||
      sourceTarget.height != destinationTarget.height ||
      sourceTarget.hdrEnabled != destinationTarget.hdrEnabled)
    return ANITY_ERR_NOT_SUPPORTED;
  D3D11_TEXTURE2D_DESC sourceDesc{};
  D3D11_TEXTURE2D_DESC destinationDesc{};
  sourceTarget.resolveTexture->GetDesc(&sourceDesc);
  destinationTarget.resolveTexture->GetDesc(&destinationDesc);
  if (sourceDesc.Format != destinationDesc.Format ||
      sourceDesc.SampleDesc.Count != 1 || destinationDesc.SampleDesc.Count != 1)
    return ANITY_ERR_NOT_SUPPORTED;
  st->context->CopyResource(destinationTarget.resolveTexture, sourceTarget.resolveTexture);
  return ANITY_OK;
}

extern "C" AnityResult AnityGraphics_D3D11_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice* device, uint64_t sourceTargetId,
    int32_t sourceIsCameraTarget, uint64_t destinationTargetId) {
  auto* st = GetState(device);
  if (!st || destinationTargetId == 0 ||
      (sourceIsCameraTarget != 0 && sourceIsCameraTarget != 1) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == 0) ||
      (sourceIsCameraTarget == 0 && sourceTargetId == destinationTargetId))
    return ANITY_ERR_INVALID_ARG;
  // Backbuffer depth is not persistently owned by this backend yet. Never
  // substitute a stale depth resource for Unity's CameraTarget contract.
  if (sourceIsCameraTarget != 0) return ANITY_ERR_NOT_SUPPORTED;
  std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
  const auto source = st->cameraTargets.find(sourceTargetId);
  const auto destination = st->cameraTargets.find(destinationTargetId);
  if (source == st->cameraTargets.end() || destination == st->cameraTargets.end())
    return ANITY_ERR_INVALID_ARG;
  const D3D11CameraRenderTarget& sourceTarget = source->second;
  const D3D11CameraRenderTarget& destinationTarget = destination->second;
  if (!sourceTarget.depthSrv || !destinationTarget.resolveTexture ||
      sourceTarget.width != destinationTarget.width ||
      sourceTarget.height != destinationTarget.height || destinationTarget.hdrEnabled != 0)
    return ANITY_ERR_NOT_SUPPORTED;
  D3D11_UNORDERED_ACCESS_VIEW_DESC uavDesc{};
  uavDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
  uavDesc.ViewDimension = D3D11_UAV_DIMENSION_TEXTURE2D;
  uavDesc.Texture2D.MipSlice = 0;
  ID3D11UnorderedAccessView* output = nullptr;
  if (FAILED(st->device->CreateUnorderedAccessView(destinationTarget.resolveTexture,
          &uavDesc, &output)) || !output)
    return ANITY_ERR_NOT_SUPPORTED;
  const AnityResult pipelineResult = EnsureDepthCopyPipelines(st);
  if (pipelineResult != ANITY_OK) {
    output->Release();
    return pipelineResult;
  }
  ID3D11ShaderResourceView* input = sourceTarget.depthSrv;
  st->context->CSSetShaderResources(0, 1, &input);
  UINT initialCount = 0;
  st->context->CSSetUnorderedAccessViews(0, 1, &output, &initialCount);
  st->context->CSSetShader(sourceTarget.msaaSamples > 1 ? st->depthCopyMsaaShader :
      st->depthCopyShader, nullptr, 0);
  st->context->Dispatch(static_cast<UINT>((sourceTarget.width + 7) / 8),
      static_cast<UINT>((sourceTarget.height + 7) / 8), 1);
  ID3D11ShaderResourceView* nullSrv = nullptr;
  ID3D11UnorderedAccessView* nullUav = nullptr;
  st->context->CSSetShaderResources(0, 1, &nullSrv);
  st->context->CSSetUnorderedAccessViews(0, 1, &nullUav, &initialCount);
  st->context->CSSetShader(nullptr, nullptr, 0);
  output->Release();
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
  if (st->depthCopyShader) st->depthCopyShader->Release();
  if (st->depthCopyMsaaShader) st->depthCopyMsaaShader->Release();
  {
    std::lock_guard<std::mutex> lock(st->textureMutex);
    for (auto& entry : st->textures) ReleaseTextureResource(entry.second);
    st->textures.clear();
    ReleaseTextureResource(st->whiteTexture);
  }
  {
    std::lock_guard<std::mutex> lock(st->cameraTargetMutex);
    for (auto& entry : st->cameraTargets) ReleaseCameraRenderTarget(entry.second);
    st->cameraTargets.clear();
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
extern "C" AnityResult AnityGraphics_D3D11_EnsureCameraRenderTarget(
    AnityGraphicsDevice*, const AnityGraphicsCameraRenderTargetDesc*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" void AnityGraphics_D3D11_DestroyCameraRenderTarget(
    AnityGraphicsDevice*, uint64_t) {}
extern "C" AnityResult AnityGraphics_D3D11_ExecuteCameraPass(
    AnityGraphicsDevice*, const AnityGraphicsCameraPassDesc*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" AnityResult AnityGraphics_D3D11_ReadbackCameraRenderTargetRGBA8(
    AnityGraphicsDevice*, uint64_t, uint8_t*, int32_t, int32_t*) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" AnityResult AnityGraphics_D3D11_CopyCameraRenderTargetColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t) { return ANITY_ERR_NOT_SUPPORTED; }
extern "C" AnityResult AnityGraphics_D3D11_CopyCameraRenderTargetDepthToColor(
    AnityGraphicsDevice*, uint64_t, int32_t, uint64_t) { return ANITY_ERR_NOT_SUPPORTED; }
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
