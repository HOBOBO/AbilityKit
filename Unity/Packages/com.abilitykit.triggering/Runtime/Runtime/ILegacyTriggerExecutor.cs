using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime
{
    public interface ILegacyTriggerExecutor
    {
        bool Evaluate<TArgs>(string conditionType, IReadOnlyDictionary<string, object> args, in TArgs eventArgs, in ExecCtx ctx);

        void Execute<TArgs>(string actionType, IReadOnlyDictionary<string, object> args, in TArgs eventArgs, in ExecCtx ctx);
    }
}
