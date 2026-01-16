using System;

namespace AbilityKit.Ability.Triggering.Blackboard
{
    public static class TriggerContextBlackboardExtensions
    {
        public static bool TryGetBlackboardResolver(this TriggerContext context, out IBlackboardResolver resolver)
        {
            resolver = null;
            if (context == null) return false;

            var sp = context.Services;
            if (sp == null) return false;

            try
            {
                var obj = sp.GetService(typeof(IBlackboardResolver));
                resolver = obj as IBlackboardResolver;
                return resolver != null;
            }
            catch
            {
                resolver = null;
                return false;
            }
        }

        public static bool TryResolveBlackboard(this TriggerContext context, string boardId, out IBlackboard blackboard)
        {
            blackboard = null;
            if (context == null || boardId == null) return false;
            if (!TryGetBlackboardResolver(context, out var resolver)) return false;
            return resolver.TryResolve(boardId, out blackboard) && blackboard != null;
        }
    }
}
