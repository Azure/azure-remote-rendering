// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;

/// <summary>
/// A menu UX controller for switching between submenus
/// </summary>
public class SubMenuController : MonoBehaviour
{
    private int _activeIndex = 0;

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
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Start()
    {
        for (int i = 1; i < menuContainers.Length; ++i)
        {
            GameObject container = menuContainers[i];
            SetCollidersForContainer(container, false);
            SetSubMenuActiveForContainer(container, false);
        }
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    public void GoToMenu(int index)
    {
        if (index != _activeIndex)
        {
            StartCoroutine(GoToMenuRoutine(index));
        }
    }
    #endregion Public Methods

    #region Private Methods
    private IEnumerator GoToMenuRoutine(int index)
    {
        if (menuContainers == null ||
            index < 0 ||
            index >= menuContainers.Length)
        {
            yield break;
        }


        GameObject firstContainer = menuContainers[0];
        GameObject oldContainer = menuContainers[_activeIndex];
        GameObject newContainer = menuContainers[index];

        if (oldContainer != newContainer)
        {
            SetCollidersForContainer(oldContainer, false);
            SetSubMenuActiveForContainer(oldContainer, false);
        }

        yield return new WaitForSeconds(transitionDelay);

        // enable new container objects
        menuContainers[index].SetActive(true);

        float time = index == 0 ? transitionTime : 0f;
        bool done = false;
        
        while (!done)
        {
            // if we're going to root, slide to the right
            if (index == 0)
            {
                if (time > 0f)
                {
                    time -= Time.deltaTime;
                    firstContainer.transform.localPosition = Vector3.Lerp(activePosition, offscreenLeftPosition, movementCurve.Evaluate(time / transitionTime));
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
                    firstContainer.transform.localPosition = Vector3.Lerp(activePosition, offscreenLeftPosition, movementCurve.Evaluate(time / transitionTime));
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
        _activeIndex = index;
    }

    private void SetCollidersForContainer(GameObject container, bool shouldCollide)
    {
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
