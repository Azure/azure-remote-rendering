// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "OpenXrProgram.h"

#include "DxUtility.h"

// wchar_t conversion
#include <codecvt>
#include <xlocbuf>

#ifdef USE_REMOTE_RENDERING
#include "Content/StatusDisplay.h"
#include <AzureRemoteRendering.inl>
#include <RemoteRenderingExtensions.h>
#endif

namespace {
    struct ImplementOpenXrProgram : sample::IOpenXrProgram {
        ImplementOpenXrProgram(std::string applicationName, std::unique_ptr<sample::IGraphicsPluginD3D11> graphicsPlugin)
            : m_applicationName(std::move(applicationName))
            , m_graphicsPlugin(std::move(graphicsPlugin)) {
        }

#ifdef USE_REMOTE_RENDERING
        ~ImplementOpenXrProgram() {
            if (m_renderingSession != nullptr) {
                m_renderingSession->Disconnect();
                m_renderingSession = nullptr;
            }
            m_client = nullptr;

            // One-time deinitialization
            RR::ShutdownRemoteRendering();
        }
#endif

        void Run() override {
#ifdef USE_REMOTE_RENDERING
            InitARR();
#endif
            CreateInstance();
            CreateActions();

            bool requestRestart = false;
            do {
                InitializeSystem();
                InitializeSession();

                while (true) {
                    bool exitRenderLoop = false;
                    ProcessEvents(&exitRenderLoop, &requestRestart);
                    if (exitRenderLoop) {
                        break;
                    }

                    if (m_sessionRunning) {
                        PollActions();
#ifdef USE_REMOTE_RENDERING
                        UpdateARR();
#endif
                        RenderFrame();
                    } else {
                        // Throttle loop since xrWaitFrame won't be called.
                        using namespace std::chrono_literals;
                        std::this_thread::sleep_for(250ms);
                    }
                }

                if (requestRestart) {
                    PrepareSessionRestart();
                }
            } while (requestRestart);
        }

    private:
        void CreateInstance() {
            CHECK(m_instance.Get() == XR_NULL_HANDLE);

            // Build out the extensions to enable. Some extensions are required and some are optional.
            const std::vector<const char*> enabledExtensions = SelectExtensions();

            // Create the instance with enabled extensions.
            XrInstanceCreateInfo createInfo{XR_TYPE_INSTANCE_CREATE_INFO};
            createInfo.enabledExtensionCount = (uint32_t)enabledExtensions.size();
            createInfo.enabledExtensionNames = enabledExtensions.data();

            createInfo.applicationInfo = {"BasicXrApp", 1, "", 1, XR_CURRENT_API_VERSION};
            strcpy_s(createInfo.applicationInfo.applicationName, m_applicationName.c_str());
            CHECK_XRCMD(xrCreateInstance(&createInfo, m_instance.Put()));

            m_extensions.PopulateDispatchTable(m_instance.Get());
        }

        std::vector<const char*> SelectExtensions() {
            // Fetch the list of extensions supported by the runtime.
            uint32_t extensionCount;
            CHECK_XRCMD(xrEnumerateInstanceExtensionProperties(nullptr, 0, &extensionCount, nullptr));
            std::vector<XrExtensionProperties> extensionProperties(extensionCount, {XR_TYPE_EXTENSION_PROPERTIES});
            CHECK_XRCMD(xrEnumerateInstanceExtensionProperties(nullptr, extensionCount, &extensionCount, extensionProperties.data()));

            std::vector<const char*> enabledExtensions;

            // Add a specific extension to the list of extensions to be enabled, if it is supported.
            auto EnableExtensionIfSupported = [&](const char* extensionName) {
                for (uint32_t i = 0; i < extensionCount; i++) {
                    if (strcmp(extensionProperties[i].extensionName, extensionName) == 0) {
                        enabledExtensions.push_back(extensionName);
                        return true;
                    }
                }
                return false;
            };

            // D3D11 extension is required for this sample, so check if it's supported.
            CHECK(EnableExtensionIfSupported(XR_KHR_D3D11_ENABLE_EXTENSION_NAME));

#if UWP
            // Require XR_EXT_win32_appcontainer_compatible extension when building in UWP context.
            CHECK(EnableExtensionIfSupported(XR_EXT_WIN32_APPCONTAINER_COMPATIBLE_EXTENSION_NAME));
#endif

            // Additional optional extensions for enhanced functionality. Track whether enabled in m_optionalExtensions.
            m_optionalExtensions.DepthExtensionSupported = EnableExtensionIfSupported(XR_KHR_COMPOSITION_LAYER_DEPTH_EXTENSION_NAME);
            m_optionalExtensions.UnboundedRefSpaceSupported = EnableExtensionIfSupported(XR_MSFT_UNBOUNDED_REFERENCE_SPACE_EXTENSION_NAME);
            m_optionalExtensions.SpatialAnchorSupported = EnableExtensionIfSupported(XR_MSFT_SPATIAL_ANCHOR_EXTENSION_NAME);

            return enabledExtensions;
        }

        void CreateActions() {
            CHECK(m_instance.Get() != XR_NULL_HANDLE);

            // Create an action set.
            {
                XrActionSetCreateInfo actionSetInfo{XR_TYPE_ACTION_SET_CREATE_INFO};
                strcpy_s(actionSetInfo.actionSetName, "place_hologram_action_set");
                strcpy_s(actionSetInfo.localizedActionSetName, "Placement");
                CHECK_XRCMD(xrCreateActionSet(m_instance.Get(), &actionSetInfo, m_actionSet.Put()));
            }

            // Create actions.
            {
                // Enable subaction path filtering for left or right hand.
                m_subactionPaths[LeftSide] = GetXrPath("/user/hand/left");
                m_subactionPaths[RightSide] = GetXrPath("/user/hand/right");

                // Create an input action to place a hologram.
                {
                    XrActionCreateInfo actionInfo{XR_TYPE_ACTION_CREATE_INFO};
                    actionInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
                    strcpy_s(actionInfo.actionName, "place_hologram");
                    strcpy_s(actionInfo.localizedActionName, "Place Hologram");
                    actionInfo.countSubactionPaths = (uint32_t)m_subactionPaths.size();
                    actionInfo.subactionPaths = m_subactionPaths.data();
                    CHECK_XRCMD(xrCreateAction(m_actionSet.Get(), &actionInfo, m_placeAction.Put()));
                }

                // Create an input action getting the left and right hand poses.
                {
                    XrActionCreateInfo actionInfo{XR_TYPE_ACTION_CREATE_INFO};
                    actionInfo.actionType = XR_ACTION_TYPE_POSE_INPUT;
                    strcpy_s(actionInfo.actionName, "hand_pose");
                    strcpy_s(actionInfo.localizedActionName, "Hand Pose");
                    actionInfo.countSubactionPaths = (uint32_t)m_subactionPaths.size();
                    actionInfo.subactionPaths = m_subactionPaths.data();
                    CHECK_XRCMD(xrCreateAction(m_actionSet.Get(), &actionInfo, m_poseAction.Put()));
                }

                // Create an output action for vibrating the left and right controller.
                {
                    XrActionCreateInfo actionInfo{XR_TYPE_ACTION_CREATE_INFO};
                    actionInfo.actionType = XR_ACTION_TYPE_VIBRATION_OUTPUT;
                    strcpy_s(actionInfo.actionName, "vibrate");
                    strcpy_s(actionInfo.localizedActionName, "Vibrate");
                    actionInfo.countSubactionPaths = (uint32_t)m_subactionPaths.size();
                    actionInfo.subactionPaths = m_subactionPaths.data();
                    CHECK_XRCMD(xrCreateAction(m_actionSet.Get(), &actionInfo, m_vibrateAction.Put()));
                }

                // Create an input action to exit the session.
                {
                    XrActionCreateInfo actionInfo{XR_TYPE_ACTION_CREATE_INFO};
                    actionInfo.actionType = XR_ACTION_TYPE_BOOLEAN_INPUT;
                    strcpy_s(actionInfo.actionName, "exit_session");
                    strcpy_s(actionInfo.localizedActionName, "Exit session");
                    actionInfo.countSubactionPaths = (uint32_t)m_subactionPaths.size();
                    actionInfo.subactionPaths = m_subactionPaths.data();
                    CHECK_XRCMD(xrCreateAction(m_actionSet.Get(), &actionInfo, m_exitAction.Put()));
                }
            }

            // Set up suggested bindings for the simple_controller profile.
            {
                std::vector<XrActionSuggestedBinding> bindings;
                bindings.push_back({m_placeAction.Get(), GetXrPath("/user/hand/right/input/select/click")});
                bindings.push_back({m_placeAction.Get(), GetXrPath("/user/hand/left/input/select/click")});
                bindings.push_back({m_poseAction.Get(), GetXrPath("/user/hand/right/input/grip/pose")});
                bindings.push_back({m_poseAction.Get(), GetXrPath("/user/hand/left/input/grip/pose")});
                bindings.push_back({m_vibrateAction.Get(), GetXrPath("/user/hand/right/output/haptic")});
                bindings.push_back({m_vibrateAction.Get(), GetXrPath("/user/hand/left/output/haptic")});
                bindings.push_back({m_exitAction.Get(), GetXrPath("/user/hand/right/input/menu/click")});
                bindings.push_back({m_exitAction.Get(), GetXrPath("/user/hand/left/input/menu/click")});

                XrInteractionProfileSuggestedBinding suggestedBindings{XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING};
                suggestedBindings.interactionProfile = GetXrPath("/interaction_profiles/khr/simple_controller");
                suggestedBindings.suggestedBindings = bindings.data();
                suggestedBindings.countSuggestedBindings = (uint32_t)bindings.size();
                CHECK_XRCMD(xrSuggestInteractionProfileBindings(m_instance.Get(), &suggestedBindings));
            }
        }

