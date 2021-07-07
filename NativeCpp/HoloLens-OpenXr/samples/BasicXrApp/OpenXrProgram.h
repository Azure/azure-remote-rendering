// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#ifdef USE_REMOTE_RENDERING
#pragma warning(disable : 4100)
#pragma warning(disable : 4505)
#undef min
#undef max
#include <AzureRemoteRendering.h>
#include <d3d11_4.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
namespace RR = Microsoft::Azure::RemoteRendering;
extern "C" void ForceD3D11Device(winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice& device);

// Our application's possible states:
enum class AppConnectionStatus {
    Disconnected,

    CreatingSession,
    StartingSession,
    Connecting,
    Connected,

    // error state:
    ConnectionFailed,
};
#endif

class Timer {
public:
    Timer() {
        using namespace std::chrono;
        m_startTime = high_resolution_clock::now();
    }
    float GetTotalSeconds() const {
        using namespace std::chrono;
        duration<float> seconds = high_resolution_clock::now() - m_startTime;
        return seconds.count();
    }

private:
    std::chrono::steady_clock::time_point m_startTime;
};

namespace sample {
    struct Cube {
        xr::SpaceHandle Space{};
        std::optional<XrPosef> PoseInSpace{}; // Relative pose in cube Space. Default to identity.
        XrVector3f Scale{0.1f, 0.1f, 0.1f};

        XrPosef PoseInAppSpace = xr::math::Pose::Identity(); // Cube pose in app space that gets updated every frame
    };

    struct IOpenXrProgram {
        virtual ~IOpenXrProgram() = default;
        virtual void Run() = 0;
#ifdef USE_REMOTE_RENDERING
        virtual void RenderARR(ID3D11DeviceContext* context) = 0;
#endif
    };

    struct IGraphicsPluginD3D11 {
        virtual ~IGraphicsPluginD3D11() = default;

        // Create an instance of this graphics api for the provided instance and systemId.
        virtual ID3D11Device* InitializeDevice(LUID adapterLuid, const std::vector<D3D_FEATURE_LEVEL>& featureLevels) = 0;

        // List of color pixel formats supported by this app.
        virtual const std::vector<DXGI_FORMAT>& SupportedColorFormats() const = 0;
        virtual const std::vector<DXGI_FORMAT>& SupportedDepthFormats() const = 0;

        // Render to swapchain images using stereo image array
        virtual void RenderView(
#ifdef USE_REMOTE_RENDERING
            IOpenXrProgram* program,
#endif
            const XrRect2Di& imageRect,
            const float renderTargetClearColor[4],
            const std::vector<xr::math::ViewProjection>& viewProjections,
            DXGI_FORMAT colorSwapchainFormat,
            ID3D11Texture2D* colorTexture,
            DXGI_FORMAT depthSwapchainFormat,
            ID3D11Texture2D* depthTexture,
            const std::vector<const sample::Cube*>& cubes) = 0;
    };

    std::unique_ptr<IGraphicsPluginD3D11> CreateCubeGraphics();
    std::unique_ptr<IOpenXrProgram> CreateOpenXrProgram(std::string applicationName, std::unique_ptr<IGraphicsPluginD3D11> graphicsPlugin);

} // namespace sample
