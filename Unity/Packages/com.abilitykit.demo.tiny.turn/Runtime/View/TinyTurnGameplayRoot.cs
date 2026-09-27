#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Network.Room;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Tiny.Turn.View
{
    [DisallowMultipleComponent]
    public sealed class TinyTurnGameplayRoot : MonoBehaviour
    {
        private TinyTurnSession? _session;
        private CancellationTokenSource? _lifetime;
        private Task? _active;
        private string _returnScene = string.Empty;
        private string _roomToJoin = string.Empty;
        private string _error = string.Empty;
        private float _nextPoll;
        private float _nextRecovery;
        private float _nextSnapshotRequest;
        private int _observedSnapshotRequests;
        private bool _destroyed;
        private bool _initialized;

        public TinyTurnSession? Session => _session;
        public bool IsReady => _initialized && _session != null && _error.Length == 0;
        public string LastError => _error;

        private async void Start()
        {
            if (!TinyTurnProjectLaunch.TryConsume(out var launch, out _returnScene))
            {
                _error = "Tiny Turn launch session is missing.";
                return;
            }
            _lifetime = new CancellationTokenSource();
            try
            {
                _session = await TinyTurnSession.ConnectAsync(launch, _lifetime.Token);
                if (_destroyed) return;
                await _session.RestoreAsync(_lifetime.Token);
                if (!_destroyed) _initialized = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { if (!_destroyed) _error = exception.Message; }
        }

        private void Update()
        {
            if (_destroyed || _session == null || _lifetime == null) return;
            try { _session.Tick(Time.unscaledDeltaTime); }
            catch (Exception exception) { _error = exception.Message; }
            if (_observedSnapshotRequests != _session.SnapshotRequestCount)
            {
                _observedSnapshotRequests = _session.SnapshotRequestCount;
                _nextSnapshotRequest = Time.unscaledTime + 2f;
            }
            if (_active != null && !_active.IsCompleted) return;
            if (_session.NeedsConnectionRestore && Time.unscaledTime >= _nextRecovery)
            {
                _nextRecovery = Time.unscaledTime + 2f;
                Run(_session.RecoverConnectionAsync);
            }
            else if (_session.NeedsFullSnapshot && Time.unscaledTime >= _nextSnapshotRequest)
            {
                _nextSnapshotRequest = Time.unscaledTime + 2f;
                Run(token => _session.RequestFullSnapshotAsync(
                    "Tiny Turn authoritative refresh", token));
            }
            else if (!_session.ConnectionUnavailable && _session.RoomId.Length != 0 &&
                Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + 0.5f;
                Run(_session.PollAsync);
            }
        }

        private void OnGUI()
        {
            var session = _session;
            var width = Mathf.Min(320, Screen.width - 24);
            GUILayout.BeginArea(new Rect(12, 12, width, 285), GUI.skin.window);
            GUILayout.Label("Tiny Turn");
            GUILayout.Label(session == null ? "Connecting" :
                session.ConnectionUnavailable ? "Connection lost - restoring" : session.Status);
            if (_error.Length != 0) GUILayout.Label(_error);
            if (session != null)
            {
                if (session.RoomId.Length != 0) GUILayout.TextField(session.RoomId);
                if (session.State.HasValue)
                {
                    var state = session.State.Value;
                    GUILayout.Label("Turn " + state.Turn + " | " +
                        (state.WinnerId != 0 ? "Finished" :
                            session.ActionPending ? "Waiting for confirmation" :
                            session.CanAct ? "Your turn" :
                            "Waiting for Player " + state.CurrentPlayerId));
                    GUILayout.Label("HP " + state.PlayerOneHp + " : " + state.PlayerTwoHp);
                    if (state.WinnerId != 0) GUILayout.Label("Winner: Player " + state.WinnerId);
                }
                else if (session.Room?.Phase == RoomGatewaySessionPhase.InBattle)
                    GUILayout.Label("Waiting for authoritative state");
                GUI.enabled = (_active == null || _active.IsCompleted) &&
                    !session.ConnectionUnavailable;
                if (session.RoomId.Length == 0)
                {
                    if (GUILayout.Button("Create room")) Run(session.CreateRoomAsync);
                    _roomToJoin = GUILayout.TextField(_roomToJoin);
                    if (GUILayout.Button("Join room"))
                        Run(token => session.JoinRoomAsync(_roomToJoin, token));
                }
                else if (session.Room?.Phase == RoomGatewaySessionPhase.Lobby)
                {
                    if (GUILayout.Button("Ready")) Run(session.SetReadyAsync);
                    GUI.enabled = GUI.enabled && session.CanStart;
                    if (GUILayout.Button("Start")) Run(session.BeginLoadingAsync);
                }
                else if (session.CanAct && GUILayout.Button("Attack"))
                    Run(session.SubmitActionAsync);
                else if (session.ActionPending &&
                    GUILayout.Button("Retry pending action"))
                    Run(session.RetryPendingActionAsync);
            }
            GUI.enabled = true;
            if (GUILayout.Button("Back")) ReturnToLobby();
            GUILayout.EndArea();
        }

        private void Run(Func<CancellationToken, Task> action)
        {
            if (_lifetime == null || (_active != null && !_active.IsCompleted)) return;
            _error = string.Empty;
            _active = ExecuteAsync(action, _lifetime.Token);
        }

        private async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken token)
        {
            try { await action(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) { if (!_destroyed) _error = exception.Message; }
            finally { _active = null; }
        }

        private async void ReturnToLobby()
        {
            if (_returnScene.Length == 0) return;
            try
            {
                if (_session != null && _lifetime != null && !_session.ConnectionUnavailable)
                    await _session.LeaveLobbyAsync(_lifetime.Token);
                if (!_destroyed) SceneManager.LoadScene(_returnScene, LoadSceneMode.Single);
            }
            catch (Exception exception) { if (!_destroyed) _error = exception.Message; }
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _lifetime?.Cancel();
            _session?.Dispose();
            _lifetime?.Dispose();
        }
    }
}
