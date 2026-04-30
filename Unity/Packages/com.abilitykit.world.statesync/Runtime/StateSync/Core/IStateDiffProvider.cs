using System;

namespace AbilityKit.Ability.StateSync
{
    public interface IStateDiffProvider
    {
        IStateDiff ComputeDiff<TState>(TState current, TState previous) where TState : class;
        TState ApplyDiff<TState>(TState baseState, IStateDiff diff) where TState : class;
        byte[] SerializeState<TState>(TState state) where TState : class;
        TState DeserializeState<TState>(byte[] data) where TState : class;
    }
}
