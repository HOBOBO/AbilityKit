using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync
{
    public interface IStateManager
    {
        void RegisterRollbackable(IRollbackable entity);
        void UnregisterRollbackable(long entityId);
        void CaptureState(int frame);
        bool TryRestore(int frame);
        IStateDiff ComputeDiff(int fromFrame, int toFrame);
        byte[] GetFullState(int frame);
        IReadOnlyList<int> GetCapturedFrames();
        void ClearHistory();
    }
}
