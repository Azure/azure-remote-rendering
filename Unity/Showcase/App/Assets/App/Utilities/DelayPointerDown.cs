using Microsoft.MixedReality.Toolkit.Input;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
// Handle forwarding events to proxy object late in the frame. We need this workaround for
// some implementors of IMixedRealityPointerHandler for their OnPointerDown behavior.
// The reason for that is that Start() for the ObjectManipulator component on new proxy
// object hasn't been called in all cases when OnPointerDown() gets called. This resulted
// in events that were forwarded to uninitialized ObjectManipulators which resulted in
// jumpy behavior, for example when using the move piece tool.
/// </summary>
class DelayPointerDown : MonoBehaviour
{
    private MixedRealityPointerEventData _pendingPointerDown = null;
    private bool _started = false;
    private Action _pendingFired = null;

    public void Start()
    {
        _started = true;
        if (_pendingPointerDown != null)
        {
            Fire(_pendingPointerDown, _pendingFired);
            _pendingPointerDown = null;
            _pendingFired = null;
        }
    }

    public void Fire(MixedRealityPointerEventData pointerDownEventData, Action fired = null)
    {
        if (_started)
        {
            RouteEventToCurrentObject(pointerDownEventData, OnPointerDownEventHandler);
            _pendingPointerDown = null;
            fired?.Invoke();
        }
        else
        {
            _pendingPointerDown = pointerDownEventData;
            _pendingFired = fired;
        }
    }

    private static readonly ExecuteEvents.EventFunction<IMixedRealityPointerHandler> OnPointerDownEventHandler =
        delegate (IMixedRealityPointerHandler handler, BaseEventData eventData)
        {
            if (eventData != null)
            {
                var casted = ExecuteEvents.ValidateEventData<MixedRealityPointerEventData>(eventData);
                handler.OnPointerDown(casted);
            }
        };

    private void RouteEventToCurrentObject<T>(BaseEventData eventData, ExecuteEvents.EventFunction<T> eventFunction) where T : IEventSystemHandler
    {
        ExecuteEvents.Execute(gameObject, eventData, eventFunction);
    }
}