        void InitializeSystem() {
            CHECK(m_instance.Get() != XR_NULL_HANDLE);
            CHECK(m_systemId == XR_NULL_SYSTEM_ID);

            XrSystemGetInfo systemInfo{XR_TYPE_SYSTEM_GET_INFO};
            systemInfo.formFactor = m_formFactor;
            while (true) {
                XrResult result = xrGetSystem(m_instance.Get(), &systemInfo, &m_systemId);
                if (SUCCEEDED(result)) {
                    break;
                } else if (result == XR_ERROR_FORM_FACTOR_UNAVAILABLE) {
                    DEBUG_PRINT("No headset detected.  Trying again in one second...");
                    using namespace std::chrono_literals;
                    std::this_thread::sleep_for(1s);
                } else {
                    CHECK_XRRESULT(result, "xrGetSystem");
                }
            };

            // Choose an environment blend mode.
            {
                // Query the list of supported environment blend modes for the current system.
                uint32_t count;
                CHECK_XRCMD(xrEnumerateEnvironmentBlendModes(m_instance.Get(), m_systemId, m_primaryViewConfigType, 0, &count, nullptr));
                CHECK(count > 0); // A system must support at least one environment blend mode.

                std::vector<XrEnvironmentBlendMode> environmentBlendModes(count);
                CHECK_XRCMD(xrEnumerateEnvironmentBlendModes(
                    m_instance.Get(), m_systemId, m_primaryViewConfigType, count, &count, environmentBlendModes.data()));

                // This sample supports all modes, pick the system's preferred one.
                m_environmentBlendMode = environmentBlendModes[0];
            }

            // Choosing a reasonable depth range can help improve hologram visual quality.
            // Use reversed-Z (near > far) for more uniform Z resolution.
            m_nearFar = {20.f, 0.1f};
        }

        void InitializeSession() {
            CHECK(m_instance.Get() != XR_NULL_HANDLE);
            CHECK(m_systemId != XR_NULL_SYSTEM_ID);
            CHECK(m_session.Get() == XR_NULL_HANDLE);

            // Create the D3D11 device for the adapter associated with the system.
            XrGraphicsRequirementsD3D11KHR graphicsRequirements{XR_TYPE_GRAPHICS_REQUIREMENTS_D3D11_KHR};
            CHECK_XRCMD(m_extensions.xrGetD3D11GraphicsRequirementsKHR(m_instance.Get(), m_systemId, &graphicsRequirements));

            // Create a list of feature levels which are both supported by the OpenXR runtime and this application.
            std::vector<D3D_FEATURE_LEVEL> featureLevels = {D3D_FEATURE_LEVEL_12_1,
                                                            D3D_FEATURE_LEVEL_12_0,
                                                            D3D_FEATURE_LEVEL_11_1,
                                                            D3D_FEATURE_LEVEL_11_0,
                                                            D3D_FEATURE_LEVEL_10_1,
                                                            D3D_FEATURE_LEVEL_10_0};
            featureLevels.erase(std::remove_if(featureLevels.begin(),
                                               featureLevels.end(),
                                               [&](D3D_FEATURE_LEVEL fl) { return fl < graphicsRequirements.minFeatureLevel; }),
                                featureLevels.end());
            CHECK_MSG(featureLevels.size() != 0, "Unsupported minimum feature level!");

            ID3D11Device* device = m_graphicsPlugin->InitializeDevice(graphicsRequirements.adapterLuid, featureLevels);

#ifdef USE_REMOTE_RENDERING
            m_statusDisplay = std::make_unique<StatusDisplay>(device);
#endif

            XrGraphicsBindingD3D11KHR graphicsBinding{XR_TYPE_GRAPHICS_BINDING_D3D11_KHR};
            graphicsBinding.device = device;

            XrSessionCreateInfo createInfo{XR_TYPE_SESSION_CREATE_INFO};
            createInfo.next = &graphicsBinding;
            createInfo.systemId = m_systemId;
            CHECK_XRCMD(xrCreateSession(m_instance.Get(), &createInfo, m_session.Put()));

            XrSessionActionSetsAttachInfo attachInfo{XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO};
            std::vector<XrActionSet> actionSets = {m_actionSet.Get()};
            attachInfo.countActionSets = (uint32_t)actionSets.size();
            attachInfo.actionSets = actionSets.data();
            CHECK_XRCMD(xrAttachSessionActionSets(m_session.Get(), &attachInfo));

            CreateSpaces();
            CreateSwapchains();
        }

        void CreateSpaces() {
            CHECK(m_session.Get() != XR_NULL_HANDLE);

            // Create a app space to bridge interactions and all holograms.
            {
                if (m_optionalExtensions.UnboundedRefSpaceSupported) {
                    // Unbounded reference space provides the best app space for world-scale experiences.
                    m_appSpaceType = XR_REFERENCE_SPACE_TYPE_UNBOUNDED_MSFT;
                } else {
                    // If running on a platform that does not support world-scale experiences, fall back to local space.
                    m_appSpaceType = XR_REFERENCE_SPACE_TYPE_LOCAL;
                }

                XrReferenceSpaceCreateInfo spaceCreateInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
                spaceCreateInfo.referenceSpaceType = m_appSpaceType;
                spaceCreateInfo.poseInReferenceSpace = xr::math::Pose::Identity();
                CHECK_XRCMD(xrCreateReferenceSpace(m_session.Get(), &spaceCreateInfo, m_appSpace.Put()));
            }

#ifdef USE_REMOTE_RENDERING
            // Create a view space for status display positioning.
            {
                XrReferenceSpaceCreateInfo spaceCreateInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
                spaceCreateInfo.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_VIEW;
                spaceCreateInfo.poseInReferenceSpace = xr::math::Pose::Translation({0.0f, 0.0f, -2.0f});
                CHECK_XRCMD(xrCreateReferenceSpace(m_session.Get(), &spaceCreateInfo, m_statusDisplaySpace.Put()));
            }
#endif

            // Create a space for each hand pointer pose.
            for (uint32_t side : {LeftSide, RightSide}) {
                XrActionSpaceCreateInfo createInfo{XR_TYPE_ACTION_SPACE_CREATE_INFO};
                createInfo.action = m_poseAction.Get();
                createInfo.poseInActionSpace = xr::math::Pose::Identity();
                createInfo.subactionPath = m_subactionPaths[side];
                CHECK_XRCMD(xrCreateActionSpace(m_session.Get(), &createInfo, m_cubesInHand[side].Space.Put()));
            }
        }

