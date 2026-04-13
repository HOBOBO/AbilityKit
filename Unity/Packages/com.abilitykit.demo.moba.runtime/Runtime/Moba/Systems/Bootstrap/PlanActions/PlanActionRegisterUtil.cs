using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// Plan Action 注册工具类
    /// 保留数值转换功能，ID生成已移至 TriggeringConstants
    /// </summary>
    internal static class PlanActionRegisterUtil
    {
        /// <summary>
        /// 获取Action ID（已委托给TriggeringConstants）
        /// </summary>
        public static ActionId GetActionId(string actionName)
        {
            return TriggeringConstants.GetActionId(actionName);
        }

        /// <summary>
        /// 尝试将double转换为int ID
        /// </summary>
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

        /// <summary>
        /// 尝试将double转换为float
        /// </summary>
        public static bool TryToFloat(double raw, out float value)
        {
            value = (float)raw;
            if (float.IsNaN(value) || float.IsInfinity(value)) return false;
            return true;
        }

        /// <summary>
        /// 将double四舍五入为int
        /// </summary>
        public static int ToIntRound(double raw)
        {
            return (int)System.Math.Round(raw);
        }
    }
}
