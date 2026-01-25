using System;
using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Joystick
{
    [Serializable]
    public struct JoystickOutput
    {
        public Vector2 Value;
        public float Magnitude;

        public JoystickOutput(Vector2 value, float magnitude)
        {
            Value = value;
            Magnitude = magnitude;
        }
    }
}
