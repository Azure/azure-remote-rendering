// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// A variation of MouseDeviceManager where the device initialization is delayed a bit to avoid other services from
    /// missing the "RaiseSourceDetected" event.
    /// </summary>
    [MixedRealityDataProvider(
        typeof(IMixedRealityInputSystem),
        (SupportedPlatforms)(-1), // All platforms supported by Unity
        "Unity Mouse Device Manager")]
    public class UnityMouseDeviceManager : BaseInputDeviceManager
    {
        private const int DeviceIntializationDelay = 0;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="registrar">The <see cref="IMixedRealityServiceRegistrar"/> instance that loaded the data provider.</param>
        /// <param name="inputSystem">The <see cref="Microsoft.MixedReality.Toolkit.Input.IMixedRealityInputSystem"/> instance that receives data from this provider.</param>
        /// <param name="name">Friendly name of the service.</param>
        /// <param name="priority">Service priority. Used to determine order of instantiation.</param>
        /// <param name="profile">The service's configuration profile.</param>
        public UnityMouseDeviceManager(
            IMixedRealityInputSystem inputSystem,
            string name = null,
            uint priority = DefaultPriority,
            BaseMixedRealityProfile profile = null) : base(inputSystem, name, priority, profile) { }

        /// <summary>
        /// Current Mouse Controller.
        /// </summary>
        public UnityMouseController Controller { get; private set; }

        /// <summary>
        /// Update, create or destroy controller as needed
        /// </summary>
        public override void Update()
        {
            if (UInput.mousePresent)
            {
                CreateSource();
            }
            else
            {
                DestroySource();
            }

            Controller?.Update();
        }

        /// <summary>
        /// Destroy controller
        /// </summary>
        public override void Disable()
        {
            DestroySource();
        }

        /// <summary>
        /// A task that will only complete when the input system has in a valid state.
        /// </summary>
        /// <remarks>
        /// It's possible for this object to have been destroyed after the await, which
        /// implies that callers should check that this != null after awaiting this task.
        /// </remarks>
        protected async Task EnsureInputSystemValid()
        {
            if (CoreServices.InputSystem == null)
            {
                await new WaitUntil(() => CoreServices.InputSystem != null);
            }
        }

        /// <summary>
        /// Initialize the mouse pointer
        /// </summary>
        private void CreateSource()
        {
            if (CoreServices.InputSystem == null || Controller != null)
            {
                return;
            }

            Controller = CreateController();
            CoreServices.InputSystem.RaiseSourceDetected(Controller.InputSource, Controller);
        }

        /// <summary>
        /// Destroy the mouse pointer
        /// </summary>
        private void DestroySource()
        {
            if (Controller == null)
            {
                return;
            }

            var oldController = Controller;
            Controller = null;
            CoreServices.InputSystem?.RaiseSourceLost(oldController.InputSource, oldController);
        }

        /// <summary>
        /// Create a new mouse controller.
        /// </summary>
        private UnityMouseController CreateController()
        {
            IMixedRealityInputSource mouseInputSource = null;
            UnityMouseController mouseController = null;

            var pointers = RequestPointers(SupportedControllerType.Mouse, Handedness.None);
            mouseInputSource = CoreServices.InputSystem.RequestNewGenericInputSource("Mouse Input", pointers);
            mouseController = new UnityMouseController(TrackingState.NotApplicable, Handedness.None, mouseInputSource);

            for (int i = 0; i < mouseInputSource.Pointers.Length; i++)
            {
                mouseInputSource.Pointers[i].Controller = mouseController;
            }

            mouseController.SetupConfiguration(typeof(UnityMouseController));
            return mouseController;
        }
    }
}
