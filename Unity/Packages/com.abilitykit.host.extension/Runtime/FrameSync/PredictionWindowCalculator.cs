using System;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 计算自适应预测窗口，只更新积压平滑值，不推进帧时钟或消费输入。
    /// </summary>
    internal static class PredictionWindowCalculator
    {
        /// <summary>
        /// 本次窗口及理想帧限制结果，停滞计数仍由驱动器维护。
        /// </summary>
        internal readonly struct Result
        {
            internal readonly int RawBacklog;
            internal readonly int Window;
            internal readonly bool CappedByIdeal;
            internal readonly bool StalledByIdeal;

            internal Result(int rawBacklog, int window, bool cappedByIdeal, bool stalledByIdeal)
            {
                RawBacklog = rawBacklog;
                Window = window;
                CappedByIdeal = cappedByIdeal;
                StalledByIdeal = stalledByIdeal;
            }
        }

        internal static Result Calculate(int targetFrame, int confirmedFrame, int inputDelay,
            int minWindow, int maxWindow, float alpha, int idealFrame,
            ref bool hasEwma, ref float backlogEwma)
        {
            var rawBacklog = targetFrame - confirmedFrame;
            if (rawBacklog < 0) rawBacklog = 0;
            if (!hasEwma)
            {
                hasEwma = true;
                backlogEwma = rawBacklog;
            }
            else
            {
                backlogEwma = (alpha * rawBacklog) + ((1f - alpha) * backlogEwma);
            }

            var smoothedBacklog = (int)MathF.Round(backlogEwma);
            if (smoothedBacklog < 0) smoothedBacklog = 0;
            var window = smoothedBacklog + inputDelay;
            if (window < minWindow) window = minWindow;
            if (maxWindow > 0 && window > maxWindow) window = maxWindow;
            if (maxWindow == 0) window = 0;

            if (idealFrame > 0)
            {
                var maxAheadByIdeal = idealFrame - confirmedFrame;
                if (maxAheadByIdeal < 0) maxAheadByIdeal = 0;
                if (maxAheadByIdeal < window)
                    return new Result(rawBacklog, maxAheadByIdeal, true, maxAheadByIdeal == 0 && window > 0);
            }
            return new Result(rawBacklog, window, false, false);
        }
    }
}
