#if UNITY_EDITOR
using System;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerPlanCompilerResolvers
    {
        public static int ResolvePayloadFieldId(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return 0;
            return StableStringId.Get("field:" + fieldName);
        }

        public static ActionId ResolveActionId(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return default;
            return new ActionId(StableStringId.Get("action:" + actionName));
        }

        public static FunctionId ResolveFunctionId(string functionName)
        {
            if (string.IsNullOrEmpty(functionName)) return default;
            return new FunctionId(StableStringId.Get("func:" + functionName));
        }

        public static IdNameRegistry CreateIdNamesFor(string actionOrFunctionOrFieldName, int id, EIdNameKind kind)
        {
            var reg = new IdNameRegistry();
            if (string.IsNullOrEmpty(actionOrFunctionOrFieldName) || id == 0) return reg;

            switch (kind)
            {
                case EIdNameKind.Action:
                    reg.RegisterAction(new ActionId(id), actionOrFunctionOrFieldName);
                    break;
                case EIdNameKind.Function:
                    reg.RegisterFunction(new FunctionId(id), actionOrFunctionOrFieldName);
                    break;
                case EIdNameKind.Field:
                    reg.RegisterField(id, actionOrFunctionOrFieldName);
                    break;
            }

            return reg;
        }

        public enum EIdNameKind : byte
        {
            Action = 0,
            Function = 1,
            Field = 2,
        }
    }
}
#endif
