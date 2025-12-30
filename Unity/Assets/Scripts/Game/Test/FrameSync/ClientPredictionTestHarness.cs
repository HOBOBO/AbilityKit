using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using UnityEngine;

namespace AbilityKit.Game.Test.FrameSync
{
    public sealed class ClientPredictionTestHarness : MonoBehaviour
    {
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool autoTick = true;
        [SerializeField] private float fixedDelta = 1f / 30f;
        [SerializeField] private int rollbackBufferFrames = 240;

        [Header("World")]
        [SerializeField] private string worldType = "battle";
        [SerializeField] private string authoritativeWorldId = "room_auth";
        [SerializeField] private string predictedWorldId = "room_pred";
        [SerializeField] private string playerId = "p1";

        [Header("Desync")]
        [SerializeField] private bool forceDesync = true;
        [SerializeField] private int desyncAtFrame = 45;

        private IWorld _auth;
        private IWorld _pred;

        private IWorldInputSink _authInput;
        private IWorldInputSink _predInput;

        private MobaActorRegistry _authRegistry;
        private MobaActorRegistry _predRegistry;

        private MobaLobbyStateService _authLobby;
        private MobaLobbyStateService _predLobby;

        private ClientPredictionRunner _runner;
        private RollbackCoordinator _rollback;

        private RollbackCoordinator _authRollback;

        private float _acc;
        private FrameIndex _frame;

        private void Start()
        {
            if (autoStart)
            {
                StartSession();
            }
        }

        private void OnDisable()
        {
            StopSession();
        }

        private void Update()
        {
            if (!autoTick) return;
            if (_auth == null || _pred == null) return;

            _acc += Time.deltaTime;
            while (_acc >= fixedDelta)
            {
                StepOnce();
                _acc -= fixedDelta;
            }
        }

        public void StartSession()
        {
            StopSession();

            _frame = new FrameIndex(0);
            _acc = 0f;

            _auth = CreateWorld(new WorldId(authoritativeWorldId));
            _pred = CreateWorld(new WorldId(predictedWorldId));

            _authInput = _auth.Services.Get<IWorldInputSink>();
            _predInput = _pred.Services.Get<IWorldInputSink>();

            _authRegistry = _auth.Services.Get<MobaActorRegistry>();
            _predRegistry = _pred.Services.Get<MobaActorRegistry>();

            _authLobby = _auth.Services.Get<MobaLobbyStateService>();
            _predLobby = _pred.Services.Get<MobaLobbyStateService>();

            BootstrapLobby(_auth, _authLobby);
            BootstrapLobby(_pred, _predLobby);

            var registry = new RollbackRegistry();
            registry.Register(new MobaActorTransformRollbackProvider(_predRegistry));
            registry.Register(new MobaMoveRollbackProvider(_pred.Services.Get<MobaMoveService>()));
            _rollback = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(rollbackBufferFrames));

            var authRegistry = new RollbackRegistry();
            authRegistry.Register(new MobaActorTransformRollbackProvider(_authRegistry));
            authRegistry.Register(new MobaMoveRollbackProvider(_auth.Services.Get<MobaMoveService>()));
            _authRollback = new RollbackCoordinator(authRegistry, new RollbackSnapshotRingBuffer(rollbackBufferFrames));

            var predictedHashBuffer = new WorldStateHashRingBuffer(rollbackBufferFrames);
            var reconciler = new ClientPredictionReconciler(predictedHashBuffer);

            _runner = new ClientPredictionRunner(_pred, _predInput, _rollback, new InputHistoryRingBuffer(rollbackBufferFrames), reconciler)
            {
                Log = msg => Debug.Log($"[PredRunner] {msg}")
            };

            Debug.Log("ClientPredictionTestHarness started");
        }

        public void StopSession()
        {
            _runner = null;
            _rollback = null;

            _authInput = null;
            _predInput = null;

            _authRegistry = null;
            _predRegistry = null;

            _authLobby = null;
            _predLobby = null;

            _auth?.Dispose();
            _pred?.Dispose();
            _auth = null;
            _pred = null;
        }

