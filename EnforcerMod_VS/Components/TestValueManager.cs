﻿using UnityEngine;

public class TestValueManager : MonoBehaviour {

    //how do doing attributes
    //[debugfloat("valuename", KeyCode.U, KeyCode.J, 5)]
    //would be 
    public static float testValue = 1f;
    public static float testValue2 = 1f;
    public static float testValue3 = 1f;

    private float tim;
    private float holdTime = 0.5f;

    //compiler flags when
    private bool testingEnabled = true;

    void Update() {
        if (!testingEnabled)
            return;

        if (!Input.GetKey(KeyCode.LeftAlt))
            return;

        manageTestValue(ref testValue, "m2 slam size", KeyCode.Keypad7, KeyCode.Keypad4, 0.1f);
        manageTestValue(ref testValue2, "m2 slam height", KeyCode.Keypad9, KeyCode.Keypad6, 0.1f);
    }

    private void manageTestValue(ref float value, string valueName, KeyCode upKey, KeyCode downKey, float incrementAmount) {

        if (Input.GetKeyDown(upKey)) {

            value = setTestValue(value + incrementAmount, valueName);
        }

        if (Input.GetKeyDown(downKey)) {

            value = setTestValue(value - incrementAmount, valueName);
        }


        if (Input.GetKey(upKey) || Input.GetKey(downKey)) {

            float amount = incrementAmount * (Input.GetKey(upKey) ? 1 : -1);

            tim += Time.deltaTime;

            if (tim > holdTime) {

                value = setTestValue(value + amount, valueName);
            }
        }


        if (Input.GetKeyUp(upKey) || Input.GetKeyUp(downKey)) {
            tim = 0;
        }

    }

    private float setTestValue(float value, string print) {
        Debug.LogWarning($"{print}: {value}");
        return value;
    }
}