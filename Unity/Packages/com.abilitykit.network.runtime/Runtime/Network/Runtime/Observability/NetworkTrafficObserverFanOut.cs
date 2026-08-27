#nullable enable

using System;

namespace AbilityKit.Network.Runtime.Observability
{
    /// <summary>Forwards one immutable traffic event to several independent observers.</summary>
    public sealed class NetworkTrafficObserverFanOut : INetworkTrafficObserver
    {
        private readonly INetworkTrafficObserver[] _observers;
        private readonly Action<Exception>? _errorHandler;

        public NetworkTrafficObserverFanOut(
            INetworkTrafficObserver[] observers,
            Action<Exception>? errorHandler = null)
        {
            if (observers == null) throw new ArgumentNullException(nameof(observers));
            if (observers.Length == 0)
                throw new ArgumentException("At least one traffic observer is required.", nameof(observers));

            _observers = new INetworkTrafficObserver[observers.Length];
            for (var i = 0; i < observers.Length; i++)
            {
                _observers[i] = observers[i] ?? throw new ArgumentException(
                    "Traffic observer collections cannot contain null values.", nameof(observers));
            }
            _errorHandler = errorHandler;
        }

        public void OnTraffic(NetworkTrafficEvent trafficEvent)
        {
            if (trafficEvent == null) throw new ArgumentNullException(nameof(trafficEvent));

            for (var i = 0; i < _observers.Length; i++)
            {
                try
                {
                    _observers[i].OnTraffic(trafficEvent);
                }
                catch (Exception exception)
                {
                    try { _errorHandler?.Invoke(exception); }
                    catch { }
                }
            }
        }
    }
}
