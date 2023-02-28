using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DirectionalLightToggleButton : MonoBehaviour
{
    public DirectionalLightViewController lightMenu;
    public TextMeshPro buttonText;

    // protected void UpdateToggleState()
    // {
    //     base.UpdateToggleState();
    //     // Toggle text
    //     buttonText.text = ToggleState ? "Locked" : "Unlocked";
    //
    //     if (lightMenu.remoteLightMove != null)
    //     {
    //         // Turn manipulation on/off
    //         lightMenu.remoteLightMove.enabled = !ToggleState;
    //     }
    //     else if (ToggleState == false)
    //     {
    //         // Stay locked if no manipulator found
    //         ToggleState = true;
    //     }
    // }
}