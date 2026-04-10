using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.Share.Impl.Moba.Systems;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Battle.Requests;

namespace AbilityKit.Game.Battle
{
    public sealed class InMemoryBattleLogicTransport : IBattleLogicTransport, IHostClient
    {
        private readonly HostRuntime _server;
        private readonly ServerClientId _clientId;
        private HostClientConnectionAdapter _connection;

        private readonly Dictionary<WorldId, List<JoinWorldRequest>> _pendingJoins = new Dictionary<WorldId, List<JoinWorldRequest>>();
        private readonly Dictionary<WorldId, List<LeaveWorldRequest>> _pendingLeaves = new Dictionary<WorldId, List<LeaveWorldRequest>>();
        private readonly Dictionary<WorldId, List<SubmitInputRequest>> _pendingInputs = new Dictionary<WorldId, List<SubmitInputRequest>>();

        public InMemoryBattleLogicTransport(HostRuntime server, string clientId = "in_memory")
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _clientId = new ServerClientId(clientId);
        }

        public event Action<FramePacket> FramePushed;

        public ServerClientId ClientId => _clientId;

        public void Connect()
        {
            _connection ??= new HostClientConnectionAdapter(this);
            _server.Connect(_connection);
        }

        public void Disconnect()
        {
            _server.Disconnect(_clientId);
        }

        public void SendCreateWorld(CreateWorldRequest request)
        {
            var options = request.Options;
            options.ServiceBuilder ??= AbilityKit.Ability.World.Services.WorldServiceContainerFactory.CreateDefaultOnly();
            options.ServiceBuilder.RegisterInstance(new WorldInitData(request.OpCode, request.Payload));

            // Ensure SkillExecutor dependencies are resolvable in local/in-memory worlds.
            // Server worlds typically register IFrameTime via ServerFrameTimeModule.
            options.ServiceBuilder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new FrameTime());

            // Defensive: upstream may already have added MobaWorldBootstrapModule (possibly multiple times).
            // EntitasWorldComposer requires module types to be unique.
            var bootstrapSeen = false;
            for (int i = options.Modules.Count - 1; i >= 0; i--)
            {
                var m = options.Modules[i];
                if (m == null) continue;

                if (m.GetType() != typeof(MobaWorldBootstrapModule)) continue;

                if (!bootstrapSeen)
                {
                    bootstrapSeen = true;
                    continue;
                }

                options.Modules.RemoveAt(i);
            }

            _server.CreateWorld(options);

            if (!_server.TryGetWorld(options.Id, out var created) || created == null)
            {
                Log.Error($"[InMemoryBattleLogicTransport] CreateWorld succeeded but TryGetWorld failed. worldId={options.Id}");
            }

            TryFlushPending(options.Id);
        }

        public void SendJoin(JoinWorldRequest request)
        {
            if (TryApplyJoin(request)) return;

            if (!_pendingJoins.TryGetValue(request.WorldId, out var list) || list == null)
            {
                list = new List<JoinWorldRequest>(4);
                _pendingJoins[request.WorldId] = list;
            }
            list.Add(request);
        }

        public void SendLeave(LeaveWorldRequest request)
        {
            if (TryApplyLeave(request)) return;

            if (!_pendingLeaves.TryGetValue(request.WorldId, out var list) || list == null)
            {
                list = new List<LeaveWorldRequest>(4);
                _pendingLeaves[request.WorldId] = list;
            }
            list.Add(request);
        }

        private bool TryApplyJoin(in JoinWorldRequest request)
        {
            if (_server.TryGetWorld(request.WorldId, out var world) && world != null)
            {
                return true;
            }

            Log.Error($"[InMemoryBattleLogicTransport] Join ignored: world not found. worldId={request.WorldId}, playerId={request.PlayerId.Value}");
            return false;
        }

        private bool TryApplyLeave(in LeaveWorldRequest request)
        {
            if (_server.TryGetWorld(request.WorldId, out var world) && world != null)
            {
                return true;
            }
            return false;
        }

        private void TryFlushPending(WorldId worldId)
        {
            if (_pendingJoins.TryGetValue(worldId, out var joins) && joins != null && joins.Count > 0)
            {
                for (int i = 0; i < joins.Count; i++)
                {
                    TryApplyJoin(joins[i]);
                }
                joins.Clear();
            }

            if (_pendingLeaves.TryGetValue(worldId, out var leaves) && leaves != null && leaves.Count > 0)
            {
                for (int i = 0; i < leaves.Count; i++)
                {
                    TryApplyLeave(leaves[i]);
                }
                leaves.Clear();
            }

            if (_pendingInputs.TryGetValue(worldId, out var inputs) && inputs != null && inputs.Count > 0)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    TryApplyInput(inputs[i]);
                }
                inputs.Clear();
            }
        }

        public void SendInput(SubmitInputRequest request)
        {
            if (TryApplyInput(request)) return;

            if (!_pendingInputs.TryGetValue(request.WorldId, out var list) || list == null)
            {
                list = new List<SubmitInputRequest>(8);
                _pendingInputs[request.WorldId] = list;
            }
            list.Add(request);
        }

        private bool TryApplyInput(in SubmitInputRequest request)
        {
            if (_server.Features.TryGetFeature<IFrameSyncInputHub>(out var hub) && hub != null)
            {
                var ok = hub.SubmitInput(_clientId, request.WorldId, request.Input);
                if (!ok)
                {
                    Log.Error($"[InMemoryBattleLogicTransport] SubmitInput rejected by IFrameSyncInputHub. worldId={request.WorldId}, clientId={_clientId.Value}, opCode={request.Input.OpCode}");
                }
                return ok;
            }

            Log.Error($"[InMemoryBattleLogicTransport] SubmitInput failed: IFrameSyncInputHub feature not installed. worldId={request.WorldId}, clientId={_clientId.Value}");
            return false;
        }

        public void OnMessage(ServerMessage message)
        {
            if (message is FrameMessage frame)
            {
                FramePushed?.Invoke(frame.Packet);
            }
        }
    }
}