        std::tuple<DXGI_FORMAT, DXGI_FORMAT> SelectSwapchainPixelFormats() {
            CHECK(m_session.Get() != XR_NULL_HANDLE);

            // Query the runtime's preferred swapchain formats.
            uint32_t swapchainFormatCount;
            CHECK_XRCMD(xrEnumerateSwapchainFormats(m_session.Get(), 0, &swapchainFormatCount, nullptr));

            std::vector<int64_t> swapchainFormats(swapchainFormatCount);
            CHECK_XRCMD(xrEnumerateSwapchainFormats(
                m_session.Get(), (uint32_t)swapchainFormats.size(), &swapchainFormatCount, swapchainFormats.data()));

            // Choose the first runtime-preferred format that this app supports.
            auto SelectPixelFormat = [](const std::vector<int64_t>& runtimePreferredFormats,
                                        const std::vector<DXGI_FORMAT>& applicationSupportedFormats) {
                auto found = std::find_first_of(std::begin(runtimePreferredFormats),
                                                std::end(runtimePreferredFormats),
                                                std::begin(applicationSupportedFormats),
                                                std::end(applicationSupportedFormats));
                if (found == std::end(runtimePreferredFormats)) {
                    THROW("No runtime swapchain format is supported.");
                }
                return (DXGI_FORMAT)*found;
            };

            DXGI_FORMAT colorSwapchainFormat = SelectPixelFormat(swapchainFormats, m_graphicsPlugin->SupportedColorFormats());
            DXGI_FORMAT depthSwapchainFormat = SelectPixelFormat(swapchainFormats, m_graphicsPlugin->SupportedDepthFormats());

            return {colorSwapchainFormat, depthSwapchainFormat};
        }

        void CreateSwapchains() {
            CHECK(m_session.Get() != XR_NULL_HANDLE);
            CHECK(m_renderResources == nullptr);

            m_renderResources = std::make_unique<RenderResources>();

            // Read graphics properties for preferred swapchain length and logging.
            XrSystemProperties systemProperties{XR_TYPE_SYSTEM_PROPERTIES};
            CHECK_XRCMD(xrGetSystemProperties(m_instance.Get(), m_systemId, &systemProperties));

            // Select color and depth swapchain pixel formats.
            const auto [colorSwapchainFormat, depthSwapchainFormat] = SelectSwapchainPixelFormats();

            // Query and cache view configuration views.
            uint32_t viewCount;
            CHECK_XRCMD(xrEnumerateViewConfigurationViews(m_instance.Get(), m_systemId, m_primaryViewConfigType, 0, &viewCount, nullptr));
            CHECK(viewCount == m_stereoViewCount);

            m_renderResources->ConfigViews.resize(viewCount, {XR_TYPE_VIEW_CONFIGURATION_VIEW});
            CHECK_XRCMD(xrEnumerateViewConfigurationViews(
                m_instance.Get(), m_systemId, m_primaryViewConfigType, viewCount, &viewCount, m_renderResources->ConfigViews.data()));

            // Using texture array for better performance, so requiring left/right views have identical sizes.
            const XrViewConfigurationView& view = m_renderResources->ConfigViews[0];
            CHECK(m_renderResources->ConfigViews[0].recommendedImageRectWidth ==
                  m_renderResources->ConfigViews[1].recommendedImageRectWidth);
            CHECK(m_renderResources->ConfigViews[0].recommendedImageRectHeight ==
                  m_renderResources->ConfigViews[1].recommendedImageRectHeight);
            CHECK(m_renderResources->ConfigViews[0].recommendedSwapchainSampleCount ==
                  m_renderResources->ConfigViews[1].recommendedSwapchainSampleCount);

            // Use the system's recommended rendering parameters.
            const uint32_t imageRectWidth = view.recommendedImageRectWidth;
            const uint32_t imageRectHeight = view.recommendedImageRectHeight;
            const uint32_t swapchainSampleCount = view.recommendedSwapchainSampleCount;

            // Create swapchains with texture array for color and depth images.
            // The texture array has the size of viewCount, and they are rendered in a single pass using VPRT.
            const uint32_t textureArraySize = viewCount;
            m_renderResources->ColorSwapchain =
                CreateSwapchainD3D11(m_session.Get(),
                                     colorSwapchainFormat,
                                     imageRectWidth,
                                     imageRectHeight,
                                     textureArraySize,
                                     swapchainSampleCount,
                                     0 /*createFlags*/,
                                     XR_SWAPCHAIN_USAGE_SAMPLED_BIT | XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT);

            m_renderResources->DepthSwapchain =
                CreateSwapchainD3D11(m_session.Get(),
                                     depthSwapchainFormat,
                                     imageRectWidth,
                                     imageRectHeight,
                                     textureArraySize,
                                     swapchainSampleCount,
                                     0 /*createFlags*/,
                                     XR_SWAPCHAIN_USAGE_SAMPLED_BIT | XR_SWAPCHAIN_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT);

            // Preallocate view buffers for xrLocateViews later inside frame loop.
            m_renderResources->Views.resize(viewCount, {XR_TYPE_VIEW});
        }

        struct SwapchainD3D11;
        SwapchainD3D11 CreateSwapchainD3D11(XrSession session,
                                            DXGI_FORMAT format,
                                            uint32_t width,
                                            uint32_t height,
                                            uint32_t arraySize,
                                            uint32_t sampleCount,
                                            XrSwapchainCreateFlags createFlags,
                                            XrSwapchainUsageFlags usageFlags) {
            SwapchainD3D11 swapchain;
            swapchain.Format = format;
            swapchain.Width = width;
            swapchain.Height = height;
            swapchain.ArraySize = arraySize;

            XrSwapchainCreateInfo swapchainCreateInfo{XR_TYPE_SWAPCHAIN_CREATE_INFO};
            swapchainCreateInfo.arraySize = arraySize;
            swapchainCreateInfo.format = format;
            swapchainCreateInfo.width = width;
            swapchainCreateInfo.height = height;
            swapchainCreateInfo.mipCount = 1;
            swapchainCreateInfo.faceCount = 1;
            swapchainCreateInfo.sampleCount = sampleCount;
            swapchainCreateInfo.createFlags = createFlags;
            swapchainCreateInfo.usageFlags = usageFlags;

            CHECK_XRCMD(xrCreateSwapchain(session, &swapchainCreateInfo, swapchain.Handle.Put()));

            uint32_t chainLength;
            CHECK_XRCMD(xrEnumerateSwapchainImages(swapchain.Handle.Get(), 0, &chainLength, nullptr));

            swapchain.Images.resize(chainLength, {XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR});
            CHECK_XRCMD(xrEnumerateSwapchainImages(swapchain.Handle.Get(),
                                                   (uint32_t)swapchain.Images.size(),
                                                   &chainLength,
                                                   reinterpret_cast<XrSwapchainImageBaseHeader*>(swapchain.Images.data())));

            return swapchain;
        }

