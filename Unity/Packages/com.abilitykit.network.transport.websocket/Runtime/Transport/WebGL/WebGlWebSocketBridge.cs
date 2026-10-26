#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AOT;

namespace AbilityKit.Network.Transport.WebSocket.WebGL
{
    internal sealed class WebGlWebSocketBridge : IDisposable
    {
        private delegate void OpenCallback(int id);
        private delegate void MessageCallback(int id, IntPtr data, int length);
        private delegate void CloseCallback(int id);
        private delegate void ErrorCallback(int id, int code);

        private static readonly ConcurrentDictionary<int, WebGlWebSocketBridge> Instances = new();
        private static readonly OpenCallback OpenHandler = OnOpen;
        private static readonly MessageCallback MessageHandler = OnMessage;
        private static readonly CloseCallback CloseHandler = OnClose;
        private static readonly ErrorCallback ErrorHandler = OnError;

        private int _id;

        public bool IsConnected { get; private set; }
        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<ArraySegment<byte>> BytesReceived;

        public void Connect(Uri uri)
        {
            if (_id != 0) throw new InvalidOperationException("WebSocket already started.");
            _id = Create(uri.AbsoluteUri, OpenHandler, MessageHandler, CloseHandler, ErrorHandler);
            if (_id == 0) throw new InvalidOperationException("WebGL WebSocket bridge could not allocate a socket.");
            Instances[_id] = this;
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected.");
            SendBytes(_id, bytes.Array, bytes.Offset, bytes.Count);
        }

        public void Dispose()
        {
            var id = _id;
            _id = 0;
            IsConnected = false;
            if (id == 0) return;
            Instances.TryRemove(id, out _);
            CloseSocket(id);
        }

        [MonoPInvokeCallback(typeof(OpenCallback))]
        private static void OnOpen(int id)
        {
            if (!Instances.TryGetValue(id, out var instance)) return;
            instance.IsConnected = true;
            instance.Connected?.Invoke();
        }

        [MonoPInvokeCallback(typeof(MessageCallback))]
        private static void OnMessage(int id, IntPtr data, int length)
        {
            if (!Instances.TryGetValue(id, out var instance) || length <= 0) return;
            var bytes = new byte[length];
            Marshal.Copy(data, bytes, 0, length);
            instance.BytesReceived?.Invoke(new ArraySegment<byte>(bytes));
        }

        [MonoPInvokeCallback(typeof(CloseCallback))]
        private static void OnClose(int id)
        {
            if (!Instances.TryRemove(id, out var instance)) return;
            instance._id = 0;
            instance.IsConnected = false;
            instance.Disconnected?.Invoke();
        }

        [MonoPInvokeCallback(typeof(ErrorCallback))]
        private static void OnError(int id, int code)
        {
            if (!Instances.TryGetValue(id, out var instance)) return;
            instance.Error?.Invoke(new InvalidOperationException("WebGL WebSocket error: " + code));
        }

        [DllImport("__Internal", EntryPoint = "AbilityKitWebSocketCreate")]
        private static extern int Create(string url, OpenCallback onOpen, MessageCallback onMessage, CloseCallback onClose, ErrorCallback onError);

        [DllImport("__Internal", EntryPoint = "AbilityKitWebSocketSendBytes")]
        private static extern void SendBytes(int id, byte[] bytes, int offset, int length);

        [DllImport("__Internal", EntryPoint = "AbilityKitWebSocketClose")]
        private static extern void CloseSocket(int id);
    }
}
#endif
