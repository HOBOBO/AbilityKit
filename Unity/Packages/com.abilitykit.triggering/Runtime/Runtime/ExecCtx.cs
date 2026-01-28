using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Triggering.Runtime
{
    public readonly struct ExecCtx
    {
        public readonly TriggerContext Context;
        public readonly IEventBus EventBus;
        public readonly FunctionRegistry Functions;
        public readonly ActionRegistry Actions;
        public readonly IBlackboardResolver Blackboards;
        public readonly IPayloadAccessorRegistry Payloads;
        public readonly IIdNameRegistry IdNames;
        public readonly ILegacyTriggerExecutor Legacy;
        public readonly ExecPolicy Policy;
        public readonly ExecutionControl Control;

        public ExecCtx(TriggerContext context, IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, IBlackboardResolver blackboards, IPayloadAccessorRegistry payloads, IIdNameRegistry idNames, ILegacyTriggerExecutor legacy, ExecPolicy policy, ExecutionControl control)
        {
            Context = context;
            EventBus = eventBus;
            Functions = functions;
            Actions = actions;
            Blackboards = blackboards;
            Payloads = payloads;
            IdNames = idNames;
            Legacy = legacy;
            Policy = policy;
            Control = control;
        }
    }
}
