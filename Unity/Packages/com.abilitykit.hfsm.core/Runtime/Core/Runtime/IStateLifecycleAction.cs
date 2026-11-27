#nullable enable
using AbilityKit.Deterministic;

namespace AbilityKit.HFSM.Runtime
{
    public interface IStateLifecycleAction<in TOwner>
    {
        void OnEnter(TOwner owner, string stateId, in TickContext context);

        void OnExit(TOwner owner, string stateId, in TickContext context);
    }
}
