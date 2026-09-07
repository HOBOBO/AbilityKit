#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public readonly struct TransitionContext
    {
        internal TransitionContext(
            TickContext tick,
            string machineId,
            string fromStateId,
            TransitionDefinition transition,
            string triggerId,
            long activeSinceRaw)
        {
            Tick = tick;
            MachineId = machineId;
            FromStateId = fromStateId;
            ToStateId = transition.ToStateId;
            TransitionId = transition.Id;
            TriggerId = triggerId;
            ActiveDurationRaw = checked(tick.TimeRaw - activeSinceRaw);
        }

        public TickContext Tick { get; }

        public string MachineId { get; }

        public string FromStateId { get; }

        public string ToStateId { get; }

        public string TransitionId { get; }

        public string TriggerId { get; }

        public long ActiveDurationRaw { get; }

        public Fixed64 ActiveDuration => Fixed64.FromRaw(ActiveDurationRaw);
    }
}