        void ProcessEvents(bool* exitRenderLoop, bool* requestRestart) {
            *exitRenderLoop = *requestRestart = false;

            auto pollEvent = [&](XrEventDataBuffer& eventData) -> bool {
                eventData.type = XR_TYPE_EVENT_DATA_BUFFER;
                eventData.next = nullptr;
                return CHECK_XRCMD(xrPollEvent(m_instance.Get(), &eventData)) == XR_SUCCESS;
            };

            XrEventDataBuffer eventData{};
            while (pollEvent(eventData)) {
                switch (eventData.type) {
                case XR_TYPE_EVENT_DATA_INSTANCE_LOSS_PENDING: {
                    *exitRenderLoop = true;
                    *requestRestart = false;
                    return;
                }
                case XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED: {
                    const auto stateEvent = *reinterpret_cast<const XrEventDataSessionStateChanged*>(&eventData);
                    CHECK(m_session.Get() != XR_NULL_HANDLE && m_session.Get() == stateEvent.session);
                    m_sessionState = stateEvent.state;
                    switch (m_sessionState) {
                    case XR_SESSION_STATE_READY: {
                        CHECK(m_session.Get() != XR_NULL_HANDLE);
                        XrSessionBeginInfo sessionBeginInfo{XR_TYPE_SESSION_BEGIN_INFO};
                        sessionBeginInfo.primaryViewConfigurationType = m_primaryViewConfigType;
                        CHECK_XRCMD(xrBeginSession(m_session.Get(), &sessionBeginInfo));
                        m_sessionRunning = true;
                        break;
                    }
                    case XR_SESSION_STATE_STOPPING: {
                        m_sessionRunning = false;
                        CHECK_XRCMD(xrEndSession(m_session.Get()));
                        break;
                    }
                    case XR_SESSION_STATE_EXITING: {
                        // Do not attempt to restart, because user closed this session.
                        *exitRenderLoop = true;
                        *requestRestart = false;
                        break;
                    }
                    case XR_SESSION_STATE_LOSS_PENDING: {
                        // Session was lost, so start over and poll for new systemId.
                        *exitRenderLoop = true;
                        *requestRestart = true;
                        break;
                    }
                    }
                    break;
                }
                case XR_TYPE_EVENT_DATA_REFERENCE_SPACE_CHANGE_PENDING:
                case XR_TYPE_EVENT_DATA_INTERACTION_PROFILE_CHANGED:
                default: {
                    DEBUG_PRINT("Ignoring event type %d", eventData.type);
                    break;
                }
                }
            }
        }

        struct Hologram;
        Hologram CreateHologram(const XrPosef& poseInAppSpace, XrTime placementTime) const {
            Hologram hologram{};
            if (m_optionalExtensions.SpatialAnchorSupported) {
                // Anchors provide the best stability when moving beyond 5 meters, so if the extension is enabled,
                // create an anchor at given location and place the hologram at the resulting anchor space.
                XrSpatialAnchorCreateInfoMSFT createInfo{XR_TYPE_SPATIAL_ANCHOR_CREATE_INFO_MSFT};
                createInfo.space = m_appSpace.Get();
                createInfo.pose = poseInAppSpace;
                createInfo.time = placementTime;

                XrResult result = m_extensions.xrCreateSpatialAnchorMSFT(
                    m_session.Get(), &createInfo, hologram.Anchor.Put(m_extensions.xrDestroySpatialAnchorMSFT));
                if (XR_SUCCEEDED(result)) {
                    XrSpatialAnchorSpaceCreateInfoMSFT createSpaceInfo{XR_TYPE_SPATIAL_ANCHOR_SPACE_CREATE_INFO_MSFT};
                    createSpaceInfo.anchor = hologram.Anchor.Get();
                    createSpaceInfo.poseInAnchorSpace = xr::math::Pose::Identity();
                    CHECK_XRCMD(m_extensions.xrCreateSpatialAnchorSpaceMSFT(m_session.Get(), &createSpaceInfo, hologram.Cube.Space.Put()));
                } else if (result == XR_ERROR_CREATE_SPATIAL_ANCHOR_FAILED_MSFT) {
                    DEBUG_PRINT("Anchor cannot be created, likely due to lost positional tracking.");
                } else {
                    CHECK_XRRESULT(result, "xrCreateSpatialAnchorMSFT");
                }
            } else {
                // If the anchor extension is not available, place hologram in the app space.
                // This works fine as long as user doesn't move far away from app space origin.
                XrReferenceSpaceCreateInfo createInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
                createInfo.referenceSpaceType = m_appSpaceType;
                createInfo.poseInReferenceSpace = poseInAppSpace;
                CHECK_XRCMD(xrCreateReferenceSpace(m_session.Get(), &createInfo, hologram.Cube.Space.Put()));
            }
            return hologram;
        }

        void PollActions() {
            // Get updated action states.
            std::vector<XrActiveActionSet> activeActionSets = {{m_actionSet.Get(), XR_NULL_PATH}};
            XrActionsSyncInfo syncInfo{XR_TYPE_ACTIONS_SYNC_INFO};
            syncInfo.countActiveActionSets = (uint32_t)activeActionSets.size();
            syncInfo.activeActionSets = activeActionSets.data();
            CHECK_XRCMD(xrSyncActions(m_session.Get(), &syncInfo));

            // Check the state of the actions for left and right hands separately.
            for (uint32_t side : {LeftSide, RightSide}) {
                const XrPath subactionPath = m_subactionPaths[side];

                // Apply a tiny vibration to the corresponding hand to indicate that action is detected.
                auto ApplyVibration = [this, subactionPath] {
                    XrHapticActionInfo actionInfo{XR_TYPE_HAPTIC_ACTION_INFO};
                    actionInfo.action = m_vibrateAction.Get();
                    actionInfo.subactionPath = subactionPath;

                    XrHapticVibration vibration{XR_TYPE_HAPTIC_VIBRATION};
                    vibration.amplitude = 0.5f;
                    vibration.duration = XR_MIN_HAPTIC_DURATION;
                    vibration.frequency = XR_FREQUENCY_UNSPECIFIED;
                    CHECK_XRCMD(xrApplyHapticFeedback(m_session.Get(), &actionInfo, (XrHapticBaseHeader*)&vibration));
                };

                XrActionStateBoolean placeActionValue{XR_TYPE_ACTION_STATE_BOOLEAN};
                {
                    XrActionStateGetInfo getInfo{XR_TYPE_ACTION_STATE_GET_INFO};
                    getInfo.action = m_placeAction.Get();
                    getInfo.subactionPath = subactionPath;
                    CHECK_XRCMD(xrGetActionStateBoolean(m_session.Get(), &getInfo, &placeActionValue));
                }

                // When select button is pressed, place the cube at the location of the corresponding hand.
                if (placeActionValue.isActive && placeActionValue.changedSinceLastSync && placeActionValue.currentState) {
                    // Use the pose at the historical time when the action happened to do the placement.
                    const XrTime placementTime = placeActionValue.lastChangeTime;

                    // Locate the hand in the scene.
                    XrSpaceLocation handLocation{XR_TYPE_SPACE_LOCATION};
                    CHECK_XRCMD(xrLocateSpace(m_cubesInHand[side].Space.Get(), m_appSpace.Get(), placementTime, &handLocation));

                    // Ensure we have tracking before placing a cube in the scene, so that it stays reliably at a physical location.
                    if (!xr::math::Pose::IsPoseValid(handLocation)) {
                        DEBUG_PRINT("Cube cannot be placed when positional tracking is lost.");
                    } else {
                        // Place a new cube at the given location and time, and remember output placement space and anchor.
                        m_holograms.push_back(CreateHologram(handLocation.pose, placementTime));
                    }

                    ApplyVibration();
                }

                // This sample, when menu button is released, requests to quit the session, and therefore quit the application.
                {
                    XrActionStateBoolean exitActionValue{XR_TYPE_ACTION_STATE_BOOLEAN};
                    XrActionStateGetInfo getInfo{XR_TYPE_ACTION_STATE_GET_INFO};
                    getInfo.action = m_exitAction.Get();
                    getInfo.subactionPath = subactionPath;
                    CHECK_XRCMD(xrGetActionStateBoolean(m_session.Get(), &getInfo, &exitActionValue));

                    if (exitActionValue.isActive && exitActionValue.changedSinceLastSync && !exitActionValue.currentState) {
                        CHECK_XRCMD(xrRequestExitSession(m_session.Get()));
                        ApplyVibration();
                    }
                }
            }
        }

