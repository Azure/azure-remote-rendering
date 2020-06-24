using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NotificationBar : MonoBehaviour {
    public TextMeshPro notificationText;

    private static NotificationBar instance;

    private void Awake() {
        instance = this;
    }

    public static void Message(string msg) {
        Debug.Log("Notification: " + msg);
        instance.notificationText.text = msg;
    }

    private void OnDestroy() {
        instance = null;
    }
}
