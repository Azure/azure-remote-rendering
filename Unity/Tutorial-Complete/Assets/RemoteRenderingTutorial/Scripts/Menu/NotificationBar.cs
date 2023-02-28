using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NotificationBar : MonoBehaviour {
    public TextMeshPro notificationText;
    private static NotificationBar instance;
    private float displayDuration;
    private float displayElapsed;

    private void Awake() {
        instance = this;
    }

    public static void Message(string msg, float timeout = 5.0f) {
        Debug.Log("Notification: " + msg);

        if (instance != null)
        {
            instance.notificationText.text = msg;

            instance.gameObject.SetActive(true);
            instance.displayDuration = timeout;
            instance.displayElapsed = 0;
        }
    }

    public void Update()
    {
        displayElapsed += Time.deltaTime;
        if (displayElapsed > displayDuration)
        {
            gameObject.SetActive(false);
            notificationText.text = "";
        }
    }

    private void OnDestroy() {
        instance = null;
    }
}
