using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host.Framework
{
    public sealed class HostRuntime : IWorldHost, IServerConnectionHost
    {
        private readonly IWorldManager _worlds;
        private readonly HostRuntimeOptions _options;
        private readonly object _clientsSync = new object();

        private readonly HostRuntimeFeatures _features = new HostRuntimeFeatures();

        private readonly Dictionary<ServerClientId, IServerConnection> _clients = new Dictionary<ServerClientId, IServerConnection>();

        public HostRuntime(IWorldManager worlds)
            : this(worlds, null)
        {
        }

        public HostRuntime(IWorldManager worlds, HostRuntimeOptions options)
        {
            _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            _options = options;
        }

        public IWorldManager Worlds => _worlds;

        public IHostRuntimeFeatures Features => _features;

        public void Connect(IServerConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            lock (_clientsSync)
            {
                _clients[connection.ClientId] = connection;
            }
        }

        public void Disconnect(ServerClientId clientId)
        {
            lock (_clientsSync)
            {
                _clients.Remove(clientId);
            }
        }

        public bool TryGetWorld(WorldId id, out IWorld world)
        {
            return _worlds.TryGet(id, out world);
        }

        public IWorld CreateWorld(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (_options != null)
            {
                _options.BeforeCreateWorld.Invoke(options);
                _options.OnBeforeCreateWorld?.Invoke(options);
            }

            var world = _worlds.Create(options);

            if (_options != null)
            {
                PublishLifecycleNotification(
                    () => _options.WorldCreated.Invoke(world),
                    "[HostRuntime] WorldCreated hook failed");
                PublishLifecycleNotification(
                    () => _options.OnWorldCreated?.Invoke(world),
                    "[HostRuntime] OnWorldCreated callback failed");
            }

            Broadcast(new WorldCreatedMessage(world.Id, world.WorldType));
            return world;
        }

        public bool DestroyWorld(WorldId id)
        {
            if (!_worlds.Destroy(id)) return false;

            if (_options != null)
            {
                PublishLifecycleNotification(
                    () => _options.WorldDestroyed.Invoke(id),
                    "[HostRuntime] WorldDestroyed hook failed");
                PublishLifecycleNotification(
                    () => _options.OnWorldDestroyed?.Invoke(id),
                    "[HostRuntime] OnWorldDestroyed callback failed");
            }

            Broadcast(new WorldDestroyedMessage(id));
            return true;
        }

        public void Tick(float deltaTime)
        {
            try
            {
                if (_options != null)
                {
                    _options.PreTick.Invoke(deltaTime);
                    _options.OnPreTick?.Invoke(deltaTime);
                }

                var shouldRunWorldTick =
                    !_features.TryGetFeature<IHostRuntimeTickGate>(out var tickGate) ||
                    tickGate == null ||
                    tickGate.ShouldRunWorldTick;
                if (shouldRunWorldTick)
                {
                    _worlds.Tick(deltaTime);
                }

                if (shouldRunWorldTick && _options != null)
                {
                    _options.PostTick.Invoke(deltaTime);
                    _options.OnPostTick?.Invoke(deltaTime);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[HostRuntime] Tick failed");
            }
        }

        public void Broadcast(ServerMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            IServerConnection[] clients;
            lock (_clientsSync)
            {
                clients = new IServerConnection[_clients.Count];
                _clients.Values.CopyTo(clients, 0);
            }

            foreach (var c in clients)
            {
                try
                {
                    SendTo(c, message);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[HostRuntime] Broadcast SendTo failed: clientId={c.ClientId.Value} messageType={message.GetType().Name}");
                }
            }
        }

        public void SendTo(IServerConnection connection, ServerMessage message)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (message == null) throw new ArgumentNullException(nameof(message));

            if (_options != null)
            {
                _options.BeforeSendMessage.Invoke(connection.ClientId, message);
                _options.OnBeforeSendMessage?.Invoke(connection.ClientId, message);
            }

            try
            {
                connection.Send(message);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[HostRuntime] Connection.Send failed: clientId={connection.ClientId.Value} messageType={message.GetType().Name}");
                return;
            }

            if (_options != null)
            {
                _options.AfterSendMessage.Invoke(connection.ClientId, message);
                _options.OnAfterSendMessage?.Invoke(connection.ClientId, message);
            }
        }

        private static void PublishLifecycleNotification(Action notification, string failureMessage)
        {
            try
            {
                notification();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, failureMessage);
            }
        }
    }
}
