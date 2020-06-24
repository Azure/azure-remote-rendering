// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Input;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class RemoteRayCastPointerHandler : BaseRemoteRayCastPointerHandler, IMixedRealityPointerHandler
{
    public UnityRemoteEntityEvent OnRemoteEntityClicked = new UnityRemoteEntityEvent();

    public override event Action<Entity> RemoteEntityClicked;

    public void Awake()
    {
        // Forward events to Unity events
        RemoteEntityClicked += (entity) => OnRemoteEntityClicked?.Invoke(entity);
    }

    public async void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        if (RemoteEntityClicked != null) //Ensure someone is listening before we do the work
        {
            var firstHit = await PointerDataToRemoteRayCast(eventData.Pointer);
            if (firstHit.success)
                RemoteEntityClicked.Invoke(firstHit.hit.HitEntity);
        }
    }

    public void OnPointerDown(MixedRealityPointerEventData eventData) { }

    public void OnPointerDragged(MixedRealityPointerEventData eventData) { }

    public void OnPointerUp(MixedRealityPointerEventData eventData) { }

    private async Task<(bool success, RayCastHit hit)> PointerDataToRemoteRayCast(IMixedRealityPointer pointer, HitCollectionPolicy hitPolicy = HitCollectionPolicy.ClosestHit)
    {
        RayCastHit hit;
        var result = pointer.Result;
        if (result != null)
        {
            var endPoint = result.Details.Point;
            var direction = pointer.Rays[pointer.Result.RayStepIndex].Direction;
            Debug.DrawRay(endPoint, direction, Color.green, 0);
            hit = (await RemoteRayCaster.RemoteRayCast(endPoint, direction, hitPolicy)).FirstOrDefault();
        }
        else
        {
            hit = new RayCastHit();
        }
        return (hit.HitEntity != null, hit);
    }
}