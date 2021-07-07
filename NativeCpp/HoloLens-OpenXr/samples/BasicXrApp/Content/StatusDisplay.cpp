//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

#include "pch.h"

#pragma optimize("", off)

#include "StatusDisplay.h"

#include <shaders\GeometryShader_txt.h>
#include <shaders\PixelShader_txt.h>
#include <shaders\VPRTVertexShader_txt.h>
#include <shaders\VertexShader_txt.h>

#include <DirectXColors.h>

#define TEXTURE_WIDTH 650
#define TEXTURE_HEIGHT 650

#define Font L"Segoe UI"
#define FontSizeLarge 32.0f
#define FontSizeSmall 22.0f
#define FontLanguage L"en-US"

using namespace DirectX;
using namespace winrt::Windows::Foundation::Numerics;

// Initializes D2D resources used for text rendering.
StatusDisplay::StatusDisplay(ID3D11Device* device) {
    D2D1_FACTORY_OPTIONS options{};
#if defined(_DEBUG)
    options.debugLevel = D2D1_DEBUG_LEVEL_INFORMATION;
#endif

    // Initialize the Direct2D Factory.
    winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, __uuidof(ID2D1Factory2), &options, &m_d2dFactory));

    // Initialize the DirectWrite Factory.
    winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory2), &m_dwriteFactory));

    CreateDeviceDependentResources(device);
}

StatusDisplay::~StatusDisplay() {
    ReleaseDeviceDependentResources();
}

// Called once per frame. Rotates the quad, and calculates and sets the model matrix
// relative to the position transform indicated by hologramPositionTransform.
void StatusDisplay::Update() {
    if (m_poseText.has_value()) {
        XMStoreFloat4x4(&m_modelConstantBufferDataText.model, XMMatrixTranspose(xr::math::LoadXrPose(m_poseText.value())));
    }
}

// Renders a frame to the screen.
void StatusDisplay::Render(ID3D11DeviceContext* context) {
    // Loading is asynchronous. Resources must be created before drawing can occur.
    if ((!m_textEnabled)) {
        return;
    }

    // First render all text using direct2D
    context->ClearRenderTargetView(m_textRenderTarget.get(), DirectX::Colors::Transparent);

    m_d2dTextRenderTarget->BeginDraw();

    {
        std::scoped_lock lock(m_lineMutex);
        if (m_lines.size() > 0) {
            float top = m_lines[0].metrics.height;

            for (auto& line : m_lines) {
                if (line.alignBottom) {
                    top = TEXTURE_HEIGHT - line.metrics.height;
                }
                m_d2dTextRenderTarget->DrawTextLayout(D2D1::Point2F(0, top), line.layout.get(), m_brushes[line.color].get());
                top += line.metrics.height * line.lineHeightMultiplier;
            }
        }
    }

    // Ignore D2DERR_RECREATE_TARGET here. This error indicates that the device
    // is lost. It will be handled during the next call to Present.
    const HRESULT hr = m_d2dTextRenderTarget->EndDraw();
    if (hr != D2DERR_RECREATE_TARGET) {
        winrt::check_hresult(hr);
    }

    // Now render the quads into 3d space
    // Each vertex is one instance of the VertexPositionTexCoord struct.
    const UINT stride = sizeof(VertexPositionTexCoord);
    const UINT offset = 0;
    ID3D11Buffer* pBufferToSet = nullptr;
    context->IASetIndexBuffer(m_indexBuffer.get(), DXGI_FORMAT_R16_UINT, 0);

    context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->IASetInputLayout(m_inputLayout.get());
    context->OMSetBlendState(m_textAlphaBlendState.get(), nullptr, 0xffffffff);

    // Attach the vertex shader.
    context->VSSetShader(m_vertexShader.get(), nullptr, 0);

    // Apply the model constant buffer to the vertex shader.
    pBufferToSet = m_modelConstantBuffer.get();
    context->VSSetConstantBuffers(0, 1, &pBufferToSet);

    // On devices that do not support the D3D11_FEATURE_D3D11_OPTIONS3::
    // VPAndRTArrayIndexFromAnyShaderFeedingRasterizer optional feature,
    // a pass-through geometry shader sets the render target ID.
    context->GSSetShader(!m_usingVprtShaders ? m_geometryShader.get() : nullptr, nullptr, 0);

    // Attach the pixel shader.
    context->PSSetShader(m_pixelShader.get(), nullptr, 0);

    // Set up for rendering the texture that contains the text.
    pBufferToSet = m_vertexBufferText.get();
    context->IASetVertexBuffers(0, 1, &pBufferToSet, &stride, &offset);

    ID3D11ShaderResourceView* pShaderViewToSet = m_textShaderResourceView.get();
    context->PSSetShaderResources(0, 1, &pShaderViewToSet);

    ID3D11SamplerState* pSamplerToSet = m_textSamplerState.get();
    context->PSSetSamplers(0, 1, &pSamplerToSet);

    context->UpdateSubresource(m_modelConstantBuffer.get(), 0, nullptr, &m_modelConstantBufferDataText, 0, 0);

    // Draw the text.
    context->DrawIndexedInstanced(m_indexCount, 2, 0, 0, 0);

    // Reset the blend state.
    context->OMSetBlendState(nullptr, nullptr, 0xffffffff);

    // Detach our texture.
    ID3D11ShaderResourceView* emptyResource = nullptr;
    context->PSSetShaderResources(0, 1, &emptyResource);
}

