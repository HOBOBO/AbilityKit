using System;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface IRunningAction : IDisposable
    {
        bool IsDone { get; }
        void Tick(float deltaTime);
        void Cancel();
    }
}
