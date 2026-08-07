using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Transport.InMemory
{
    /// <summary>
    /// 进程内 <see cref="ITransport"/>：一对 linked 传输，一端 <see cref="Send"/> → 另一端 <see cref="BytesReceived"/>，
    /// 无真实 socket。用于快速 in-process 集成测试（客户端 <c>NetworkSdkClient</c> 全栈 + 一个 in-process 服务端）。
    ///
    /// 用 <see cref="CreateConnectedPair"/> 创建一对。<see cref="Connect"/> 触发 <see cref="Connected"/>
    /// （在 <c>ConnectionManager</c> 订阅事件之后调用，时序与真实传输一致）。<see cref="Send"/> 同步路由到对端的
    /// <see cref="BytesReceived"/>（即时投递，不跨线程 —— 测试代码里 Send 返回前对端已收到）。
    /// </summary>
    public sealed class InMemoryTransport : ITransport
    {
        private readonly object _gate = new object();
        private InMemoryTransport _peer;
        private bool _connected;

        private InMemoryTransport() { }

        /// <summary>创建一对互相 linked 的 in-memory 传输。</summary>
        public static (InMemoryTransport A, InMemoryTransport B) CreateConnectedPair()
        {
            var a = new InMemoryTransport();
            var b = new InMemoryTransport();
            a._peer = b;
            b._peer = a;
            return (a, b);
        }

        public bool IsConnected
        {
            get { lock (_gate) { return _connected; } }
        }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<ArraySegment<byte>> BytesReceived;

        public void Connect(string host, int port)
        {
            lock (_gate)
            {
                if (_peer == null)
                {
                    throw new InvalidOperationException("InMemoryTransport has no peer. Use CreateConnectedPair().");
                }

                _connected = true;
            }

            Connected?.Invoke();
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count <= 0) return;

            InMemoryTransport peer;
            lock (_gate) { peer = _peer; }

            // 即时同步投递到对端（Send 返回前对端 BytesReceived 已触发）。
            peer?.BytesReceived?.Invoke(bytes);
        }

        public void Close()
        {
            bool was;
            lock (_gate)
            {
                was = _connected;
                _connected = false;
            }

            if (was) Disconnected?.Invoke();
        }

        public void Dispose() => Close();
    }
}
