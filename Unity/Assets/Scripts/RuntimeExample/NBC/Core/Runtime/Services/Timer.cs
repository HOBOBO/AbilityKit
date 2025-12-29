using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBC
{
    class TimerHandler
    {
        public string key;
        public bool repeat;
        public float delay;
        public bool userFrame;
        public float exeTime;
        public object caller;
        public Action<object> method;
        public object args;
        public bool jumpFrame;

        public void Clear()
        {
            caller = null;
            method = null;
            args = null;
        }

        public void Run(bool withClear)
        {
            if (caller == null)
            {
                Clear();
                return;
            }
            var callback = method;

            if (withClear) Clear();
            callback?.Invoke(args);
        }
    }

    public static class Timer
    {
        static Timer()
        {
            MonoManager.Inst.OnUpdate += Update;
        }


        private static void Update()
        {
            _timer.Update();
        }

        private static readonly TimerData _timer = new TimerData();


        /// <summary>
        /// 定时执行一次
        /// </summary>
        /// <param name="delay">延迟时间(单位为秒)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        public static void Once(float delay, object caller, Action<object> method, object args = null,
            bool coverBefore = true)
        {
            _timer.once(delay, caller, method, args, coverBefore);
        }

        
        
        /// <summary>
        /// 定时重复执行。
        /// </summary>
        /// <param name="delay"> 间隔时间(单位秒)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        /// <param name="jumpFrame">时钟是否跳帧。基于时间的循环回调，单位时间间隔内，如能执行多次回调，出于性能考虑，引擎默认只执行一次，设置jumpFrame=true后，则回调会连续执行多次</param>
        public static void Loop(float delay, object caller, Action<object> method, object args = null,
            bool coverBefore = true,
            bool jumpFrame = false)
        {
            _timer.loop(delay, caller, method, args, coverBefore, jumpFrame);
        }

        /// <summary>
        /// 定时执行一次(基于帧率)。
        /// </summary>
        /// <param name="delay">延迟几帧(单位为帧)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        public static void FrameOnce(int delay, object caller, Action<object> method, object args = null,
            bool coverBefore = true)
        {
            _timer.frameOnce(delay, caller, method, args, coverBefore);
        }

        /// <summary>
        /// 定时重复执行(基于帧率)。
        /// </summary>
        /// <param name="delay">间隔几帧(单位为帧)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        public static void FrameLoop(int delay, object caller, Action<object> method, object args = null,
            bool coverBefore = true)
        {
            _timer.frameLoop(delay, caller, method, args, coverBefore);
        }


        /// <summary>
        /// 清理定时器
        /// </summary>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数</param>
        public static void Clear(object caller, Action<object> method)
        {
            _timer.clear(caller, method);
        }


        /// <summary>
        /// 清理对象身上的所有定时器
        /// </summary>
        /// <param name="caller">执行域(this)</param>
        public static void ClearAll(object caller)
        {
            _timer.clearAll(caller);
        }
    }

    public class TimerData
    {
        // private static int _guid = 1;
        //
        // /// <summary>
        // /// 获取一个全局唯一定时器ID。
        // /// </summary>
        // private static int GetGUID()
        // {
        //     return _guid++;
        // }

        private static readonly Queue<TimerHandler> _pool = new Queue<TimerHandler>();
        private static int _mid = 1;

        /// <summary>
        /// 当前帧开始的时间
        /// </summary>
        public float currTimer = Time.time;

        /** 当前的帧数。*/
        public int currFrame = 0;

        /**@internal 两帧之间的时间间隔,单位毫秒。*/
        private float _delta = 0;

        private float _lastTimer = Time.time;


        private Dictionary<string, TimerHandler> _map = new Dictionary<string, TimerHandler>();

        private List<TimerHandler> _handlers = new List<TimerHandler>();

        private List<TimerHandler> _temp = new List<TimerHandler>();


        private int _count = 0;

        public float delta => _delta;


        internal void Update()
        {
            var frame = currFrame = currFrame + 1;
            var now = Time.time; //Date.now();
            var awake = (now - _lastTimer) > 30000;

            _delta = (now - _lastTimer);
            var timer = currTimer = currTimer + _delta;
            _lastTimer = now;
            //处理handler
            var handlers = _handlers;
            _count = 0;
            for (int i = 0, n = handlers.Count; i < n; i++)
            {
                var handler = handlers[i];
                if (handler.method != null)
                {
                    var t = handler.userFrame ? frame : timer;
                    if (t >= handler.exeTime)
                    {
                        if (handler.repeat)
                        {
                            if (!handler.jumpFrame || awake)
                            {
                                handler.exeTime += handler.delay;
                                handler.Run(false);
                                if (t > handler.exeTime)
                                {
                                    //如果执行一次后还能再执行，做跳出处理，如果想用多次执行，需要设置jumpFrame=true
                                    handler.exeTime += Mathf.Ceil((t - handler.exeTime) / handler.delay) *
                                                       handler.delay;
                                }
                            }
                            else
                            {
                                while (t >= handler.exeTime)
                                {
                                    handler.exeTime += handler.delay;
                                    handler.Run(false);
                                }
                            }
                        }
                        else
                        {
                            handler.Run(true);
                        }
                    }
                }
                else
                {
                    _count++;
                }
            }

            if (_count > 30 || frame % 200 == 0) _clearHandlers();
        }


        private void _clearHandlers()
        {
            var handlers = _handlers;
            for (int i = 0, n = handlers.Count; i < n; i++)
            {
                var handler = handlers[i];
                if (handler.method != null) _temp.Add(handler);
                else _recoverHandler(handler);
            }

            _handlers = _temp;
            handlers.Clear();
            _temp = handlers;
        }


        private void _recoverHandler(TimerHandler handler)
        {
            if (_map[handler.key] == handler)
            {
                _map.Remove(handler.key);
            }

            handler.Clear();
            _pool.Enqueue(handler);
        }

        private TimerHandler _create(bool useFrame, bool repeat, float delay, object caller, Action<object> method,
            object args, bool coverBefore)
        {
            //如果延迟为0，则立即执行
            if (delay <= 0)
            {
                method.Invoke(args);
                return null;
            }

            TimerHandler handler;
            //先覆盖相同函数的计时
            if (coverBefore)
            {
                handler = _getHandler(caller, method);
                if (handler != null)
                {
                    handler.repeat = repeat;
                    handler.userFrame = useFrame;
                    handler.delay = delay;
                    handler.caller = caller;
                    handler.method = method;
                    handler.args = args;
                    handler.exeTime =
                        delay + (useFrame ? currFrame : currTimer + Time.time - _lastTimer);
                    return handler;
                }
            }

            //找到一个空闲的timerHandler
            handler = _pool.Count > 0 ? _pool.Dequeue() : new TimerHandler();
            handler.repeat = repeat;
            handler.userFrame = useFrame;
            handler.delay = delay;
            handler.caller = caller;
            handler.method = method;
            handler.args = args;
            handler.exeTime = delay + (useFrame ? currFrame : currTimer + Time.time - _lastTimer);

            //索引handler
            _indexHandler(handler);

            //插入数组
            _handlers.Add(handler);

            return handler;
        }


        private TimerHandler _getHandler(object caller, Action<object> method)
        {
            // var cid = caller ? caller.$_GID || (caller.$_GID = GetGUID()) : 0;
            // var mid = method.$_TID || (method.$_TID = Timer._mid++);
            var cid = caller.GetHashCode();
            var mid = method.GetHashCode();
            var key = cid + "_" + mid;
            if (_map.TryGetValue(key, out var k)) return k;
            return null;
        }


        private void _indexHandler(TimerHandler handler)
        {
            var caller = handler.caller;
            var method = handler.method;
            // var cid = caller ? caller.$_GID || (caller.$_GID = GetGUID()) : 0;
            // var mid = method.$_TID || (method.$_TID = Timer._mid++);
            var cid = caller.GetHashCode();
            var mid = method.GetHashCode();
            var key = cid + "_" + mid;
            handler.key = key;
            _map[handler.key] = handler;
        }


        /// <summary>
        /// 定时执行一次
        /// </summary>
        /// <param name="delay">延迟时间(单位为秒)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        public void once(float delay, object caller, Action<object> method, object args = null, bool coverBefore = true)
        {
            _create(false, false, delay, caller, method, args, coverBefore);
        }


        /// <summary>
        /// 定时重复执行。
        /// </summary>
        /// <param name="delay"> 间隔时间(单位秒)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        /// <param name="jumpFrame">时钟是否跳帧。基于时间的循环回调，单位时间间隔内，如能执行多次回调，出于性能考虑，引擎默认只执行一次，设置jumpFrame=true后，则回调会连续执行多次</param>
        public void loop(float delay, object caller, Action<object> method, object args = null, bool coverBefore = true,
            bool jumpFrame = false)
        {
            var handler = _create(false, true, delay, caller, method, args, coverBefore);
            if (handler != null) handler.jumpFrame = jumpFrame;
        }

        /// <summary>
        /// 定时执行一次(基于帧率)。
        /// </summary>
        /// <param name="delay">延迟几帧(单位为帧)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        public void frameOnce(int delay, object caller, Action<object> method, object args = null,
            bool coverBefore = true)
        {
            _create(true, false, delay, caller, method, args, coverBefore);
        }

        /// <summary>
        /// 定时重复执行(基于帧率)。
        /// </summary>
        /// <param name="delay">间隔几帧(单位为帧)。</param>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数。</param>
        /// <param name="args">回调参数。</param>
        /// <param name="coverBefore">是否覆盖之前的延迟执行，默认为 true 。</param>
        public void frameLoop(int delay, object caller, Action<object> method, object args = null,
            bool coverBefore = true)
        {
            _create(true, true, delay, caller, method, args, coverBefore);
        }

        public override string ToString()
        {
            return "handlers:" + _handlers.Count + "pool:" + _pool.Count;
        }

        /// <summary>
        /// 清理定时器
        /// </summary>
        /// <param name="caller">执行域(this)。</param>
        /// <param name="method">定时器回调函数</param>
        public void clear(object caller, Action<object> method)
        {
            var handler = _getHandler(caller, method);
            handler?.Clear();
        }


        /// <summary>
        /// 清理对象身上的所有定时器
        /// </summary>
        /// <param name="caller">执行域(this)</param>
        public void clearAll(object caller)
        {
            if (caller == null) return;
            for (int i = 0, n = _handlers.Count; i < n; i++)
            {
                var handler = _handlers[i];
                if (handler.caller == caller)
                {
                    handler.Clear();
                }
            }
        }
    }
}