using System;
using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Joystick
{
    [Serializable]
    public struct JoystickConfig
    {
        public float Radius;
        public float DeadZone;
        public bool HideWhenReleased;

        public static JoystickConfig Default => new JoystickConfig
        {
            Radius = 120f,
            DeadZone = 8f,
            HideWhenReleased = true,
        };
    }
}
