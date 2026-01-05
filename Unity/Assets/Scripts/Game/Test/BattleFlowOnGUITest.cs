using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Moba.Config;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AbilityKit.Game.Test
{
    public sealed class BattleFlowOnGUITest : MonoBehaviour
    {
        private Battle.BattleLogicSession _session;

        private readonly Dictionary<int, GameObject> _actorViews = new Dictionary<int, GameObject>();

        private bool _useRemote;
        private bool _autoTick = true;
        private bool _wasdMove = true;
        private bool _jklSkill = true;
        private bool _logToConsole = true;
        private bool _logFrames;

        private string _worldId = "room_1";
        private string _worldType = "battle";
        private string _clientId = "battle_client";
        private string _playerId = "p1";

        private int _inputOpCode = 1;
        private string _inputPayload = "hello";

        private int _lastFrame;
        private Vector2 _scroll;
        private readonly List<string> _logs = new List<string>(256);

        private float _moveLogCooldown;
        private float _snapshotLogCooldown;
        private float _frameLogCooldown;
        private float _inputDiagCooldown;

        private float _lastMoveDx;
        private float _lastMoveDz;

        private const float FixedDelta = 1f / 30f;
        private float _tickAccumulator;

        private void OnEnable()
        {
            Application.targetFrameRate = 60;
            Log("[BattleFlowOnGUITest] Enabled");
        }

        private void OnDisable()
        {
            StopSession();
        }

        private void Update()
        {
            if (_autoTick && _session != null)
            {
                _tickAccumulator += Time.deltaTime;
                while (_tickAccumulator >= FixedDelta)
                {
                    _session.Tick(FixedDelta);
                    _tickAccumulator -= FixedDelta;
                }
            }

            if (_wasdMove && _session != null)
            {
                GetMoveInput(out var dx, out var dz);

                var wasMoving = Math.Abs(_lastMoveDx) > 0.0001f || Math.Abs(_lastMoveDz) > 0.0001f;
                var isMoving = Math.Abs(dx) > 0.0001f || Math.Abs(dz) > 0.0001f;

                if (isMoving || (wasMoving && !isMoving))
                {
                    var payload = MobaMoveCodec.Serialize(dx, dz);
                    var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_playerId), (int)MobaOpCode.Move, payload);
                    _session.SubmitInput(new SubmitInputRequest(new WorldId(_worldId), cmd));

                    _lastMoveDx = dx;
                    _lastMoveDz = dz;

                    _moveLogCooldown -= Time.deltaTime;
                    if (_moveLogCooldown <= 0f)
                    {
                        _moveLogCooldown = 0.2f;
                        Log($"SendMove: frame={cmd.Frame.Value}, dx={dx}, dz={dz}");
                    }
                }
                else
                {
                    _lastMoveDx = dx;
                    _lastMoveDz = dz;

                    _inputDiagCooldown -= Time.deltaTime;
                    if (_inputDiagCooldown <= 0f)
                    {
                        _inputDiagCooldown = 1f;
                        Log($"WASD idle: focused={Application.isFocused}, dx={dx}, dz={dz}. If dx/dz always 0, check GameView focus or Active Input Handling (New Input System). ");
                    }
                }
            }

            if (_jklSkill && _session != null)
            {
                if (GetSkillKeyDown(out var slot))
                {
                    var op = slot == 1 ? (int)MobaOpCode.Skill1 : slot == 2 ? (int)MobaOpCode.Skill2 : (int)MobaOpCode.Skill3;
                    var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_playerId), op, Array.Empty<byte>());
                    _session.SubmitInput(new SubmitInputRequest(new WorldId(_worldId), cmd));
                    Log($"SendSkill: frame={cmd.Frame.Value}, slot={slot}, op={op}");
                }
            }
        }

        private static bool GetSkillKeyDown(out int slot)
        {
            slot = 0;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame) { slot = 1; return true; }
                if (kb.kKey.wasPressedThisFrame) { slot = 2; return true; }
                if (kb.lKey.wasPressedThisFrame) { slot = 3; return true; }
                return false;
            }
#endif

            if (Input.GetKeyDown(KeyCode.J)) { slot = 1; return true; }
            if (Input.GetKeyDown(KeyCode.K)) { slot = 2; return true; }
            if (Input.GetKeyDown(KeyCode.L)) { slot = 3; return true; }
            return false;
        }

        private static void GetMoveInput(out float dx, out float dz)
        {
            dx = 0f;
            dz = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) dx -= 1f;
                if (kb.dKey.isPressed) dx += 1f;
                if (kb.wKey.isPressed) dz += 1f;
                if (kb.sKey.isPressed) dz -= 1f;
                return;
            }