        void RenderFrame() {
            CHECK(m_session.Get() != XR_NULL_HANDLE);

            XrFrameWaitInfo frameWaitInfo{XR_TYPE_FRAME_WAIT_INFO};
            XrFrameState frameState{XR_TYPE_FRAME_STATE};
            CHECK_XRCMD(xrWaitFrame(m_session.Get(), &frameWaitInfo, &frameState));

            XrFrameBeginInfo frameBeginInfo{XR_TYPE_FRAME_BEGIN_INFO};
            CHECK_XRCMD(xrBeginFrame(m_session.Get(), &frameBeginInfo));

            // xrEndFrame can submit multiple layers. This sample submits one.
            std::vector<XrCompositionLayerBaseHeader*> layers;

            // The projection layer consists of projection layer views.
            XrCompositionLayerProjection layer{XR_TYPE_COMPOSITION_LAYER_PROJECTION};

            // Inform the runtime that the app's submitted alpha channel has valid data for use during composition.
            // The primary display on HoloLens has an additive environment blend mode. It will ignore the alpha channel.
            // However, mixed reality capture uses the alpha channel if this bit is set to blend content with the environment.
            layer.layerFlags = XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT;

            // Only render when session is visible, otherwise submit zero layers.
            if (frameState.shouldRender) {
                // First update the viewState and views using latest predicted display time.
                {
                    XrViewLocateInfo viewLocateInfo{XR_TYPE_VIEW_LOCATE_INFO};
                    viewLocateInfo.viewConfigurationType = m_primaryViewConfigType;
                    viewLocateInfo.displayTime = frameState.predictedDisplayTime;
                    viewLocateInfo.space = m_appSpace.Get();

                    // The output view count of xrLocateViews is always same as xrEnumerateViewConfigurationViews.
                    // Therefore, Views can be preallocated and avoid two call idiom here.
                    uint32_t viewCapacityInput = (uint32_t)m_renderResources->Views.size();
                    uint32_t viewCountOutput;
                    CHECK_XRCMD(xrLocateViews(m_session.Get(),
                                              &viewLocateInfo,
                                              &m_renderResources->ViewState,
                                              viewCapacityInput,
                                              &viewCountOutput,
                                              m_renderResources->Views.data()));

                    CHECK(viewCountOutput == viewCapacityInput);
                    CHECK(viewCountOutput == m_renderResources->ConfigViews.size());
                    CHECK(viewCountOutput == m_renderResources->ColorSwapchain.ArraySize);
                    CHECK(viewCountOutput == m_renderResources->DepthSwapchain.ArraySize);
                }

                // Then, render projection layer into each view.
                if (RenderLayer(frameState.predictedDisplayTime, layer)) {
                    layers.push_back(reinterpret_cast<XrCompositionLayerBaseHeader*>(&layer));
                }
            }

            // Submit the composition layers for the predicted display time.
            XrFrameEndInfo frameEndInfo{XR_TYPE_FRAME_END_INFO};
            frameEndInfo.displayTime = frameState.predictedDisplayTime;
            frameEndInfo.environmentBlendMode = m_environmentBlendMode;
            frameEndInfo.layerCount = (uint32_t)layers.size();
            frameEndInfo.layers = layers.data();
            CHECK_XRCMD(xrEndFrame(m_session.Get(), &frameEndInfo));
        }

