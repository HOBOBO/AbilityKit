#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public enum RuntimeEventType
    {
        Initialized = 0,
        StateEntered = 1,
        ExitRequested = 2,
        StateExited = 3,
        TransitionCompleted = 4,
        Shutdown = 5,
        Faulted = 6,
        Restored = 7,
    }

    public readonly struct RuntimeEvent
    {
        internal RuntimeEvent(
            RuntimeEventType type,
            TickContext tick,
            string machineId,
            string stateId,
            string transitionId,
            string triggerId)
        {
            Type = type;
            Tick = tick;
            MachineId = machineId;
            StateId = stateId;
            TransitionId = transitionId;
            TriggerId = triggerId;
        }

        public RuntimeEventType Type { get; }

        public TickContext Tick { get; }

        public string MachineId { get; }

        public string StateId { get; }

        public string TransitionId { get; }

        public string TriggerId { get; }
    }
}