#endif

            // Legacy input
            if (Input.GetKey(KeyCode.A)) dx -= 1f;
            if (Input.GetKey(KeyCode.D)) dx += 1f;
            if (Input.GetKey(KeyCode.W)) dz += 1f;
            if (Input.GetKey(KeyCode.S)) dz -= 1f;
        }

        private void OnGUI()
        {
            const float w = 420;
            const float h = 620;

            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.window);
            GUILayout.Label("Battle Flow Test (OnGUI)");

            _useRemote = GUILayout.Toggle(_useRemote, "Remote Mode (In-Memory Transport)");
            _autoTick = GUILayout.Toggle(_autoTick, "Auto Tick (Update)");
            _wasdMove = GUILayout.Toggle(_wasdMove, "WASD Move (Send Move Input)");
            _jklSkill = GUILayout.Toggle(_jklSkill, "JKL Skill (Send Skill1/2/3)");
            _logToConsole = GUILayout.Toggle(_logToConsole, "Log To Console (Debug.Log)");
            _logFrames = GUILayout.Toggle(_logFrames, "Log Every Frame (Noisy)");

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label("WorldId", GUILayout.Width(70));
            _worldId = GUILayout.TextField(_worldId);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("WorldType", GUILayout.Width(70));
            _worldType = GUILayout.TextField(_worldType);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("ClientId", GUILayout.Width(70));
            _clientId = GUILayout.TextField(_clientId);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("PlayerId", GUILayout.Width(70));
            _playerId = GUILayout.TextField(_playerId);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUI.enabled = _session == null;
            if (GUILayout.Button("Start Session", GUILayout.Height(28)))
            {
                StartSession();
            }
            if (GUILayout.Button("One-Click Start", GUILayout.Height(28)))
            {
                OneClickStart();
            }
            GUI.enabled = _session != null;
            if (GUILayout.Button("Stop Session", GUILayout.Height(28)))
            {
                StopSession();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _session != null;
            if (GUILayout.Button("Connect")) _session?.Connect();
            if (GUILayout.Button("CreateWorld")) CreateWorld();
            if (GUILayout.Button("Join")) _session?.Join(new JoinWorldRequest(new WorldId(_worldId), new PlayerId(_playerId)));
            if (GUILayout.Button("Leave")) _session?.Leave(new LeaveWorldRequest(new WorldId(_worldId), new PlayerId(_playerId)));
            if (GUILayout.Button("Disconnect")) _session?.Disconnect();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            GUILayout.Label("Submit Input");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Op", GUILayout.Width(30));
            var opStr = GUILayout.TextField(_inputOpCode.ToString(), GUILayout.Width(60));
            if (int.TryParse(opStr, out var op)) _inputOpCode = op;
            GUILayout.Label("Payload", GUILayout.Width(55));
            _inputPayload = GUILayout.TextField(_inputPayload);
            GUILayout.EndHorizontal();

            GUI.enabled = _session != null;
            if (GUILayout.Button("SubmitInput"))
            {
                SubmitInput();
            }
            GUI.enabled = true;

            GUILayout.Space(6);

            GUILayout.Label($"LastFrame: {_lastFrame}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Log")) _logs.Clear();
            if (GUILayout.Button("Tick Once") && _session != null) _session.Tick(FixedDelta);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            for (var i = 0; i < _logs.Count; i++)
            {
                GUILayout.Label(_logs[i]);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void StartSession()
        {
            StopSession();

            var opts = new Battle.BattleLogicSessionOptions
            {
                Mode = _useRemote ? Battle.BattleLogicMode.Remote : Battle.BattleLogicMode.Local,
                WorldId = new WorldId(_worldId),
                WorldType = _worldType,
                ClientId = _clientId,
                PlayerId = _playerId,

                ScanAssemblies = new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(BattleFlowOnGUITest).Assembly
                },
                NamespacePrefixes = new[] { "AbilityKit" },

                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };

            _session = new Battle.BattleLogicSession(opts);
            _session.FrameReceived += OnFrame;

            _lastFrame = 0;
            _tickAccumulator = 0f;
            Log($"Session started. Mode={(opts.Mode)}");
        }

        private void OneClickStart()
        {
            StartSession();
            if (_session == null) return;

            _session.Connect();
            CreateWorld();
            _session.Join(new JoinWorldRequest(new WorldId(_worldId), new PlayerId(_playerId)));
            _session.SubmitInput(new SubmitInputRequest(new WorldId(_worldId), new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_playerId), (int)MobaOpCode.Ready, Array.Empty<byte>())));
            Log("One-Click Start done: Connect + CreateWorld + Join");
        }

        private void StopSession()
        {
            if (_session == null) return;

            try
            {
                _session.FrameReceived -= OnFrame;
                _session.Dispose();
            }
            catch (Exception e)
            {
                Log($"StopSession exception: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                _session = null;
            }

            DestroyFirstActorView();

            Log("Session stopped");
        }

        private void DestroyFirstActorView()
        {
            foreach (var kv in _actorViews)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }

            _actorViews.Clear();
        }

        private void CreateWorld()
        {
            if (_session == null) return;

            var builder = AbilityKit.Ability.World.Services.WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.Client,
                new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(BattleFlowOnGUITest).Assembly
                },
                new[] { "AbilityKit" }
            );

            builder.AddModule(new MobaConfigWorldModule());

            var options = new WorldCreateOptions(new WorldId(_worldId), _worldType)
            {
                ServiceBuilder = builder
            };

            var req = new EnterMobaGameReq(
                playerId: new PlayerId(_playerId),
                matchId: _worldId,
                mapId: 1,
                teamId: 1,
                heroId: 10001,
                randomSeed: 12345,
                tickRate: 30,
                inputDelayFrames: 2,
                opCode: 0,
                payload: null
            );

            var initPayload = EnterMobaGameCodec.SerializeReq(req);
            _session.CreateWorld(new CreateWorldRequest(options, MobaWorldBootstrapModule.InitOpCode, initPayload));
            Log($"CreateWorld: {options.Id.Value}, type={options.WorldType}");
        }

        private void SubmitInput()
        {
            if (_session == null) return;

            var payload = string.IsNullOrEmpty(_inputPayload) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(_inputPayload);
            var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_playerId), _inputOpCode, payload);
            _session.SubmitInput(new SubmitInputRequest(new WorldId(_worldId), cmd));
            Log($"SubmitInput: frame={cmd.Frame.Value}, player={_playerId}, op={_inputOpCode}, bytes={payload.Length}");
        }

        private void OnFrame(FramePacket packet)
        {
            _lastFrame = packet.Frame.Value;

            if (packet.Snapshot.HasValue)
            {
                if (packet.Snapshot.Value.OpCode == (int)MobaOpCode.EnterGameSnapshot)
                {
                    ApplyEnterGameResSnapshot(packet.Snapshot.Value.Payload);
                }
                else if (packet.Snapshot.Value.OpCode == (int)MobaOpCode.LobbySnapshot)
                {
                    ApplyLobbySnapshot(packet.Snapshot.Value.Payload);
                }
                else if (packet.Snapshot.Value.OpCode == (int)MobaOpCode.ActorTransformSnapshot)
                {
                    ApplyActorTransformSnapshot(packet.Snapshot.Value.Payload);
                }
            }

            var inputsCount = packet.Inputs?.Count ?? 0;
            var snapshotInfo = packet.Snapshot.HasValue
                ? $"snapshot(op={packet.Snapshot.Value.OpCode}, bytes={(packet.Snapshot.Value.Payload?.Length ?? 0)})"
                : "snapshot(null)";

            if (_logFrames)
            {
                Log($"OnFrame: world={packet.WorldId.Value}, frame={packet.Frame.Value}, inputs={inputsCount}, {snapshotInfo}");
            }
            else
            {
                _frameLogCooldown -= FixedDelta;
                if (_frameLogCooldown <= 0f)
                {
                    _frameLogCooldown = 1f;
                    Log($"OnFrame: world={packet.WorldId.Value}, frame={packet.Frame.Value}, inputs={inputsCount}, {snapshotInfo}");
                }
            }
        }

        private void ApplyLobbySnapshot(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;
            var snap = MobaLobbyCodec.DeserializeSnapshot(payload);
            var count = snap.Players == null ? 0 : snap.Players.Length;
            var readyCount = 0;
            if (snap.Players != null)
            {
                for (int i = 0; i < snap.Players.Length; i++)
                {
                    if (snap.Players[i].Ready) readyCount++;
                }
            }
            Log($"LobbySnapshot: version={snap.Version}, started={snap.Started}, joined={count}, ready={readyCount}");
        }

        private void ApplyEnterGameResSnapshot(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var res = EnterMobaGameCodec.DeserializeRes(payload);
            if (res.Payload == null || res.Payload.Length < 12) return;

            var x = BitConverter.ToSingle(res.Payload, 0);
            var y = BitConverter.ToSingle(res.Payload, 4);
            var z = BitConverter.ToSingle(res.Payload, 8);

            if (!_actorViews.TryGetValue(res.LocalActorId, out var go) || go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Actor_{res.LocalActorId}";
                go.transform.localScale = new Vector3(1f, 2f, 1f);
                _actorViews[res.LocalActorId] = go;
            }

            go.transform.position = new Vector3(x, y, z);
        }

        private void ApplyActorTransformSnapshot(byte[] payload)
        {
            var entries = MobaActorTransformSnapshotCodec.Deserialize(payload);
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (!_actorViews.TryGetValue(e.actorId, out var go) || go == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Actor_{e.actorId}";
                    go.transform.localScale = new Vector3(1f, 2f, 1f);
                    _actorViews[e.actorId] = go;
                }

                go.transform.position = new Vector3(e.x, e.y, e.z);
            }

            _snapshotLogCooldown -= Time.deltaTime;
            if (_snapshotLogCooldown <= 0f)
            {
                _snapshotLogCooldown = 0.5f;
                if (entries.Length > 0)
                {
                    Log($"RecvTransformSnapshot: count={entries.Length}, first=({entries[0].actorId}:{entries[0].x:F2},{entries[0].y:F2},{entries[0].z:F2})");
                }
                else
                {
                    Log("RecvTransformSnapshot: count=0");
                }
            }
        }

        private void Log(string msg)
        {
            if (_logs.Count > 800) _logs.RemoveRange(0, 200);
            _logs.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
            _scroll.y = float.MaxValue;

            if (_logToConsole)
            {
                UnityEngine.Debug.Log(msg);
            }
        }
    }
}
