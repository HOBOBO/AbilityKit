using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    internal static class PlanActionRegisterUtil
    {
        public static ActionId GetActionId(string actionName)
        {
            return new ActionId(AbilityKit.Triggering.Eventing.StableStringId.Get("action:" + actionName));
        }

        public static bool TryToIntId(double raw, out int id, string logScope)
        {
            id = 0;

            if (double.IsNaN(raw) || double.IsInfinity(raw)) return false;
            if (raw <= int.MinValue || raw >= int.MaxValue) return false;

            var rounded = System.Math.Round(raw);
            if (System.Math.Abs(raw - rounded) > 0.000001d)
            {
                if (!string.IsNullOrEmpty(logScope))
                {
                    Log.Warning($"[{logScope}] id arg is not integer; will round. raw={raw} rounded={rounded}");
                }
            }

            id = (int)rounded;
            return true;
        }

        public static bool TryToFloat(double raw, out float value)
        {
            value = (float)raw;
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            return true;
        }

        public static int ToIntRound(double raw)
        {
            return (int)System.Math.Round(raw);
        }
    }
}