        uint32_t AcquireAndWaitForSwapchainImage(XrSwapchain handle) {
            uint32_t swapchainImageIndex;
            XrSwapchainImageAcquireInfo acquireInfo{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
            CHECK_XRCMD(xrAcquireSwapchainImage(handle, &acquireInfo, &swapchainImageIndex));

            XrSwapchainImageWaitInfo waitInfo{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};
            waitInfo.timeout = XR_INFINITE_DURATION;
            CHECK_XRCMD(xrWaitSwapchainImage(handle, &waitInfo));

            return swapchainImageIndex;
        }

        void InitializeSpinningCube(XrTime predictedDisplayTime) {
            auto createReferenceSpace = [session = m_session.Get()](XrReferenceSpaceType referenceSpaceType, XrPosef poseInReferenceSpace) {
                xr::SpaceHandle space;
                XrReferenceSpaceCreateInfo createInfo{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
                createInfo.referenceSpaceType = referenceSpaceType;
                createInfo.poseInReferenceSpace = poseInReferenceSpace;
                CHECK_XRCMD(xrCreateReferenceSpace(session, &createInfo, space.Put()));
                return space;
            };

            {
                // Initialize a big cube 1 meter in front of user.
                Hologram hologram{};
                hologram.Cube.Scale = {0.25f, 0.25f, 0.25f};
                hologram.Cube.Space = createReferenceSpace(XR_REFERENCE_SPACE_TYPE_LOCAL, xr::math::Pose::Translation({0, 0, -1}));
                m_holograms.push_back(std::move(hologram));
                m_mainCubeIndex = (uint32_t)m_holograms.size() - 1;
            }

            {
                // Initialize a small cube and remember the time when animation is started.
                Hologram hologram{};
                hologram.Cube.Scale = {0.1f, 0.1f, 0.1f};
                hologram.Cube.Space = createReferenceSpace(XR_REFERENCE_SPACE_TYPE_LOCAL, xr::math::Pose::Translation({0, 0, -1}));
                m_holograms.push_back(std::move(hologram));
                m_spinningCubeIndex = (uint32_t)m_holograms.size() - 1;

                m_spinningCubeStartTime = predictedDisplayTime;
            }
        }

        void UpdateSpinningCube(XrTime predictedDisplayTime) {
            if (!m_mainCubeIndex || !m_spinningCubeIndex) {
                // Deferred initialization of spinning cubes so they appear at right place for the first frame.
                InitializeSpinningCube(predictedDisplayTime);
            }

            // Pause spinning cube animation when app loses 3D focus
            if (IsSessionFocused()) {
                auto convertToSeconds = [](XrDuration nanoSeconds) {
                    using namespace std::chrono;
                    return duration_cast<duration<float>>(duration<XrDuration, std::nano>(nanoSeconds)).count();
                };

                const XrDuration duration = predictedDisplayTime - m_spinningCubeStartTime;
                const float seconds = convertToSeconds(duration);
                const float angle = DirectX::XM_PIDIV2 * seconds; // Rotate 90 degrees per second
                const float radius = 0.5f;                        // Rotation radius in meters

                // Let spinning cube rotate around the main cube's y axis.
                XrPosef pose;
                pose.position = {radius * std::sin(angle), 0, radius * std::cos(angle)};
                pose.orientation = xr::math::Quaternion::RotationAxisAngle({0, 1, 0}, angle);
                m_holograms[m_spinningCubeIndex.value()].Cube.PoseInSpace = pose;
            }
        }

        bool RenderLayer(XrTime predictedDisplayTime, XrCompositionLayerProjection& layer) {
            const uint32_t viewCount = (uint32_t)m_renderResources->ConfigViews.size();

            if (!xr::math::Pose::IsPoseValid(m_renderResources->ViewState)) {
                DEBUG_PRINT("xrLocateViews returned an invalid pose.");
                return false; // Skip rendering layers if view location is invalid
            }

            std::vector<const sample::Cube*> visibleCubes;

            auto UpdateVisibleCube = [&](sample::Cube& cube) {
                if (cube.Space.Get() != XR_NULL_HANDLE) {
                    XrSpaceLocation cubeSpaceInAppSpace{XR_TYPE_SPACE_LOCATION};
                    CHECK_XRCMD(xrLocateSpace(cube.Space.Get(), m_appSpace.Get(), predictedDisplayTime, &cubeSpaceInAppSpace));

                    // Update cube's location with latest space location
                    if (xr::math::Pose::IsPoseValid(cubeSpaceInAppSpace)) {
                        if (cube.PoseInSpace.has_value()) {
                            cube.PoseInAppSpace = xr::math::Pose::Multiply(cube.PoseInSpace.value(), cubeSpaceInAppSpace.pose);
                        } else {
                            cube.PoseInAppSpace = cubeSpaceInAppSpace.pose;
                        }
                        visibleCubes.push_back(&cube);
                    }
                }
            };

            UpdateSpinningCube(predictedDisplayTime);

            UpdateVisibleCube(m_cubesInHand[LeftSide]);
            UpdateVisibleCube(m_cubesInHand[RightSide]);

            for (auto& hologram : m_holograms) {
                UpdateVisibleCube(hologram.Cube);
            }

#ifdef USE_REMOTE_RENDERING
            if (m_statusDisplay != nullptr) {
                XrSpaceLocation viewSpaceInAppSpace{XR_TYPE_SPACE_LOCATION};
                CHECK_XRCMD(xrLocateSpace(m_statusDisplaySpace.Get(), m_appSpace.Get(), predictedDisplayTime, &viewSpaceInAppSpace));

                if (xr::math::Pose::IsPoseValid(viewSpaceInAppSpace)) {
                    m_statusDisplay->PositionDisplay(viewSpaceInAppSpace.pose);
                }
                m_statusDisplay->Update();
            }
#endif

            m_renderResources->ProjectionLayerViews.resize(viewCount);
            if (m_optionalExtensions.DepthExtensionSupported) {
                m_renderResources->DepthInfoViews.resize(viewCount);
            }

            // Swapchain is acquired, rendered to, and released together for all views as texture array
            const SwapchainD3D11& colorSwapchain = m_renderResources->ColorSwapchain;
            const SwapchainD3D11& depthSwapchain = m_renderResources->DepthSwapchain;

            // Use the full size of the allocated swapchain image (could render smaller some frames to hit framerate)
            const XrRect2Di imageRect = {{0, 0}, {(int32_t)colorSwapchain.Width, (int32_t)colorSwapchain.Height}};
            CHECK(colorSwapchain.Width == depthSwapchain.Width);
            CHECK(colorSwapchain.Height == depthSwapchain.Height);

            const uint32_t colorSwapchainImageIndex = AcquireAndWaitForSwapchainImage(colorSwapchain.Handle.Get());
            const uint32_t depthSwapchainImageIndex = AcquireAndWaitForSwapchainImage(depthSwapchain.Handle.Get());

            // Prepare rendering parameters of each view for swapchain texture arrays
            std::vector<xr::math::ViewProjection> viewProjections(viewCount);
            for (uint32_t i = 0; i < viewCount; i++) {
                viewProjections[i] = {m_renderResources->Views[i].pose, m_renderResources->Views[i].fov, m_nearFar};

                m_renderResources->ProjectionLayerViews[i] = {XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW};
                m_renderResources->ProjectionLayerViews[i].pose = m_renderResources->Views[i].pose;
                m_renderResources->ProjectionLayerViews[i].fov = m_renderResources->Views[i].fov;
                m_renderResources->ProjectionLayerViews[i].subImage.swapchain = colorSwapchain.Handle.Get();
                m_renderResources->ProjectionLayerViews[i].subImage.imageRect = imageRect;
                m_renderResources->ProjectionLayerViews[i].subImage.imageArrayIndex = i;

                if (m_optionalExtensions.DepthExtensionSupported) {
                    m_renderResources->DepthInfoViews[i] = {XR_TYPE_COMPOSITION_LAYER_DEPTH_INFO_KHR};
                    m_renderResources->DepthInfoViews[i].minDepth = 0;
                    m_renderResources->DepthInfoViews[i].maxDepth = 1;
                    m_renderResources->DepthInfoViews[i].nearZ = m_nearFar.Near;
                    m_renderResources->DepthInfoViews[i].farZ = m_nearFar.Far;
                    m_renderResources->DepthInfoViews[i].subImage.swapchain = depthSwapchain.Handle.Get();
                    m_renderResources->DepthInfoViews[i].subImage.imageRect = imageRect;
                    m_renderResources->DepthInfoViews[i].subImage.imageArrayIndex = i;

                    // Chain depth info struct to the corresponding projection layer view's next pointer
                    m_renderResources->ProjectionLayerViews[i].next = &m_renderResources->DepthInfoViews[i];
                }
            }

            // For HoloLens additive display, best to clear render target with transparent black color (0,0,0,0)
            constexpr DirectX::XMVECTORF32 opaqueColor = {0.184313729f, 0.309803933f, 0.309803933f, 1.000000000f};
            constexpr DirectX::XMVECTORF32 transparent = {0.000000000f, 0.000000000f, 0.000000000f, 0.000000000f};
            const DirectX::XMVECTORF32 renderTargetClearColor =
                (m_environmentBlendMode == XR_ENVIRONMENT_BLEND_MODE_OPAQUE) ? opaqueColor : transparent;

            m_graphicsPlugin->RenderView(
#ifdef USE_REMOTE_RENDERING
                this,
#endif
                imageRect,
                renderTargetClearColor,
                viewProjections,
                colorSwapchain.Format,
                colorSwapchain.Images[colorSwapchainImageIndex].texture,
                depthSwapchain.Format,
                depthSwapchain.Images[depthSwapchainImageIndex].texture,
                visibleCubes);

            XrSwapchainImageReleaseInfo releaseInfo{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
            CHECK_XRCMD(xrReleaseSwapchainImage(colorSwapchain.Handle.Get(), &releaseInfo));
            CHECK_XRCMD(xrReleaseSwapchainImage(depthSwapchain.Handle.Get(), &releaseInfo));

            layer.space = m_appSpace.Get();
            layer.viewCount = (uint32_t)m_renderResources->ProjectionLayerViews.size();
            layer.views = m_renderResources->ProjectionLayerViews.data();
            return true;
        }

        void PrepareSessionRestart() {
            m_mainCubeIndex = m_spinningCubeIndex = {};
            m_holograms.clear();
            m_renderResources.reset();
            m_session.Reset();
            m_systemId = XR_NULL_SYSTEM_ID;
        }

        constexpr bool IsSessionFocused() const {
            return m_sessionState == XR_SESSION_STATE_FOCUSED;
        }

        XrPath GetXrPath(const char* string) const {
            return xr::StringToPath(m_instance.Get(), string);
        }

#ifdef USE_REMOTE_RENDERING
        void InitARR() {
            // 1. One time initialization
            {
                RR::RemoteRenderingInitialization clientInit;
                clientInit.ConnectionType = RR::ConnectionType::General;
                clientInit.GraphicsApi = RR::GraphicsApiType::OpenXrD3D11;
                clientInit.ToolId = "<sample name goes here>"; // <put your sample name here>
                clientInit.UnitsPerMeter = 1.0f;
                clientInit.Forward = RR::Axis::NegativeZ;
                clientInit.Right = RR::Axis::X;
                clientInit.Up = RR::Axis::Y;
                if (RR::StartupRemoteRendering(clientInit) != RR::Result::Success) {
                    // something fundamental went wrong with the initialization
                    throw std::exception("Failed to start remote rendering. Invalid client init data.");
                }
            }

            // 2. Create Client
            {
                // Users need to fill out the following with their account data and model
                RR::SessionConfiguration init;
                init.AccountId = "00000000-0000-0000-0000-000000000000";
                init.AccountKey = "<account key>";
                init.RemoteRenderingDomain = "westus2.mixedreality.azure.com"; // <change to the region that the rendering session should be created in>
                init.AccountDomain = "westus2.mixedreality.azure.com"; // <change to the region the account was created in>
                m_modelURI = "builtin://Engine";
                m_sessionOverride = ""; // If there is a valid session ID to re-use, put it here. Otherwise a new one is created
                m_client = RR::ApiHandle(RR::RemoteRenderingClient(init));
            }

            // 3. Open/create rendering session
            {
                auto SessionHandler = [&](RR::Status status, RR::ApiHandle<RR::CreateRenderingSessionResult> result) {
                    if (status == RR::Status::OK) {
                        auto ctx = result->GetContext();
                        if (ctx.Result == RR::Result::Success) {
                            SetNewSession(result->GetSession());
                        } else {
                            SetNewState(AppConnectionStatus::ConnectionFailed, ctx.ErrorMessage.c_str());
                        }
                    } else {
                        SetNewState(AppConnectionStatus::ConnectionFailed, "failed");
                    }
                };

                // If we had an old (valid) session that we can recycle, we call async function m_client->OpenRenderingSessionAsync
                if (!m_sessionOverride.empty()) {
                    m_client->OpenRenderingSessionAsync(m_sessionOverride, SessionHandler);
                    SetNewState(AppConnectionStatus::CreatingSession, nullptr);
                } else {
                    // create a new session
                    RR::RenderingSessionCreationOptions init;
                    init.MaxLeaseInMinutes = 10; // session is leased for 10 minutes
                    init.Size = RR::RenderingSessionVmSize::Standard;
                    m_client->CreateNewRenderingSessionAsync(init, SessionHandler);
                    SetNewState(AppConnectionStatus::CreatingSession, nullptr);
                }
            }
        }

        void UpdateARR() {
            if (m_renderingSession != nullptr) {
                // Tick the client to receive messages
                m_api->Update();

                if (!m_sessionStarted) {
                    // Important: To avoid server-side throttling of the requests, we should call GetPropertiesAsync very infrequently:

                    // query session status periodically until we reach 'session started'
                    if (!m_sessionPropertiesQueryInProgress && m_timer.GetTotalSeconds() - m_timeAtLastRESTCall > m_delayBetweenRESTCalls) {
                        m_timeAtLastRESTCall = m_timer.GetTotalSeconds();
                        m_sessionPropertiesQueryInProgress = true;
                        m_renderingSession->GetPropertiesAsync(
                            [this](RR::Status status,
                                                           RR::ApiHandle<RR::RenderingSessionPropertiesResult> propertiesResult) {
                                if (status == RR::Status::OK) {
                                    auto ctx = propertiesResult->GetContext();
                                    if (ctx.Result == RR::Result::Success) {
                                        auto res = propertiesResult->GetSessionProperties();
                                        switch (res.Status) {
                                        case RR::RenderingSessionStatus::Ready: {
                                            // The following ConnectAsync is async, but we'll get notifications via
                                            // OnConnectionStatusChanged
                                            m_sessionStarted = true;
                                            SetNewState(AppConnectionStatus::Connecting, nullptr);
                                            RR::RendererInitOptions init;
                                            init.IgnoreCertificateValidation = false;
                                            init.RenderMode = RR::ServiceRenderMode::Default;
                                            m_renderingSession->ConnectAsync(init, [](RR::Status, RR::ConnectionStatus) {});
                                        } break;
                                        case RR::RenderingSessionStatus::Error:
                                            SetNewState(AppConnectionStatus::ConnectionFailed, "Session error");
                                            break;
                                        case RR::RenderingSessionStatus::Stopped:
                                            SetNewState(AppConnectionStatus::ConnectionFailed, "Session stopped");
                                            break;
                                        case RR::RenderingSessionStatus::Expired:
                                            SetNewState(AppConnectionStatus::ConnectionFailed, "Session expired");
                                            break;
                                        }
                                    } else {
                                        SetNewState(AppConnectionStatus::ConnectionFailed, ctx.ErrorMessage.c_str());
                                    }
                                } else {
                                    SetNewState(AppConnectionStatus::ConnectionFailed, "Failed to retrieve session status");
                                }
                                m_delayBetweenRESTCalls = propertiesResult->GetMinimumRetryDelay();
                                m_sessionPropertiesQueryInProgress = false; // next try
                            });
                    }
                }

                if (m_isConnected && !m_modelLoadTriggered) {
                    m_modelLoadTriggered = true;
                    StartModelLoading();
                }
            }

            UpdateStatusText();

            if (m_needsCoordinateSystemUpdate && m_appSpace.Get() && m_graphicsBinding) {
                // Set the coordinate system once. This must be called again whenever the coordinate system changes.
                #ifdef _M_ARM64
                    m_graphicsBinding->UpdateAppSpace(reinterpret_cast<uint64_t>(m_appSpace.Get()));
                #else
                    m_graphicsBinding->UpdateAppSpace(m_appSpace.Get());
                #endif
                m_needsCoordinateSystemUpdate = false;
            }

            double currTime = m_timer.GetTotalSeconds();
            // float deltaTimeInSeconds = (m_lastTime < 0) ? 0.f : (float)(currTime - m_lastTime);
            m_lastTime = currTime;

            if (m_isConnected) {
                // The API to inform the server always requires near < far. Depth buffer data will be converted locally to match what is
                // set on the HolographicCamera.
                auto settings = m_api->GetCameraSettings();
                float localNear = std::min(m_nearFar.Near, m_nearFar.Far);
                float localFar = std::max(m_nearFar.Far, m_nearFar.Far);
                settings->SetNearAndFarPlane(localNear, localFar);
                settings->SetInverseDepth(m_nearFar.Near > m_nearFar.Far);
                settings->SetEnableDepth(true);
            }
        }

        void RenderARR(ID3D11DeviceContext* context) override {
            // Inject remote rendering: as soon as we are connected, start blitting the remote frame.
            // We do the blit after the Clear and viewport setup, and before our rendering.
            if (m_isConnected) {
                m_graphicsBinding->BlitRemoteFrame();
            }

            if (m_statusDisplay) {
                // Draw connection/progress/error status
                m_statusDisplay->Render(context);
            }
        }

        void OnConnectionStatusChanged(RR::ConnectionStatus status, RR::Result error) {
            const char* asString = RR::ResultToString(error);
            m_connectionResult = error;

            switch (status) {
            case RR::ConnectionStatus::Connecting:
                SetNewState(AppConnectionStatus::Connecting, asString);
                break;
            case RR::ConnectionStatus::Connected:
                if (error == RR::Result::Success) {
                    SetNewState(AppConnectionStatus::Connected, asString);
                } else {
                    SetNewState(AppConnectionStatus::ConnectionFailed, asString);
                }
                m_modelLoadTriggered = m_modelLoadFinished = false;
                m_isConnected = error == RR::Result::Success;
                break;
            case RR::ConnectionStatus::Disconnected:
                if (error == RR::Result::Success) {
                    SetNewState(AppConnectionStatus::Disconnected, asString);
                } else {
                    SetNewState(AppConnectionStatus::ConnectionFailed, asString);
                }
                m_modelLoadTriggered = m_modelLoadFinished = false;
                m_isConnected = false;
                break;
            default:
                break;
            }
        }

        void SetNewState(AppConnectionStatus state, const char* statusMsg) {
            m_currentStatus = state;
            m_statusMsg = statusMsg ? statusMsg : "";
        }

        void SetNewSession(RR::ApiHandle<RR::RenderingSession> newSession) {
            SetNewState(AppConnectionStatus::StartingSession, nullptr);

            m_sessionStartingTime = m_timeAtLastRESTCall = m_timer.GetTotalSeconds();
            m_renderingSession = newSession;
            m_api = m_renderingSession->Connection();
            m_graphicsBinding = m_renderingSession->GetGraphicsBinding().as<RR::GraphicsBindingOpenXrD3d11>();
            m_renderingSession->ConnectionStatusChanged([this](auto status, auto error) { OnConnectionStatusChanged(status, error); });
        }

        void StartModelLoading() {
            m_modelLoadingProgress = 0.f;

            RR::LoadModelFromSasOptions params;
            params.ModelUri = m_modelURI.c_str();
            params.Parent = nullptr;

            // start the async model loading
            m_api->LoadModelFromSasAsync(
                params,
                // completed callback
                [this](RR::Status status, RR::ApiHandle<RR::LoadModelResult> result) {
                    m_modelLoadResult = RR::StatusToResult(status);
                    m_modelLoadFinished = true;

                    if (m_modelLoadResult == RR::Result::Success) {
                        RR::Double3 pos = {0.0, 0.0, -2.0};
                        result->GetRoot()->SetPosition(pos);
                    }
                },
                // progress update callback
                [this](float progress) {
                    // progress callback
                    m_modelLoadingProgress = progress;
                });
        }

        void UpdateStatusText() {
            if (m_statusDisplay == nullptr) {
                return;
            }

            m_statusDisplay->ClearLines();
            if (m_modelLoadFinished && m_modelLoadResult == RR::Result::Success) {
                // nothing to show anymore
                m_statusDisplay->SetTextEnabled(false);
                return;
            }

            m_statusDisplay->SetTextEnabled(true);

            char txtBuffer[1024];
            std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>, wchar_t> toWChar;

            switch (m_currentStatus) {
            case AppConnectionStatus::CreatingSession:
                sprintf_s(txtBuffer, "Creating session...");
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::White, 1.2f});
                break;
            case AppConnectionStatus::StartingSession: {
                sprintf_s(txtBuffer, "Starting session...");
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::White, 1.2f});
                int elapsedSecs = (int)(m_timer.GetTotalSeconds() - m_sessionStartingTime);
                sprintf_s(txtBuffer, "...this may take a while. Elapsed time: %ds", elapsedSecs);
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::Small, StatusDisplay::White, 1.2f});
                break;
            }
            case AppConnectionStatus::Connecting:
                sprintf_s(txtBuffer, "Connecting...");
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::White, 1.2f});
                break;
            case AppConnectionStatus::Connected:
                sprintf_s(txtBuffer, "Connected");
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::Green, 1.2f});
                break;
            case AppConnectionStatus::ConnectionFailed:
                sprintf_s(txtBuffer, "Failed to connect");
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::Red, 1.2f});
                sprintf_s(txtBuffer, "Error: %s", m_statusMsg.c_str());
                m_statusDisplay->AddLine(
                    StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::Red, 1.2f});
                break;
            case AppConnectionStatus::Disconnected:
                m_statusDisplay->AddLine(StatusDisplay::Line{L"Disconnected", StatusDisplay::LargeBold, StatusDisplay::Yellow, 1.2f});
                break;
            }

            // add additional lines for model loading progress
            if (m_modelLoadTriggered) {
                if (m_modelLoadFinished && m_modelLoadResult != RR::Result::Success) {
                    sprintf_s(txtBuffer, "Failed to load model: %s", RR::ResultToString(m_modelLoadResult));
                    m_statusDisplay->AddLine(
                        StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::Red, 1.2f});
                } else {
                    int percentage = (int)(m_modelLoadingProgress * 100.0f);
                    sprintf_s(txtBuffer, "Loading model (%i%%)", percentage);
                    m_statusDisplay->AddLine(
                        StatusDisplay::Line{toWChar.from_bytes(txtBuffer), StatusDisplay::LargeBold, StatusDisplay::White, 1.2f});
                }
            }
        }
