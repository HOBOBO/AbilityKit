#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public readonly struct TransitionSpec
    {
        public readonly string From;
        public readonly string To;
        public readonly string Condition;
        public readonly TransitionMode Mode;
        public readonly int Priority;
        public readonly bool ForceInstantly;

        public TransitionSpec(
            string from,
            string to,
            string condition,
            TransitionMode mode = TransitionMode.Condition,
            int priority = 0,
            bool forceInstantly = false)
        {
            From = from ?? string.Empty;
            To = to ?? string.Empty;
            Condition = condition ?? string.Empty;
            Mode = mode;
            Priority = priority;
            ForceInstantly = forceInstantly;
        }
    }
}