void StatusDisplay::CreateDeviceDependentResources(ID3D11Device* device) {
    CD3D11_SAMPLER_DESC desc(D3D11_DEFAULT);

    CD3D11_TEXTURE2D_DESC textureDesc(
        DXGI_FORMAT_B8G8R8A8_UNORM, TEXTURE_WIDTH, TEXTURE_HEIGHT, 1, 1, D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET);

    m_textTexture = nullptr;
    device->CreateTexture2D(&textureDesc, nullptr, m_textTexture.put());

    m_textShaderResourceView = nullptr;
    device->CreateShaderResourceView(m_textTexture.get(), nullptr, m_textShaderResourceView.put());

    m_textRenderTarget = nullptr;
    device->CreateRenderTargetView(m_textTexture.get(), nullptr, m_textRenderTarget.put());

    D2D1_RENDER_TARGET_PROPERTIES props = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT, D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED), 96, 96);

    winrt::com_ptr<IDXGISurface> dxgiSurface;
    m_textTexture.as(dxgiSurface);

    m_d2dTextRenderTarget = nullptr;
    winrt::check_hresult(m_d2dFactory->CreateDxgiSurfaceRenderTarget(dxgiSurface.get(), &props, m_d2dTextRenderTarget.put()));

    CreateFonts();
    CreateBrushes();

    m_usingVprtShaders = false;
    {
        D3D11_FEATURE_DATA_D3D11_OPTIONS3 options;
        device->CheckFeatureSupport(D3D11_FEATURE_D3D11_OPTIONS3, &options, sizeof(options));
        if (options.VPAndRTArrayIndexFromAnyShaderFeedingRasterizer) {
            m_usingVprtShaders = true;
        }
    }

    // If the optional VPRT feature is supported by the graphics device, we
    // can avoid using geometry shaders to set the render target array index.
    const auto vertexShaderData = m_usingVprtShaders ? VPRTVertexShader_txt : VertexShader_txt;
    const auto vertexShaderDataSize = m_usingVprtShaders ? sizeof(VPRTVertexShader_txt) : sizeof(VertexShader_txt);

    // create the vertex shader and input layout.
    winrt::check_hresult(device->CreateVertexShader(vertexShaderData, vertexShaderDataSize, nullptr, m_vertexShader.put()));

    static const D3D11_INPUT_ELEMENT_DESC vertexDesc[] = {
        {"POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
        {"TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0},
    };

    winrt::check_hresult(
        device->CreateInputLayout(vertexDesc, ARRAYSIZE(vertexDesc), vertexShaderData, vertexShaderDataSize, m_inputLayout.put()));

    winrt::check_hresult(device->CreatePixelShader(PixelShader_txt, sizeof(PixelShader_txt), nullptr, m_pixelShader.put()));

    const CD3D11_BUFFER_DESC constantBufferDesc(sizeof(ModelConstantBuffer), D3D11_BIND_CONSTANT_BUFFER);
    winrt::check_hresult(device->CreateBuffer(&constantBufferDesc, nullptr, m_modelConstantBuffer.put()));

    if (!m_usingVprtShaders) {
        winrt::check_hresult(device->CreateGeometryShader(GeometryShader_txt, sizeof(GeometryShader_txt), nullptr, m_geometryShader.put()));
    }

    static const float textQuadExtent = 0.3f;
    static const VertexPositionTexCoord quadVerticesText[] = {
        {XMFLOAT3(-textQuadExtent, textQuadExtent, 0.f), XMFLOAT2(0.f, 0.f)},
        {XMFLOAT3(textQuadExtent, textQuadExtent, 0.f), XMFLOAT2(1.f, 0.f)},
        {XMFLOAT3(textQuadExtent, -textQuadExtent, 0.f), XMFLOAT2(1.f, 1.f)},
        {XMFLOAT3(-textQuadExtent, -textQuadExtent, 0.f), XMFLOAT2(0.f, 1.f)},
    };

    D3D11_SUBRESOURCE_DATA vertexBufferDataText = {0};
    vertexBufferDataText.pSysMem = quadVerticesText;
    vertexBufferDataText.SysMemPitch = 0;
    vertexBufferDataText.SysMemSlicePitch = 0;
    const CD3D11_BUFFER_DESC vertexBufferDescText(sizeof(quadVerticesText), D3D11_BIND_VERTEX_BUFFER);
    winrt::check_hresult(device->CreateBuffer(&vertexBufferDescText, &vertexBufferDataText, m_vertexBufferText.put()));

    // Load mesh indices. Each trio of indices represents
    // a triangle to be rendered on the screen.
    // For example: 2,1,0 means that the vertices with indexes
    // 2, 1, and 0 from the vertex buffer compose the
    // first triangle of this mesh.
    // Note that the winding order is clockwise by default.
    static const unsigned short quadIndices[] = {
        0,
        2,
        3, // -z
        0,
        1,
        2,
    };

    m_indexCount = ARRAYSIZE(quadIndices);

    D3D11_SUBRESOURCE_DATA indexBufferData = {0};
    indexBufferData.pSysMem = quadIndices;
    indexBufferData.SysMemPitch = 0;
    indexBufferData.SysMemSlicePitch = 0;
    const CD3D11_BUFFER_DESC indexBufferDesc(sizeof(quadIndices), D3D11_BIND_INDEX_BUFFER);
    winrt::check_hresult(device->CreateBuffer(&indexBufferDesc, &indexBufferData, m_indexBuffer.put()));

    // Create text sampler state
    {
        CD3D11_SAMPLER_DESC samplerDesc(D3D11_DEFAULT);
        winrt::check_hresult(device->CreateSamplerState(&samplerDesc, m_textSamplerState.put()));
    }

    // Create the blend state.  This sets up a blend state for pre-multiplied alpha produced by TextRenderer.cpp's Direct2D text
    // renderer.
    CD3D11_BLEND_DESC blendStateDesc(D3D11_DEFAULT);
    blendStateDesc.AlphaToCoverageEnable = FALSE;
    blendStateDesc.IndependentBlendEnable = FALSE;

    const D3D11_RENDER_TARGET_BLEND_DESC rtBlendDesc = {
        TRUE,
        D3D11_BLEND_SRC_ALPHA,
        D3D11_BLEND_INV_SRC_ALPHA,
        D3D11_BLEND_OP_ADD,
        D3D11_BLEND_INV_DEST_ALPHA,
        D3D11_BLEND_ONE,
        D3D11_BLEND_OP_ADD,
        D3D11_COLOR_WRITE_ENABLE_ALL,
    };

    for (UINT i = 0; i < D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT; ++i) {
        blendStateDesc.RenderTarget[i] = rtBlendDesc;
    }

    winrt::check_hresult(device->CreateBlendState(&blendStateDesc, m_textAlphaBlendState.put()));
}

void StatusDisplay::ReleaseDeviceDependentResources() {
    m_usingVprtShaders = false;

    m_vertexShader = nullptr;
    m_inputLayout = nullptr;
    m_pixelShader = nullptr;
    m_geometryShader = nullptr;

    m_modelConstantBuffer = nullptr;

    m_vertexBufferText = nullptr;
    m_indexBuffer = nullptr;

    m_textSamplerState = nullptr;
    m_textAlphaBlendState = nullptr;

    for (size_t i = 0; i < ARRAYSIZE(m_brushes); i++) {
        m_brushes[i] = nullptr;
    }
    for (size_t i = 0; i < ARRAYSIZE(m_textFormats); i++) {
        m_textFormats[i] = nullptr;
    }
}

void StatusDisplay::ClearLines() {
    std::scoped_lock lock(m_lineMutex);
    m_lines.resize(0);
}

void StatusDisplay::SetLines(winrt::array_view<Line> lines) {
    std::scoped_lock lock(m_lineMutex);
    auto numLines = lines.size();
    m_lines.resize(numLines);

    for (uint32_t i = 0; i < numLines; i++) {
        assert((!lines[i].alignBottom || i == numLines - 1) && "Only the last line can use alignBottom = true");
        UpdateLineInternal(m_lines[i], lines[i]);
    }
}

void StatusDisplay::UpdateLineText(size_t index, std::wstring text) {
    std::scoped_lock lock(m_lineMutex);
    assert(index < m_lines.size() && "Line index out of bounds");

    auto& runtimeLine = m_lines[index];

    Line line = {std::move(text), runtimeLine.format, runtimeLine.color, runtimeLine.lineHeightMultiplier};
    UpdateLineInternal(runtimeLine, line);
}

size_t StatusDisplay::AddLine(const Line& line) {
    std::scoped_lock lock(m_lineMutex);
    auto newIndex = m_lines.size();
    m_lines.resize(newIndex + 1);
    UpdateLineInternal(m_lines[newIndex], line);
    return newIndex;
}

bool StatusDisplay::HasLine(size_t index) {
    std::scoped_lock lock(m_lineMutex);
    return index < m_lines.size();
}

void StatusDisplay::CreateFonts() {
    // Create Large font
    m_textFormats[Large] = nullptr;
    winrt::check_hresult(m_dwriteFactory->CreateTextFormat(Font,
                                                           nullptr,
                                                           DWRITE_FONT_WEIGHT_NORMAL,
                                                           DWRITE_FONT_STYLE_NORMAL,
                                                           DWRITE_FONT_STRETCH_NORMAL,
                                                           FontSizeLarge,
                                                           FontLanguage,
                                                           m_textFormats[Large].put()));
    winrt::check_hresult(m_textFormats[Large]->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
    winrt::check_hresult(m_textFormats[Large]->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));

    // Create large bold font
    m_textFormats[LargeBold] = nullptr;
    winrt::check_hresult(m_dwriteFactory->CreateTextFormat(Font,
                                                           nullptr,
                                                           DWRITE_FONT_WEIGHT_BOLD,
                                                           DWRITE_FONT_STYLE_NORMAL,
                                                           DWRITE_FONT_STRETCH_NORMAL,
                                                           FontSizeLarge,
                                                           FontLanguage,
                                                           m_textFormats[LargeBold].put()));
    winrt::check_hresult(m_textFormats[LargeBold]->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
    winrt::check_hresult(m_textFormats[LargeBold]->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));

    // Create small font
    m_textFormats[Small] = nullptr;
    winrt::check_hresult(m_dwriteFactory->CreateTextFormat(Font,
                                                           nullptr,
                                                           DWRITE_FONT_WEIGHT_MEDIUM,
                                                           DWRITE_FONT_STYLE_NORMAL,
                                                           DWRITE_FONT_STRETCH_NORMAL,
                                                           FontSizeSmall,
                                                           FontLanguage,
                                                           m_textFormats[Small].put()));
    winrt::check_hresult(m_textFormats[Small]->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR));
    winrt::check_hresult(m_textFormats[Small]->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));

    static_assert(TextFormatCount == 3, "Expected 3 text formats");
}

