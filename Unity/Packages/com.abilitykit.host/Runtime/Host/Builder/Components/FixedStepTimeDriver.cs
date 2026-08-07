using System;
using System.Threading;
using AbilityKit.Ability.Host.Framework;

namespace AbilityKit.Ability.Host.Builder.Components
{
    /// <summary>
    /// 固定步长时间驱动
    /// 使用定时器按固定帧率驱动 HostRuntime
    /// </summary>
    public sealed class FixedStepTimeDriver : ITimeDriver
    {
        private readonly object _lifecycleSync = new object();
        private HostRuntime _runtime;
        private HostRuntimeOptions _options;
        private Timer _timer;
        private int _frameRate = 30;
        private int _isRunning;
        private int _isTicking;

        public bool IsRunning => Volatile.Read(ref _isRunning) != 0;

        public int FrameRate
        {
            get => Volatile.Read(ref _frameRate);
            set => Volatile.Write(ref _frameRate, Math.Max(1, value));
        }

        public void Attach(HostRuntime runtime, HostRuntimeOptions options)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (options == null) throw new ArgumentNullException(nameof(options));

            lock (_lifecycleSync)
            {
                if (_isRunning != 0)
                    throw new InvalidOperationException("Cannot attach a time driver while it is running.");

                _runtime = runtime;
                _options = options;
            }
        }

        public void Detach()
        {
            Stop();
            lock (_lifecycleSync)
            {
                _runtime = null;
                _options = null;
            }
        }

        public void Start()
        {
            lock (_lifecycleSync)
            {
                if (_isRunning != 0 || _runtime == null)
                    return;

                var intervalMs = Math.Max(1, (int)Math.Round(1000.0 / _frameRate));
                var timer = new Timer(Tick, null, Timeout.Infinite, Timeout.Infinite);
                try
                {
                    Volatile.Write(ref _isRunning, 1);
                    timer.Change(intervalMs, intervalMs);
                    _timer = timer;
                }
                catch
                {
                    Volatile.Write(ref _isRunning, 0);
                    timer.Dispose();
                    throw;
                }
            }
        }

        public void Stop()
        {
            Timer timer;
            lock (_lifecycleSync)
            {
                Volatile.Write(ref _isRunning, 0);
                timer = _timer;
                _timer = null;
            }

            timer?.Dispose();
        }

        private void Tick(object state)
        {
            if (Volatile.Read(ref _isRunning) == 0 || Interlocked.CompareExchange(ref _isTicking, 1, 0) != 0)
                return;

            try
            {
                HostRuntime runtime;
                lock (_lifecycleSync)
                {
                    if (_isRunning == 0)
                        return;
                    runtime = _runtime;
                }

                runtime?.Tick(1.0f / Volatile.Read(ref _frameRate));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FixedStepTimeDriver] Tick exception: {ex}");
            }
            finally
            {
                Volatile.Write(ref _isTicking, 0);
            }
        }
    }
}
