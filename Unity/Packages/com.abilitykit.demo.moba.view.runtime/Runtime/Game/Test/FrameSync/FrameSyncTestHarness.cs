using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Test.FrameSync
{
    public sealed class FrameSyncTestHarness : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField] private bool autoStart;
        [SerializeField] private bool autoTick;

        [Header("Session")]
        [SerializeField] private bool useRemote;
        [SerializeField] private string worldId = "room_1";
        [SerializeField] private string worldType = "battle";
        [SerializeField] private string clientId = "battle_client";
        [SerializeField] private string playerId = "p1";

        [Header("Tick")]
        [SerializeField] private float fixedDelta = 1f / 30f;

        [Header("Buffers")]
        [SerializeField] private int maxFrames = 200;
        [SerializeField] private int maxInputs = 400;
        [SerializeField] private int maxLogs = 800;

        private BattleLogicSession _session;
        private float _accumulator;
        private int _lastFrame;
        private bool _paused;

        private readonly List<FramePacket> _frames = new List<FramePacket>(256);
        private readonly List<PlayerInputCommand> _submittedInputs = new List<PlayerInputCommand>(256);
        private readonly List<string> _logs = new List<string>(256);

        public bool HasSession => _session != null;
        public bool Paused => _paused;
        public int LastFrame => _lastFrame;
        public IReadOnlyList<FramePacket> Frames => _frames;
        public IReadOnlyList<PlayerInputCommand> SubmittedInputs => _submittedInputs;
        public IReadOnlyList<string> Logs => _logs;

        public bool AutoTick
        {
            get => autoTick;
            set => autoTick = value;
        }

        public float FixedDelta
        {
            get => fixedDelta;
            set => fixedDelta = Mathf.Max(0.0001f, value);
        }

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
            if (!autoTick || _paused || _session == null) return;

            _accumulator += Time.deltaTime;
            while (_accumulator >= fixedDelta)
            {
                _session.Tick(fixedDelta);
                _accumulator -= fixedDelta;
            }
        }

        public void StartSession()
        {
            StopSession();

            var opts = new BattleLogicSessionOptions
            {
                Mode = useRemote ? BattleLogicMode.Remote : BattleLogicMode.Local,
                WorldId = new WorldId(worldId),
                WorldType = worldType,
                ClientId = clientId,
                PlayerId = playerId,

                ScanAssemblies = new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(FrameSyncTestHarness).Assembly
                },
                NamespacePrefixes = new[] { "AbilityKit" },

                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };

            _session = BattleLogicSessionHost.Start(opts);
            _session.FrameReceived += OnFrame;

            _paused = false;
            _accumulator = 0f;
            _lastFrame = 0;
            ClearBuffers();

            Log($"Session started. mode={opts.Mode}, worldId={worldId}, playerId={playerId}");
        }

        public void StopSession()
        {
            if (_session == null) return;

            try
            {
                _session.FrameReceived -= OnFrame;
                BattleLogicSessionHost.Stop();
            }
            catch (Exception e)
            {
                Log($"StopSession exception: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                _session = null;
            }

            Log("Session stopped");
        }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        public void TogglePause() => _paused = !_paused;

        public void TickOnce()
        {
            if (_session == null) return;
            _session.Tick(fixedDelta);
        }

        public void TickFrames(int frames)
        {
            if (_session == null) return;
            if (frames <= 0) return;

            for (int i = 0; i < frames; i++)
            {
                _session.Tick(fixedDelta);
            }
        }

        public void Connect() => _session?.Connect();

        public void Disconnect() => _session?.Disconnect();

        public void Join()
        {
            if (_session == null) return;
            _session.Join(new JoinWorldRequest(new WorldId(worldId), new PlayerId(playerId)));
        }

        public void Leave()
        {
            if (_session == null) return;
            _session.Leave(new LeaveWorldRequest(new WorldId(worldId), new PlayerId(playerId)));
        }

        public void CreateWorld(int initOpCode, byte[] initPayload)
        {
            if (_session == null) return;

            var builder = AbilityKit.Ability.World.Services.WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.Client,
                new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(FrameSyncTestHarness).Assembly
                },
                new[] { "AbilityKit" }
            );

            var options = new WorldCreateOptions(new WorldId(worldId), worldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            _session.CreateWorld(new CreateWorldRequest(options, initOpCode, initPayload));
        }

        public void SubmitInput(int opCode, byte[] payload)
        {
            if (_session == null) return;

            var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(playerId), opCode, payload);
            _session.SubmitInput(new SubmitInputRequest(new WorldId(worldId), cmd));

            _submittedInputs.Add(cmd);
            TrimList(_submittedInputs, maxInputs);

            Log($"SubmitInput frame={cmd.Frame.Value}, op={cmd.OpCode}, bytes={(cmd.Payload?.Length ?? 0)}");
        }

        public void SubmitInputString(int opCode, string payload)
        {
            var bytes = string.IsNullOrEmpty(payload) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(payload);
            SubmitInput(opCode, bytes);
        }

        public void ClearBuffers()
        {
            _frames.Clear();
            _submittedInputs.Clear();
            _logs.Clear();
        }

        public IReadOnlyList<PlayerInputCommand> GetPendingSubmittedInputs(int max = 200)
        {
            var pending = new List<PlayerInputCommand>(Math.Min(max, _submittedInputs.Count));

            for (int i = _submittedInputs.Count - 1; i >= 0 && pending.Count < max; i--)
            {
                var cmd = _submittedInputs[i];
                if (cmd.Frame.Value > _lastFrame)
                {
                    pending.Add(cmd);
                }
            }

            pending.Reverse();
            return pending;
        }

        private void OnFrame(FramePacket packet)
        {
            _lastFrame = packet.Frame.Value;

            _frames.Add(packet);
            TrimList(_frames, maxFrames);

            Log($"OnFrame world={packet.WorldId.Value}, frame={packet.Frame.Value}, inputs={(packet.Inputs?.Count ?? 0)}, snapshot={(packet.Snapshot.HasValue ? packet.Snapshot.Value.OpCode.ToString() : "null")}");

            if (packet.Snapshot.HasValue && packet.Snapshot.Value.OpCode == (int)MobaOpCode.StateHashSnapshot)
            {
                try
                {
                    var p = MobaStateHashSnapshotCodec.Deserialize(packet.Snapshot.Value.Payload);
                    Log($"StateHashSnapshot: v={p.Version}, frame={p.Frame}, hash={p.Hash}");
                }
                catch (Exception e)
                {
                    Log($"StateHashSnapshot decode failed: {e.GetType().Name}: {e.Message}");
                }
            }
        }

        private void Log(string msg)
        {
            if (_logs.Count > maxLogs) _logs.RemoveRange(0, Math.Min(200, _logs.Count));
            _logs.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
        }

        private static void TrimList<T>(List<T> list, int max)
        {
            if (max <= 0) return;
            if (list.Count <= max) return;
            list.RemoveRange(0, list.Count - max);
        }
    }
}
