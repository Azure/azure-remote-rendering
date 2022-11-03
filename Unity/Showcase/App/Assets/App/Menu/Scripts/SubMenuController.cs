// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A menu UX controller for switching between submenus
/// </summary>
public class SubMenuController : MonoBehaviour
{
    private int _activeIndex = 0;
    private int _pendingIndex = 0;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sub menu containers to switch between.")]
    private GameObject[] menuContainers = new GameObject[0];

    /// <summary>
    /// The sub menu containers to switch between.
    /// </summary>
    public GameObject[] MenuContainers
    {
        get => menuContainers;
        set => menuContainers = value;
    }

    [SerializeField]
    [Tooltip("The amount of time, in seconds, it'll take to switch between sub menu containers.")]
    private float transitionTime = 1f;

    /// <summary>
    /// The amount of time, in seconds, to switch between sub menu containers.
    /// </summary>
    public float TransitionTime
    {
        get => transitionTime;
        set => transitionTime = value;
    }

    [SerializeField]
    [Tooltip("The animation curve applied to the translation animation that plays when switching between sub menu containers.")]
    private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animation curve applied to the translation animation that plays when switching between sub menu containers.
    /// </summary>
    public AnimationCurve MovementCurve
    {
        get => movementCurve;
        set => movementCurve = value;
    }

    [SerializeField]
    [Tooltip("This is the position of the visible sub menu container.")]
    private Vector3 activePosition = Vector3.zero;

    /// <summary>
    /// This is the position of the visible sub menu container.
    /// </summary>
    public Vector3 ActivePosition
    {
        get => activePosition;
        set => activePosition = value;
    }

    [SerializeField]
    [Tooltip("This is the position immediately left of the visible sub menu container.")]
    private Vector3 offscreenLeftPosition = Vector3.zero;

    /// <summary>
    /// This is the position immediately left of the visible sub menu container.
    /// </summary>
    public Vector3 OffscreenLeftPosition
    {
        get => offscreenLeftPosition;
        set => offscreenLeftPosition = value;
    }

    [SerializeField]
    [Tooltip("This is the position immediately right of the visible sub menu container.")]
    private Vector3 offscreenRightPosition = Vector3.zero;

    /// <summary>
    /// This is the position immediately right of the visible sub menu container.
    /// </summary>
    public Vector3 OffscreenRightPosition
    {
        get => offscreenRightPosition;
        set => offscreenRightPosition = value;
    }

    [SerializeField]
    [Tooltip("The time to wait before playing the transition animation.")]
    private float transitionDelay = 0.1f;

    /// <summary>
    /// The time to wait before playing the transition animation.
    /// </summary>
    public float TransitionDelay
    {
        get => transitionDelay;
        set => transitionDelay = value;
    }

    [SerializeField]
    [Tooltip("Event raised when the active menu changes.")]
    private UnityEvent menuChanged = new UnityEvent();

    /// <summary>
    /// Event raised when the active menu changes.
    /// </summary>
    public UnityEvent MenuChanged => menuChanged;
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Awake()
    {
        for (int i = 1; i < menuContainers.Length; ++i)
        {
            GameObject container = menuContainers[i];
            SetCollidersForContainer(container, false);
            SetSubMenuActiveForContainer(container, false);
            container.SetActive(false);
        }

        if (menuContainers.Length > 0)
        {
            GameObject container = menuContainers[0];
            container.SetActive(true);
            SetCollidersForContainer(container, true);
            SetSubMenuActiveForContainer(container, true);
        }
    }

    private void OnEnable()
    {
        if (_pendingIndex != _activeIndex)
        {
            SnapToMenu(_pendingIndex);
        }
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    public void GoToMenuByObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        int length = menuContainers == null ? 0 : menuContainers.Length;
        for (int i = 0; i < length; i++)
        {
            if (menuContainers[i] == target)
            {
                GoToMenu(i);
                break;
            }
        }
    }

    public void GoToMenu(int index)
    {
        if (index != _activeIndex)
        {
            _pendingIndex = index;
            if (isActiveAndEnabled)
            {
                StartCoroutine(GoToMenuRoutine(index));
            }
            else
            {
                SnapToMenu(index);
            }
        }
    }

