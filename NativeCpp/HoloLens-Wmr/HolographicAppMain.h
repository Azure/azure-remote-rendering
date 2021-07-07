#pragma once

//
// Comment out this preprocessor definition to disable all of the
// sample content.
//
// To remove the content after disabling it:
//     * Remove the unused code from your app's Main class.
//     * Delete the Content folder provided with this template.
//
#define DRAW_SAMPLE_CONTENT

#include "Common/DeviceResources.h"
#include "Common/StepTimer.h"
#include "Content/StatusDisplay.h"

#ifdef DRAW_SAMPLE_CONTENT
#include "Content/SpinningCubeRenderer.h"
#include "Content/SpatialInputHandler.h"
#endif

#ifdef USE_REMOTE_RENDERING
#undef min
#undef max
#include <AzureRemoteRendering.h>
namespace RR = Microsoft::Azure::RemoteRendering;
#endif


// Updates, renders, and presents holographic content using Direct3D.
namespace HolographicApp
{
    // Our application's possible states:
    enum class AppConnectionStatus
    {
        Disconnected,

        CreatingSession,
        StartingSession,
        Connecting,
        Connected,

        // error state:
        ConnectionFailed,
    };

    class HolographicAppMain : public DX::IDeviceNotify
    {
    public:
        HolographicAppMain(std::shared_ptr<DX::DeviceResources> const& deviceResources);
        ~HolographicAppMain();

        // Sets the holographic space. This is our closest analogue to setting a new window
        // for the app.
        void SetHolographicSpace(winrt::Windows::Graphics::Holographic::HolographicSpace const& holographicSpace);

        // Starts the holographic frame and updates the content.
        winrt::Windows::Graphics::Holographic::HolographicFrame Update(winrt::Windows::Graphics::Holographic::HolographicFrame const& previousFrame);

        // Renders holograms, including world-locked content.
        bool Render(winrt::Windows::Graphics::Holographic::HolographicFrame const& holographicFrame);

        // Handle saving and loading of app state owned by AppMain.
        void SaveAppState();
        void LoadAppState();

        // Handle mouse input.
        void OnPointerPressed();

        // IDeviceNotify
        void OnDeviceLost() override;
        void OnDeviceRestored() override;

    #ifdef USE_REMOTE_RENDERING
        void OnConnectionStatusChanged(RR::ConnectionStatus status, RR::Result error);
        void SetNewState(AppConnectionStatus state, const char* statusMsg);
        void SetNewSession(RR::ApiHandle<RR::RenderingSession> newSession);
        void StartModelLoading();
        void UpdateStatusText();
    #endif

    private:
        // Asynchronously creates resources for new holographic cameras.
        void OnCameraAdded(
            winrt::Windows::Graphics::Holographic::HolographicSpace const& sender,
            winrt::Windows::Graphics::Holographic::HolographicSpaceCameraAddedEventArgs const& args);

        // Synchronously releases resources for holographic cameras that are no longer
        // attached to the system.
        void OnCameraRemoved(
            winrt::Windows::Graphics::Holographic::HolographicSpace const& sender,
            winrt::Windows::Graphics::Holographic::HolographicSpaceCameraRemovedEventArgs const& args);

        // Used to notify the app when the positional tracking state changes.
        void OnLocatabilityChanged(
            winrt::Windows::Perception::Spatial::SpatialLocator const& sender,
            winrt::Windows::Foundation::IInspectable const& args);

        // Used to be aware of gamepads that are plugged in after the app starts.
        void OnGamepadAdded(winrt::Windows::Foundation::IInspectable, winrt::Windows::Gaming::Input::Gamepad const& args);

        // Used to stop looking for gamepads that are removed while the app is running.
        void OnGamepadRemoved(winrt::Windows::Foundation::IInspectable, winrt::Windows::Gaming::Input::Gamepad const& args);

        // Used to respond to changes to the default spatial locator.
        void OnHolographicDisplayIsAvailableChanged(winrt::Windows::Foundation::IInspectable, winrt::Windows::Foundation::IInspectable);

