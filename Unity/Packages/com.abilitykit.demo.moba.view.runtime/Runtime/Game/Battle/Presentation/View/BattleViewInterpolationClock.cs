namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewInterpolationClock
    {
        private double _renderTime;
        private int _lastFrame;
        private bool _initialized;

        public double RenderTime => _renderTime;

        public bool Advance(
            IBattleRuntimeContext ctx,
            float deltaTime,
            float backTimeTicks,
            float maxLagTicks,
            out double sampleTime)
        {
            sampleTime = 0d;
            if (ctx == null) return false;

            var tickRate = ctx.Plan.World.TickRate;
            if (tickRate <= 0) tickRate = 30;

            var fixedDelta = 1d / tickRate;
            var logicTime = ctx.LogicTimeSeconds;
            if (logicTime <= 0d)
            {
                logicTime = ctx.LastFrame * fixedDelta;
            }

            if (backTimeTicks <= 0f) backTimeTicks = 1f;
            var backTime = fixedDelta * backTimeTicks;
            var target = logicTime - backTime;
            if (target < 0d) target = 0d;

            // A newly attached view may join an already advancing simulation.
            // Starting at zero leaves it at MaxLagTicks indefinitely when both
            // clocks advance at the same rate, hiding short-lived projectiles.
            var initialize = !_initialized;
            if (initialize)
            {
                _renderTime = target;
                _initialized = true;
            }

            var frameAdvanced = false;
            var frame = ctx.LastFrame;
            if (initialize || _lastFrame != frame)
            {
                _lastFrame = frame;
                frameAdvanced = true;
                sampleTime = frame * fixedDelta;

                if (_renderTime > target)
                {
                    _renderTime = target;
                }
            }

            if (deltaTime > 0f)
            {
                _renderTime += deltaTime;
            }

            if (_renderTime > target)
            {
                _renderTime = target;
            }

            if (maxLagTicks < 0f) maxLagTicks = 0f;
            var maxLag = backTime + fixedDelta * maxLagTicks;
            var minRenderTime = logicTime - maxLag;
            if (minRenderTime < 0d) minRenderTime = 0d;
            if (_renderTime < minRenderTime)
            {
                _renderTime = minRenderTime;
            }

            return frameAdvanced;
        }

        public void Reset()
        {
            _renderTime = 0d;
            _lastFrame = 0;
            _initialized = false;
        }
    }
}