    public T GoToMenu<T>()
    {
        T menu = default(T);

        int length = menuContainers == null ? 0 : menuContainers.Length;
        for (int i = 0; i < length; i++)
        {
            menu = menuContainers[i].GetComponent<T>();
            if (menu != null)
            {
                GoToMenu(i);
                break;
            }
        }

        return menu;
    }

    public object GoToMenu(Type type)
    {
        object menu = null;

        int length = menuContainers == null ? 0 : menuContainers.Length;
        for (int i = 0; i < length; i++)
        {
            menu = menuContainers[i].GetComponent(type);
            if (menu != null)
            {
                GoToMenu(i);
                break;
            }
        }

        return menu;
    }
    #endregion Public Methods

    #region Private Methods
    private IEnumerator GoToMenuRoutine(int newIndex)
    {
        if (menuContainers == null ||
            newIndex < 0 ||
            newIndex >= menuContainers.Length)
        {
            yield break;
        }

        var oldIndex = _activeIndex;
        GameObject oldContainer = menuContainers[oldIndex];
        GameObject newContainer = menuContainers[newIndex];

        if (oldContainer != newContainer)
        {
            SetCollidersForContainer(oldContainer, false);
            SetSubMenuActiveForContainer(oldContainer, false);
        }

        yield return new WaitForSeconds(transitionDelay);

        // enable new container objects
        newContainer.SetActive(true);

        bool goingBack = newIndex < oldIndex;
        float time = goingBack ? transitionTime : 0f;
        bool done = false;
        
        while (!done)
        {
            // if we're going to back, slide to the right
            if (goingBack)
            {
                if (time > 0f)
                {
                    time -= Time.deltaTime;
                    newContainer.transform.localPosition = Vector3.Lerp(activePosition, offscreenLeftPosition, movementCurve.Evaluate(time / transitionTime));
                    oldContainer.transform.localPosition = Vector3.Lerp(offscreenRightPosition, activePosition, movementCurve.Evaluate(time / transitionTime));
                }
                else
                {
                    done = true;
                }
            }
            // otherwise, slide to the left
            else
            {
                if (time < transitionTime)
                {
                    time += Time.deltaTime;
                    oldContainer.transform.localPosition = Vector3.Lerp(activePosition, offscreenLeftPosition, movementCurve.Evaluate(time / transitionTime));
                    newContainer.transform.localPosition = Vector3.Lerp(offscreenRightPosition, activePosition, movementCurve.Evaluate(time / transitionTime));
                }
                else
                {
                    done = true;
                }
            }

            yield return null;
        }

        // enable collision
        SetSubMenuActiveForContainer(newContainer, true);
        SetCollidersForContainer(newContainer, true);
        oldContainer.SetActive(false);
        SetActiveIndex(newIndex);
    }

    private void SnapToMenu(int index)
    {
        if (menuContainers == null ||
            index < 0 ||
            index >= menuContainers.Length)
        {
            return;
        }

        GameObject oldContainer = menuContainers[_activeIndex];
        GameObject newContainer = menuContainers[index];

        if (oldContainer != newContainer)
        {
            oldContainer.SetActive(false);
            SetCollidersForContainer(oldContainer, false);
            SetSubMenuActiveForContainer(oldContainer, false);
            newContainer.transform.localPosition = activePosition;
            SetCollidersForContainer(newContainer, true);
            SetSubMenuActiveForContainer(newContainer, true);
            newContainer.SetActive(true);
            SetActiveIndex(index);
        }
    }

    private void SetActiveIndex(int index)
    {
        if (_activeIndex != index)
        {
            _activeIndex = index;
            menuChanged?.Invoke();
        }
    }

    private void SetCollidersForContainer(GameObject container, bool shouldCollide)
    {
        if (container == null)
        {
            return;
        }

        Collider[] colliders = container.GetComponentsInChildren<Collider>(includeInactive: true);
        foreach (Collider collider in colliders)
        {
            collider.enabled = shouldCollide;
        }
    }
    private void SetSubMenuActiveForContainer(GameObject container, bool active)
    {
        SubMenu[] subMenus = container.GetComponentsInChildren<SubMenu>(includeInactive: true);
        foreach (SubMenu subMenu in subMenus)
        {
            if (active)
            {
                subMenu.OnActivated();
            }
            else
            {
                subMenu.OnDeactivated();
            }
        }
    }
    #endregion Private Methods
}
