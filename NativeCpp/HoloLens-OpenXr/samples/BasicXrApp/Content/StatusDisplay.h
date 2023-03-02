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

#pragma once

#include "ShaderStructures.h"

#include <string>
#include <mutex>

#include <DWrite.h>
#include <d2d1.h>
#include <d2d1_2.h>
#include <dwrite_2.h>
#include <wrl/client.h>

using namespace HolographicApp;

class StatusDisplay {
public:
    // Available text formats
    enum TextFormat : uint32_t {
        Small = 0,
        Large,
        LargeBold,

        TextFormatCount
    };

    // Available text colors
    enum TextColor : uint32_t {
        White = 0,
        Yellow,
        Red,
        Green,

        TextColorCount
    };

    // A single line in the status display with all its properties
    struct Line {
        std::wstring text;
        TextFormat format = Large;
        TextColor color = White;
        float lineHeightMultiplier = 1.0f;
        bool alignBottom = false;
    };

public:
    StatusDisplay(ID3D11Device* device);
    ~StatusDisplay();

    void Update();

    void Render(ID3D11DeviceContext* deviceContext);

    void CreateDeviceDependentResources(ID3D11Device* device);
    void ReleaseDeviceDependentResources();

    // Clear all lines
    void ClearLines();

    // Set a new set of lines replacing the existing ones
    void SetLines(winrt::array_view<Line> lines);

    // Update the text of a single line
    void UpdateLineText(size_t index, std::wstring text);

    // Add a new line returning the index of the new line
    size_t AddLine(const Line& line);

    // Check if a line with the given index exists
    bool HasLine(size_t index);

    void SetTextEnabled(bool enabled) {
        m_textEnabled = enabled;
    }

    // Repositions the status display
    void PositionDisplay(const XrPosef& pose);

private:
    // Runtime representation of a text line.
    struct RuntimeLine {
        winrt::com_ptr<IDWriteTextLayout> layout = nullptr;
        DWRITE_TEXT_METRICS metrics = {};
        std::wstring text = {};
        TextFormat format = Large;
        TextColor color = White;
        float lineHeightMultiplier = 1.0f;
        bool alignBottom = false;
    };

    void CreateFonts();
    void CreateBrushes();
    void UpdateLineInternal(RuntimeLine& runtimLine, const Line& line);

    Microsoft::WRL::ComPtr<ID2D1Factory2> m_d2dFactory;
    Microsoft::WRL::ComPtr<IDWriteFactory2> m_dwriteFactory;
    // winrt::com_ptr<IWICImagingFactory2> m_wicFactory;

    winrt::com_ptr<ID2D1SolidColorBrush> m_brushes[TextColorCount] = {};
    winrt::com_ptr<IDWriteTextFormat> m_textFormats[TextFormatCount] = {};
    std::vector<RuntimeLine> m_lines;
    std::mutex m_lineMutex;

    // Resources related to text rendering.
    winrt::com_ptr<ID3D11Texture2D> m_textTexture;
    winrt::com_ptr<ID3D11ShaderResourceView> m_textShaderResourceView;
    winrt::com_ptr<ID3D11RenderTargetView> m_textRenderTarget;
    winrt::com_ptr<ID2D1RenderTarget> m_d2dTextRenderTarget;

    // Direct3D resources for quad geometry.
    winrt::com_ptr<ID3D11InputLayout> m_inputLayout;
    winrt::com_ptr<ID3D11Buffer> m_vertexBufferText;
    winrt::com_ptr<ID3D11Buffer> m_indexBuffer;
    winrt::com_ptr<ID3D11VertexShader> m_vertexShader;
    winrt::com_ptr<ID3D11GeometryShader> m_geometryShader;
    winrt::com_ptr<ID3D11PixelShader> m_pixelShader;
    winrt::com_ptr<ID3D11Buffer> m_modelConstantBuffer;

    winrt::com_ptr<ID3D11SamplerState> m_textSamplerState;
    winrt::com_ptr<ID3D11BlendState> m_textAlphaBlendState;

    // System resources for quad geometry.
    ModelConstantBuffer m_modelConstantBufferDataText = {};
    uint32_t m_indexCount = 0;

    // Variables used with the rendering loop.
    float m_degreesPerSecond = 45.0f;
    std::optional<XrPosef> m_poseText;

    // If the current D3D Device supports VPRT, we can avoid using a geometry
    // shader just to set the render target array index.
    bool m_usingVprtShaders = false;

    // This is the rate at which the hologram position is interpolated to the current location.
    const float c_lerpRate = 2.0f;

    bool m_textEnabled = true;
};
