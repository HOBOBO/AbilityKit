using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Requests;
using UnityEngine;

namespace AbilityKit.Game.Test
{
    public sealed class BattleFlowOnGUITest : MonoBehaviour
    {
        private Battle.BattleLogicSession _session;

        private bool _useRemote;
        private bool _autoTick = true;

        private string _worldId = "room_1";
        private string _worldType = "battle";
        private string _clientId = "battle_client";
        private string _playerId = "p1";

        private int _inputOpCode = 1;
        private string _inputPayload = "hello";

        private int _lastFrame;
        private Vector2 _scroll;
        private readonly List<string> _logs = new List<string>(256);

        private const float FixedDelta = 1f / 30f;
        private float _tickAccumulator;

        private void OnEnable()
        {
            Application.targetFrameRate = 30;
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
        }

        private void OnGUI()
        {
            const float w = 420;
            const float h = 620;

            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.window);
            GUILayout.Label("Battle Flow Test (OnGUI)");

            _useRemote = GUILayout.Toggle(_useRemote, "Remote Mode (In-Memory Transport)");
            _autoTick = GUILayout.Toggle(_autoTick, "Auto Tick (Update)");

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

            Log("Session stopped");
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

            var options = new WorldCreateOptions(new WorldId(_worldId), _worldType)
            {
                ServiceBuilder = builder
            };

            _session.CreateWorld(new CreateWorldRequest(options));
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

            var inputsCount = packet.Inputs?.Count ?? 0;
            var snapshotInfo = packet.Snapshot.HasValue
                ? $"snapshot(op={packet.Snapshot.Value.OpCode}, bytes={(packet.Snapshot.Value.Payload?.Length ?? 0)})"
                : "snapshot(null)";

            Log($"OnFrame: world={packet.WorldId.Value}, frame={packet.Frame.Value}, inputs={inputsCount}, {snapshotInfo}");
        }

        private void Log(string msg)
        {
            if (_logs.Count > 800) _logs.RemoveRange(0, 200);
            _logs.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
            _scroll.y = float.MaxValue;
        }
    }
}