#endif

    private:
#ifdef USE_REMOTE_RENDERING
        // Session related:
        std::string m_sessionOverride;
        RR::ApiHandle<RR::RemoteRenderingClient> m_client;
        RR::ApiHandle<RR::RenderingSession> m_renderingSession;
        RR::ApiHandle<RR::RenderingConnection> m_api;
        RR::ApiHandle<RR::GraphicsBindingOpenXrD3d11> m_graphicsBinding;

        // Model loading:
        std::string m_modelURI;

        // Connection state machine:
        Timer m_timer;
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
        bool m_needsCoordinateSystemUpdate = true;
        double m_timeAtLastRESTCall = 0;
        double m_delayBetweenRESTCalls = 10.0;

        // Status text:
        double m_lastTime = -1;
        double m_sessionStartingTime = 0.0;

        std::unique_ptr<StatusDisplay> m_statusDisplay;
        xr::SpaceHandle m_statusDisplaySpace;
#endif

        constexpr static XrFormFactor m_formFactor{XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY};
        constexpr static XrViewConfigurationType m_primaryViewConfigType{XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO};
        constexpr static uint32_t m_stereoViewCount = 2; // PRIMARY_STEREO view configuration always has 2 views

        const std::string m_applicationName;
        const std::unique_ptr<sample::IGraphicsPluginD3D11> m_graphicsPlugin;

        xr::InstanceHandle m_instance;
        xr::SessionHandle m_session;
        uint64_t m_systemId{XR_NULL_SYSTEM_ID};
        xr::ExtensionDispatchTable m_extensions;

        struct {
            bool DepthExtensionSupported{false};
            bool UnboundedRefSpaceSupported{false};
            bool SpatialAnchorSupported{false};
        } m_optionalExtensions;

        xr::SpaceHandle m_appSpace;
        XrReferenceSpaceType m_appSpaceType{};

        struct Hologram {
            sample::Cube Cube;
            xr::SpatialAnchorHandle Anchor;
        };
        std::vector<Hologram> m_holograms;

        std::optional<uint32_t> m_mainCubeIndex;
        std::optional<uint32_t> m_spinningCubeIndex;
        XrTime m_spinningCubeStartTime;

        constexpr static uint32_t LeftSide = 0;
        constexpr static uint32_t RightSide = 1;
        std::array<XrPath, 2> m_subactionPaths{};
        std::array<sample::Cube, 2> m_cubesInHand{};

        xr::ActionSetHandle m_actionSet;
        xr::ActionHandle m_placeAction;
        xr::ActionHandle m_exitAction;
        xr::ActionHandle m_poseAction;
        xr::ActionHandle m_vibrateAction;

        XrEnvironmentBlendMode m_environmentBlendMode{};
        xr::math::NearFar m_nearFar{};

        struct SwapchainD3D11 {
            xr::SwapchainHandle Handle;
            DXGI_FORMAT Format{DXGI_FORMAT_UNKNOWN};
            uint32_t Width{0};
            uint32_t Height{0};
            uint32_t ArraySize{0};
            std::vector<XrSwapchainImageD3D11KHR> Images;
        };

        struct RenderResources {
            XrViewState ViewState{XR_TYPE_VIEW_STATE};
            std::vector<XrView> Views;
            std::vector<XrViewConfigurationView> ConfigViews;
            SwapchainD3D11 ColorSwapchain;
            SwapchainD3D11 DepthSwapchain;
            std::vector<XrCompositionLayerProjectionView> ProjectionLayerViews;
            std::vector<XrCompositionLayerDepthInfoKHR> DepthInfoViews;
        };

        std::unique_ptr<RenderResources> m_renderResources{};

        bool m_sessionRunning{false};
        XrSessionState m_sessionState{XR_SESSION_STATE_UNKNOWN};
    };
} // namespace

namespace sample {
    std::unique_ptr<sample::IOpenXrProgram> CreateOpenXrProgram(std::string applicationName,
                                                                std::unique_ptr<sample::IGraphicsPluginD3D11> graphicsPlugin) {
        return std::make_unique<ImplementOpenXrProgram>(std::move(applicationName), std::move(graphicsPlugin));
    }
} // namespace sample