        private IWorld CreateWorld(WorldId id)
        {
            var reg = new WorldTypeRegistry().RegisterEntitasWorld(worldType);
            var manager = new WorldManager(new RegistryWorldFactory(reg));

            var enterReq = new EnterMobaGameReq(
                playerId: new PlayerId(playerId),
                matchId: "test",
                mapId: 1,
                teamId: 1,
                heroId: 1,
                randomSeed: 123,
                tickRate: 30,
                inputDelayFrames: 0,
                opCode: 0,
                payload: Array.Empty<byte>());

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                WorldServiceProfile.Client,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(MobaWorldBootstrapModule).Assembly,
                    typeof(ClientPredictionTestHarness).Assembly
                },
                new[] { "AbilityKit" }
            );

            builder.RegisterInstance(new WorldInitData(MobaWorldBootstrapModule.InitOpCode, EnterMobaGameCodec.SerializeReq(enterReq)));

            var options = new WorldCreateOptions(id, worldType)
            {
                ServiceBuilder = builder
            };
            options.Modules.Add(new MobaWorldBootstrapModule());

            return manager.Create(options);
        }

        private void BootstrapLobby(IWorld world, MobaLobbyStateService lobby)
        {
            var p = new PlayerId(playerId);
            lobby.OnPlayerJoined(p);

            // Bootstrap without advancing simulation frames; keep both worlds aligned at frame 0.
            var frame0 = new FrameIndex(0);
            var ready = new PlayerInputCommand(frame0, p, (int)MobaOpCode.Ready, Array.Empty<byte>());
            world.Services.Get<IWorldInputSink>().Submit(frame0, new[] { ready });
        }

        private void StepOnce()
        {
            var next = new FrameIndex(_frame.Value + 1);
            var p = new PlayerId(playerId);

            var dx = 0f;
            var dz = 0f;
            if ((next.Value % 20) < 10) dz = 1f; else dz = -1f;

            var payload = MobaMoveCodec.Serialize(dx, dz);
            var move = new PlayerInputCommand(next, p, (int)MobaOpCode.Move, payload);

            var authInputs = new[] { move };
            _authInput.Submit(next, authInputs);
            _auth.Tick(fixedDelta);

            PlayerInputCommand[] predInputs = authInputs;
            if (forceDesync && next.Value == desyncAtFrame)
            {
                // NOTE: Dropping the input would keep the previous non-zero input inside MobaMoveService,
                // so both worlds may still move identically. Use an opposite input to guarantee divergence.
                var oppositePayload = MobaMoveCodec.Serialize(-dx, -dz);
                predInputs = new[] { new PlayerInputCommand(next, p, (int)MobaOpCode.Move, oppositePayload) };
                Debug.Log($"[ClientPredictionTestHarness] Force desync at frame {next.Value} (opposite input)");
            }

            _runner.TickPredicted(next, fixedDelta, predInputs, ComputePredictedHash);

            if ((next.Value % 10) == 0)
            {
                var authHash = ComputeAuthoritativeHash(next);
                var predHash = ComputePredictedHash(next);

                Debug.Log($"[ClientPredictionTestHarness] HashCheck frame={next.Value} auth={authHash.Value} pred={predHash.Value}");

                if (authHash.Value != predHash.Value)
                {
                    // Simulate low-frequency authoritative snapshot correction (2C): inject authoritative rollback snapshot for this frame.
                    var snap = _authRollback.Capture(next);
                    _rollback.StoreSnapshot(snap);
                    Debug.Log($"[ClientPredictionTestHarness] Inject authoritative rollback snapshot at frame={next.Value}");
                }
                _runner.OnAuthoritativeStateHash(next, authHash);
            }

            _frame = next;
        }

        private WorldStateHash ComputePredictedHash(FrameIndex frame)
        {
            return ComputeHash(_predLobby, _predRegistry);
        }

        private WorldStateHash ComputeAuthoritativeHash(FrameIndex frame)
        {
            return ComputeHash(_authLobby, _authRegistry);
        }

        private static WorldStateHash ComputeHash(MobaLobbyStateService lobby, MobaActorRegistry registry)
        {
            var entries = new List<(int actorId, float x, float y, float z)>(16);
            foreach (var kv in registry.Entries)
            {
                var actorId = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;
                var p = e.transform.Value.Position;
                entries.Add((actorId, p.X, p.Y, p.Z));
            }

            entries.Sort((a, b) => a.actorId.CompareTo(b.actorId));

            uint h = 2166136261u;
            AddByte(ref h, lobby != null && lobby.Started ? (byte)1 : (byte)0);
            AddInt(ref h, entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var it = entries[i];
                AddInt(ref h, it.actorId);
                AddFloat(ref h, it.x);
                AddFloat(ref h, it.y);
                AddFloat(ref h, it.z);
            }

            return new WorldStateHash(h);
        }

        private static void AddByte(ref uint h, byte v)
        {
            h ^= v;
            h *= 16777619u;
        }

        private static void AddInt(ref uint h, int v)
        {
            unchecked
            {
                AddUInt(ref h, (uint)v);
            }
        }

        private static void AddUInt(ref uint h, uint v)
        {
            AddByte(ref h, (byte)(v & 0xFF));
            AddByte(ref h, (byte)((v >> 8) & 0xFF));
            AddByte(ref h, (byte)((v >> 16) & 0xFF));
            AddByte(ref h, (byte)((v >> 24) & 0xFF));
        }

        private static void AddFloat(ref uint h, float v)
        {
            var bits = BitConverter.SingleToInt32Bits(v);
            AddInt(ref h, bits);
        }
    }
}
