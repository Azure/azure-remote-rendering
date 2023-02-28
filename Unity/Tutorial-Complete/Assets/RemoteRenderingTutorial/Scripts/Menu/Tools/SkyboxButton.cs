using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class SkyboxButton : MonoBehaviour
{
    public string skyboxName;
    
    private Interactable interactable;
    public Interactable Interactable
    {
        get
        {
            if (interactable == null) interactable = GetComponent<Interactable>();
            return interactable;
        }
    }
}