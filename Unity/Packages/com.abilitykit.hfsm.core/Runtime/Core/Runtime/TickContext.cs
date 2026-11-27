#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{
    public readonly struct TickContext
    {
        public TickContext(int frame, Fixed64 time, Fixed64 deltaTime)
        {
            Frame = frame;
            TimeRaw = time.RawValue;
            DeltaTimeRaw = deltaTime.RawValue;
        }

        public int Frame { get; }

        public long TimeRaw { get; }

        public long DeltaTimeRaw { get; }

        public Fixed64 Time => Fixed64.FromRaw(TimeRaw);

        public Fixed64 DeltaTime => Fixed64.FromRaw(DeltaTimeRaw);
    }
}