        // Clears event registration state. Used when changing to a new HolographicSpace
        // and when tearing down AppMain.
        void UnregisterHolographicEventHandlers();

#ifdef DRAW_SAMPLE_CONTENT
        // Renders a colorful holographic cube that's 20 centimeters wide. This sample content
        // is used to demonstrate world-locked rendering.
        std::unique_ptr<SpinningCubeRenderer>                       m_spinningCubeRenderer;

        // Listens for the Pressed spatial input event.
        std::shared_ptr<SpatialInputHandler>                        m_spatialInputHandler;
#endif

        // Cached pointer to device resources.
        std::shared_ptr<DX::DeviceResources>                        m_deviceResources;

        // Render loop timer.
        DX::StepTimer                                               m_timer;

        // Represents the holographic space around the user.
        winrt::Windows::Graphics::Holographic::HolographicSpace     m_holographicSpace = nullptr;

        // SpatialLocator that is attached to the default HolographicDisplay.
        winrt::Windows::Perception::Spatial::SpatialLocator         m_spatialLocator = nullptr;

        // A stationary reference frame based on m_spatialLocator.
        winrt::Windows::Perception::Spatial::SpatialStationaryFrameOfReference m_stationaryReferenceFrame = nullptr;

        // Event registration tokens.
        winrt::event_token                                          m_cameraAddedToken;
        winrt::event_token                                          m_cameraRemovedToken;
        winrt::event_token                                          m_locatabilityChangedToken;
        winrt::event_token                                          m_gamepadAddedEventToken;
        winrt::event_token                                          m_gamepadRemovedEventToken;
        winrt::event_token                                          m_holographicDisplayIsAvailableChangedEventToken;

        // Keep track of gamepads.
        struct GamepadWithButtonState
        {
            winrt::Windows::Gaming::Input::Gamepad gamepad;
            bool buttonAWasPressedLastFrame = false;
        };
        std::vector<GamepadWithButtonState>                         m_gamepads;

        // Keep track of mouse input.
        bool                                                        m_pointerPressed = false;

        // Cache whether or not the HolographicCamera.Display property can be accessed.
        bool                                                        m_canGetHolographicDisplayForCamera = false;

        // Cache whether or not the HolographicDisplay.GetDefault() method can be called.
        bool                                                        m_canGetDefaultHolographicDisplay = false;

        // Cache whether or not the HolographicCameraRenderingParameters.CommitDirect3D11DepthBuffer() method can be called.
        bool                                                        m_canCommitDirect3D11DepthBuffer = false;
        // Cache whether or not the HolographicFrame.WaitForNextFrameReady() method can be called.
        bool                                                        m_canUseWaitForNextFrameReadyAPI = false;

#ifdef USE_REMOTE_RENDERING
        // Session related:
        std::string m_sessionOverride;
        RR::ApiHandle<RR::RemoteRenderingClient> m_client;
        RR::ApiHandle<RR::RenderingSession> m_session;
        RR::ApiHandle<RR::RenderingConnection> m_api;
        RR::ApiHandle<RR::GraphicsBindingWmrD3d11> m_graphicsBinding;

        // Model loading:
        std::string m_modelURI;

        // Connection state machine:
        AppConnectionStatus m_currentStatus = AppConnectionStatus::Disconnected;
        std::string m_statusMsg;
        RR::Result m_connectionResult = RR::Result::Success;
        RR::Result m_modelLoadResult = RR::Result::Success;
        bool m_isConnected = false;
        bool m_sessionStarted = false;
        bool m_modelLoadTriggered = false;
        bool m_sessionPropertiesQueryInProgress = false;
        float m_modelLoadingProgress = 0.f;
        bool m_modelLoadFinished = false;
        bool m_needsStatusUpdate = true;
        bool m_needsCoordinateSystemUpdate = true;
        double m_timeAtLastRESTCall = 0;

        // Status text:
        double m_lastTime = -1;
        double m_sessionStartingTime = 0.0;
        std::unique_ptr<StatusDisplay> m_statusDisplay;

#endif


   };
}
