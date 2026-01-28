using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Triggering.Runtime
{
    public readonly struct ExecCtx<TCtx>
    {
        public readonly TCtx Context;
        public readonly IEventBus EventBus;
        public readonly FunctionRegistry Functions;
        public readonly ActionRegistry Actions;
        public readonly IBlackboardResolver Blackboards;
        public readonly IPayloadAccessorRegistry Payloads;
        public readonly IIdNameRegistry IdNames;
        public readonly ExecPolicy Policy;
        public readonly ExecutionControl Control;

        public ExecCtx(TCtx context, IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, IBlackboardResolver blackboards, IPayloadAccessorRegistry payloads, IIdNameRegistry idNames, ExecPolicy policy, ExecutionControl control)
        {
            Context = context;
            EventBus = eventBus;
            Functions = functions;
            Actions = actions;
            Blackboards = blackboards;
            Payloads = payloads;
            IdNames = idNames;
            Policy = policy;
            Control = control;
        }
    }
}
