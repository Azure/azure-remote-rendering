// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Remote = Microsoft.Azure.RemoteRendering;

/// <summary>
/// A class for sharing the state of Azure Remote Rendering Entities with other clients.
/// </summary>
public class SharableObjectEntity : MonoBehaviour
{
    private SharingTargetRoot _rootTarget;
    private bool _handlingTargetEvents;
    private bool _exploding;
    private Double3 _receivedPosition;
    private Remote.Quaternion _receivedQuaternion;
    private Float3 _receivedScale;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sharing target used to send properties updates too. If null at Start(), the target on this object will be used.")]
    private SharingTarget target;

    /// <summary>
    /// The sharing target used to send properties updates too. If null at Start(), the target on this object will be used.
    /// </summary>
    public SharingTarget Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("The remote object that hosts children entities.")]
    private RemoteObject remoteObject;

    /// <summary>
    /// The remote object that hosts children entities.
    /// </summary>
    public RemoteObject RemoteObject
    {
        get => remoteObject;
        set => remoteObject = value;
    }

    [SerializeField]
    [Tooltip("The behavior that will reset children's positions and materials.")]
    private RemoteObjectReset reset;

    /// <summary>
    /// The behavior that will reset a children's position and materials.
    /// </summary>
    public RemoteObjectReset Reset
    {
        get => reset;
        set => reset = value;
    }

    [SerializeField]
    [Tooltip("The behavior that will explode a remote object.")]
    private RemoteObjectExplode explode;

    /// <summary>
    /// The behavior that will explode a remote object.
    /// </summary>
    public RemoteObjectExplode Explode
    {
        get => explode;
        set => explode = value;
    }

    [SerializeField]
    [Tooltip("The behavior that will change materials of entities.")]
    private ChangeMaterial materialChanger;

    /// <summary>
    /// The behavior that will change materials of entities.
    /// </summary>
    public ChangeMaterial MaterialChanger
    {
        get => materialChanger;
        set => materialChanger = value;
    }
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    private void Start()
    {
        if (target == null)
        {
            target = GetComponent<SharingTarget>();
        }

        if (_rootTarget == null)
        {
            _rootTarget = GetComponentInParent<SharingTargetRoot>();
        }

        if (remoteObject == null)
        {
            remoteObject = GetComponent<RemoteObject>();
        }

        if (reset == null)
        {
            reset = GetComponent<RemoteObjectReset>();
        }

        if (explode == null)
        {
            explode = GetComponent<RemoteObjectExplode>();
        }

        if (materialChanger == null)
        {
            materialChanger = GetComponent<ChangeMaterial>();
        }

        if (remoteObject != null)
        {
            remoteObject.Loaded.AddListener(HandleRemoteObjectLoaded);
            if (remoteObject.IsLoaded)
            {
                HandleRemoteObjectLoaded(null);
            }
        }

        if (reset != null)
        {
            reset.ResetCompleted.AddListener(HandleChildPositionsReset);
        }

        if (explode != null)
        {
            explode.ExplodedStated.AddListener(HandleExplodeStarted);
            explode.ExplodeCompleted.AddListener(HandleExplodeCompleted);
        }

        if (materialChanger != null)
        {
            materialChanger.MaterialApplied.AddListener(HandleChildMaterialChanged);
        }
    }

    private void OnDestroy()
    {
        if (reset != null)
        {
            reset.ResetCompleted.RemoveListener(HandleChildPositionsReset);
        }

        if (explode != null)
        {
            explode.ExplodedStated.RemoveListener(HandleExplodeStarted);
            explode.ExplodeCompleted.RemoveListener(HandleExplodeCompleted);
        }

        if (materialChanger != null)
        {
            materialChanger.MaterialApplied.RemoveListener(HandleChildMaterialChanged);
        }

        if (remoteObject != null)
        {
            remoteObject.Loaded.RemoveListener(HandleRemoteObjectLoaded);
        }

        StopHandlingTargetEvents();
        target = null;
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    /// <summary>
    /// Does the server indicate that this object should be exploded.
    /// </summary>
    private bool IsTargetExploded()
    {
        bool exploded;
        return target.TryGetProperty(SharableStrings.ObjectIsExploded, out exploded) && exploded;
    }

    /// <summary>
    /// Only start listening to events after entities have loaded. If an object is not loaded, the entities
    /// won't exist to modify.
    /// </summary>
    private void HandleRemoteObjectLoaded(RemoteObjectLoadedEventData data)
    {
        if (target != null)
        {
            // exploded is special case, that needs to be handled before the other entity properties
            if (IsTargetExploded())
            {
                // event will be handled after explode
                StartExplode();
            }
            else
            {
                // re-registering for events will cause property changes to be replayed.
                StopHandlingTargetEvents();
                StartHandlingTargetEvents();
            }
        }
    }

    /// <summary>
    /// Start listening for server events.
    /// </summary>
    private void StartHandlingTargetEvents()
    {
        if (!_handlingTargetEvents)
        {
            _handlingTargetEvents = true;
            target.ChildPropertyChanged += HandleChildPropertyChanged;
            target.MessageReceived += HandleCommandMessage;
            target.ChildTransformMessageReceived += HandleChildTransformMessage;
        }
    }

    /// <summary>
    /// Stop listening to server events.
    /// </summary>
    private void StopHandlingTargetEvents()
    {
        if (_handlingTargetEvents)
        {
            _handlingTargetEvents = false;
            target.MessageReceived -= HandleCommandMessage;
            target.ChildPropertyChanged -= HandleChildPropertyChanged;
            target.ChildTransformMessageReceived -= HandleChildTransformMessage;
        }
    }

    /// <summary>
    /// Handle an entity's material changing, and notify the other clients of this change.
    /// </summary>
    private void HandleChildMaterialChanged(Entity entity, RemoteMaterial remoteMaterial)
    {
        SendChildMaterial(entity, remoteMaterial);
    }

    /// <summary>
    /// Handle reset event that indicates that all child entities should have their position reset.
    /// </summary>
    private void HandleChildPositionsReset(RemoteObjectResetCompletedEventData args)
    {
        SendReset();
    }

    /// <summary>
    /// Handle explode event that indicates that all child entities should start exploding outwards from the models origin.
    /// </summary>
    private void HandleExplodeStarted()
    {
        StopHandlingTargetEvents();

        // Without this check we can end up in an explosion loop.
        if (!_exploding)
        {
            _exploding = true;
            ResetChildProperties(transform: true, material: false);
            SendExplode();
        }
    }

    /// <summary>
    /// Handle explode event that indicates that all child entities should complete exploding action.
    /// </summary>
    private void HandleExplodeCompleted()
    {
        StartHandlingTargetEvents();
        _exploding = false;
    }

    /// <summary>
    /// Handle child property changes received from the server.  The childTarget points to a child entity that is 
    /// rooted to this object. 
    /// </summary>
    private void HandleChildPropertyChanged(ISharingServiceTarget childTarget, string property, object input)
    {
        switch (input)
        {
            case SharingServiceTransform value when property == SharableStrings.ObjectTransform:
                ReceiveChildTransform(childTarget, value);
                break;

            case string value when property == SharableStrings.ObjectMaterial:
                ReceiveChildMaterial(childTarget, value);
                break;
        }
    }

    /// <summary>
    /// Handle command messages received from the server.
    /// </summary>
    private void HandleCommandMessage(ISharingServiceMessage message)
    {
        if (message.Command == SharableStrings.CommandObjectReset)
        {
            ReceivedReset();
        }
        else if (message.Command == SharableStrings.CommandObjectStartExploding)
        {
            ReceivedExploded();
        }
    }

    /// <summary>
    /// Handle transform messages received from the server. These are special messages that represent the target entity's
    /// transform. The childTarget points to a child entity that is rooted to this object. 
    /// </summary>
    private void HandleChildTransformMessage(ISharingServiceTarget childTarget, SharingServiceTransform transform)
    {
        ReceiveChildTransform(childTarget, transform);
    }

    /// <summary>
    /// Notify other clients for child entity material changes.
    /// </summary>
    private async void SendChildMaterial(Entity child, RemoteMaterial material)
    {
        if (material == null)
        {
            return;
        }

        string materialSerialized = null;
        try
        {
            materialSerialized = await XmlHelper.Serialize(material);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to serialize material.\r\n{ex.ToString()}");
        }

        ISharingServiceTarget target = SharingTargetEntity.CreateTarget(_rootTarget, child);
        target?.SetProperty(SharableStrings.ObjectMaterial, materialSerialized);
    }

    /// <summary>
    /// Handle receiving material changes from the server. The childTarget points to a child entity that is rooted to
    /// this object.
    /// </summary>
    private async void ReceiveChildMaterial(ISharingServiceTarget childTarget, string materialSerialized)
    {
        if (childTarget == null || string.IsNullOrEmpty(materialSerialized) || materialChanger == null)
        {
            return;
        }

        RemoteMaterial material = null;
        try
        {
            material = await XmlHelper.Deserialize<RemoteMaterial>(materialSerialized);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to deserialize material.\r\n{ex.ToString()}");
        }
        Entity child = SharingTargetEntity.ResolveTarget(childTarget, _rootTarget);

        // avoid resending material change events by unsubscribing from the material object's events
        materialChanger.MaterialApplied.RemoveListener(HandleChildMaterialChanged);
        Task materialChangeTask;
        try
        {
            materialChangeTask = materialChanger.Apply(child, material);
        }
        finally
        {
            materialChanger.MaterialApplied.AddListener(HandleChildMaterialChanged);
        }

        try
        {
            await materialChangeTask;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to apply material.\r\n{ex.ToString()}");
        }
    }

    /// <summary>
    /// Notify other clients that the child entities should return to their original positions.
    /// </summary>
    private void SendReset()
    {
        ResetChildProperties(transform: true, material: true);
        if (target != null)
        {
            target.SetProperty(SharableStrings.ObjectIsExploded, null);
            target.SendCommandMessage(SharableStrings.CommandObjectReset);
        }
    }

    /// <summary>
    /// Handle receiving a reset event from the server.
    /// </summary>
    private void ReceivedReset()
    {
        if (reset != null)
        {
            // avoid resending reset events by unsubscribing from the reset object's events
            reset.ResetCompleted.RemoveListener(HandleChildPositionsReset);

            try
            {
                reset.ResetObject(resetMaterials: true);
            }
            finally
            {
                reset.ResetCompleted.AddListener(HandleChildPositionsReset);
            }
        }
    }

    /// <summary>
    /// Notify other clients that the child entities should explode outwards from the models origin.
    /// </summary>
    private void SendExplode()
    {
        if (target != null)
        {
            target.SetProperty(SharableStrings.ObjectIsExploded, true);
            target.SendCommandMessage(SharableStrings.CommandObjectStartExploding);
        }
    }

    /// <summary>
    /// Handle receiving a explode event from the server.
    /// </summary>
    private void ReceivedExploded()
    {
        ResetChildProperties(transform: true, material: false);
        StartExplode();
    }

    /// <summary>
    /// Start exploding this object.
    /// </summary>
    private void StartExplode()
    {
        if (explode != null)
        {
            _exploding = true;
            explode.StartExplode();
        }
    }

    /// <summary>
    /// Handle receiving a child entity's transform and apply it to the entity.  The childTarget points to a child 
    /// entity that is rooted to this object.
    /// </summary>
    private void ReceiveChildTransform(ISharingServiceTarget childTarget, SharingServiceTransform transform)
    {
        Entity childEntity = SharingTargetEntity.ResolveTarget(childTarget, _rootTarget);
        SetChildTransform(childEntity, ref transform);
    }

    /// <summary>
    /// Get the entity children that have been manually moved by the users.
    /// </summary>
    private IReadOnlyList<ISharingServiceTarget> GetMovedChildren()
    {
        if (target == null)
        {
            return null;
        }

        return target.InnerTarget?.Children;
    }

    /// <summary>
    /// Clear the shared positions of children that have been manually moved by the users.
    /// </summary>
    private void ResetChildProperties(bool transform, bool material)
    {
        IReadOnlyList<ISharingServiceTarget> children = GetMovedChildren();
        if (children == null)
        {
            return;
        }

        foreach (var childTarget in children)
        {
            if (transform && childTarget.HasProperty(SharableStrings.ObjectTransform))
            {
                childTarget.SetProperty(SharableStrings.ObjectTransform, null);
            }

            if (transform && childTarget.HasProperty(SharableStrings.ObjectMaterial))
            {
                childTarget.SetProperty(SharableStrings.ObjectMaterial, null);
            }
        }
    }

    /// <summary>
    /// Copy the source transform, to the entity's destination.
    /// </summary>
    private void SetChildTransform(Entity destination, ref SharingServiceTransform source)
    {
        if (destination == null)
        {
            Debug.LogError($"Unable to apply child's shared transform, off of '{name}'. Couldn't find child entity.");
            return;
        }

        if (!destination.Valid)
        {
            Debug.LogError($"Unable to apply child's shared transform, off of '{name}'. Child entity is invalid.");
            return;
        }

        if (destination.Position.x != source.Position.x ||
            destination.Position.y != source.Position.y ||
            destination.Position.z != source.Position.z)
        {
            _receivedPosition.x = source.Position.x;
            _receivedPosition.y = source.Position.y;
            _receivedPosition.z = source.Position.z;
            destination.Position = _receivedPosition;
        }

        if (destination.Rotation.x != source.Rotation.x ||
            destination.Rotation.y != source.Rotation.y ||
            destination.Rotation.z != source.Rotation.z ||
            destination.Rotation.w != source.Rotation.w)
        {
            _receivedQuaternion.x = source.Rotation.x;
            _receivedQuaternion.y = source.Rotation.y;
            _receivedQuaternion.z = source.Rotation.z;
            _receivedQuaternion.w = source.Rotation.w;
            destination.Rotation = _receivedQuaternion;
        }

        if (destination.Scale.x != source.Scale.x ||
            destination.Scale.y != source.Scale.y ||
            destination.Scale.z != source.Scale.z)
        {
            _receivedScale.x = source.Scale.x;
            _receivedScale.y = source.Scale.y;
            _receivedScale.z = source.Scale.z;
            destination.Scale = _receivedScale;
        }
    }
    #endregion Private Functions
}