void StatusDisplay::CreateBrushes() {
    m_brushes[White] = nullptr;
    winrt::check_hresult(m_d2dTextRenderTarget->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::FloralWhite), m_brushes[White].put()));

    m_brushes[Yellow] = nullptr;
    winrt::check_hresult(m_d2dTextRenderTarget->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Yellow), m_brushes[Yellow].put()));

    m_brushes[Red] = nullptr;
    winrt::check_hresult(m_d2dTextRenderTarget->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Red), m_brushes[Red].put()));

    m_brushes[Green] = nullptr;
    winrt::check_hresult(m_d2dTextRenderTarget->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::Green), m_brushes[Green].put()));
}

void StatusDisplay::UpdateLineInternal(RuntimeLine& runtimeLine, const Line& line) {
    assert(line.format >= 0 && line.format < TextFormatCount && "Line text format out of bounds");
    assert(line.color >= 0 && line.color < TextColorCount && "Line text color out of bounds");

    if (line.format != runtimeLine.format || line.text != runtimeLine.text) {
        runtimeLine.format = line.format;
        runtimeLine.text = line.text;

        runtimeLine.layout = nullptr;
        winrt::check_hresult(m_dwriteFactory->CreateTextLayout(line.text.c_str(),
                                                               static_cast<UINT32>(line.text.length()),
                                                               m_textFormats[line.format].get(),
                                                               TEXTURE_WIDTH,  // Max width of the input text.
                                                               TEXTURE_HEIGHT, // Max height of the input text.
                                                               runtimeLine.layout.put()));

        winrt::check_hresult(runtimeLine.layout->GetMetrics(&runtimeLine.metrics));
    }

    runtimeLine.color = line.color;
    runtimeLine.lineHeightMultiplier = line.lineHeightMultiplier;
    runtimeLine.alignBottom = line.alignBottom;
}

// This function uses a SpatialPointerPose to position the world-locked hologram
// two meters in front of the user's heading.
void StatusDisplay::PositionDisplay(const XrPosef& pose) {
    // Lerp the position, to keep the hologram comfortably stable.
    if (m_poseText.has_value()) {
        m_poseText = xr::math::Pose::Slerp(m_poseText.value(), pose, 0.05f);
    } else {
        m_poseText = pose;
    }
}
