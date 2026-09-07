#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;


namespace AbilityKit.HFSM.Definition
{

    public sealed class TransitionDefinition
    {
        public string Id { get; set; } = string.Empty;

        public bool FromAnyState { get; set; }

        public string FromStateId { get; set; } = string.Empty;

        public string ToStateId { get; set; } = string.Empty;

        /// <summary>An empty trigger id means the transition is evaluated during Tick.</summary>
        public string TriggerId { get; set; } = string.Empty;

        /// <summary>An empty condition key means unconditional.</summary>
        public string ConditionKey { get; set; } = string.Empty;

        /// <summary>An optional before/after transition action registered by stable key.</summary>
        public string ActionKey { get; set; } = string.Empty;

        /// <summary>Higher values are evaluated first. Equal priorities are ordered by id ordinally.</summary>
        public int Priority { get; set; }

        public bool ForceImmediate { get; set; }

        /// <summary>Minimum active-state duration encoded as a Q32.32 raw value.</summary>
        public long MinimumActiveDurationRaw { get; set; }

    }
}
