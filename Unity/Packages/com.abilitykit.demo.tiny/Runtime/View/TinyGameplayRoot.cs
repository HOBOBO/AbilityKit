#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.View.Modules;
using AbilityKit.Network.Room;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Demo.Tiny.View
{
    [DisallowMultipleComponent]
    public sealed class TinyGameplayRoot : MonoBehaviour
    {
        private enum NetworkOperation { None, Startup, Command, Poll, Recover, Snapshot, Exit }

        private CancellationTokenSource? _lifetime;
        private CancellationTokenSource? _activeRequest;
        private TinyBattleSession? _session;
        private ModuleHost<TinyViewModuleContext, IGameModule<TinyViewModuleContext>>? _viewModules;
        private TinyViewModuleContext _viewContext;
        private readonly TinyHudPresenter _hud = new TinyHudPresenter();
        private string _status = "Connecting";
        private string _error = string.Empty;
        private NetworkOperation _operation = NetworkOperation.Startup;
        private bool _destroyed;
        private float _nextPoll;
        private float _nextRecovery;
        private float _nextSnapshotRequest;
        private int _observedSnapshotRequests;
        private string _returnScene = DemoSceneRoutes.Starter;

        public TinyBattleSession? Session => _session;
        public bool IsReady => _session != null && _operation != NetworkOperation.Startup &&
            string.IsNullOrEmpty(_error);
        public string LastError => _error;

        private async void Start()
        {
            DemoMultiplayerLaunchRequest launch;
            if (!TinyProjectLaunch.TryConsume(out launch, out _returnScene) &&
                !DemoMultiplayerLaunchIntent.TryConsume(DemoMultiplayerGameplay.Tiny, out launch))
            {
                _error = "Tiny launch session is missing.";
                _operation = NetworkOperation.None;
                return;
            }
            if (string.IsNullOrEmpty(_returnScene)) _returnScene = DemoSceneRoutes.Starter;
            _lifetime = new CancellationTokenSource();
            var token = _lifetime.Token;
            try
            {
                _viewContext = new TinyViewModuleContext(transform, () => _session,
                    token, exception => { if (!_destroyed) _error = exception.Message; });
                _viewModules = new ModuleHost<TinyViewModuleContext, IGameModule<TinyViewModuleContext>>(
                    new List<IGameModule<TinyViewModuleContext>>
                    {
                        new TinyActorViewModule(),
                        new TinyInputModule()
                    }, message => { if (!_destroyed) _error = message; });
                if (!_viewModules.TrySortByDependencies())
                    throw new InvalidOperationException(_error);
                _viewModules.Attach(in _viewContext);
                var gateway = await TinyGatewayClient.ConnectAsync(launch.Host, launch.Port, token);
                if (_destroyed || token.IsCancellationRequested) { gateway.Dispose(); return; }
                _session = new TinyBattleSession(launch, gateway);
                await _session.RestoreAsync(token);
                if (!_destroyed && !token.IsCancellationRequested) _status = _session.Status;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!_destroyed && _operation == NetworkOperation.Startup) _error = exception.Message;
            }
            finally { if (!_destroyed && _operation == NetworkOperation.Startup) _operation = NetworkOperation.None; }
        }

        private void Update()
        {
            if (_destroyed || _operation == NetworkOperation.Exit) return;
            var session = _session;
            session?.Tick(Time.unscaledDeltaTime);
            if (_viewModules?.IsAttached == true)
                _viewModules.Tick(in _viewContext, Time.unscaledDeltaTime);
            if (session == null || _lifetime == null) return;
            _status = session.Status;
            if (_observedSnapshotRequests != session.SnapshotRequestCount)
            {
                _observedSnapshotRequests = session.SnapshotRequestCount;
                _nextSnapshotRequest = Time.unscaledTime + 2f;
            }
            if (session.ConnectionUnavailable && _activeRequest != null)
                CancelActiveRequest();
            if (_operation != NetworkOperation.None) return;
            if (session.NeedsConnectionRestore && Time.unscaledTime >= _nextRecovery)
            {
                RecoverConnectionAsync();
                return;
            }
            if (session.ConnectionUnavailable) return;
            if (session.NeedsFullSnapshot && Time.unscaledTime >= _nextSnapshotRequest)
            {
                RequestFullSnapshotAsync();
                return;
            }
            if (!string.IsNullOrEmpty(session.RoomId) &&
                Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + 0.5f;
                PollRoomAsync();
            }
        }

        private async void RequestFullSnapshotAsync()
        {
            if (_session == null || _lifetime == null || _operation != NetworkOperation.None) return;
            var session = _session;
            var request = BeginRequest(NetworkOperation.Snapshot);
            var token = request.Token;
            _nextSnapshotRequest = Time.unscaledTime + 2f;
            try { await session.RequestFullSnapshotAsync("Frame rollback recovery", token); }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (IsCurrentRequest(request, NetworkOperation.Snapshot)) _error = exception.Message;
            }
            finally { EndRequest(request, NetworkOperation.Snapshot); }
        }

        private async void RecoverConnectionAsync()
        {
            if (_session == null || _lifetime == null || _operation != NetworkOperation.None) return;
            var session = _session;
            var token = _lifetime.Token;
            _operation = NetworkOperation.Recover;
            try
            {
                await session.RecoverConnectionAsync(token);
                if (_destroyed || token.IsCancellationRequested || _operation != NetworkOperation.Recover) return;
                _error = string.Empty;
                _nextPoll = 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!_destroyed && _operation == NetworkOperation.Recover)
                { _error = exception.Message; _nextRecovery = Time.unscaledTime + 2f; }
            }
            finally { if (!_destroyed && _operation == NetworkOperation.Recover) _operation = NetworkOperation.None; }
        }

        private void OnGUI()
        {
            _hud.Draw(_session, _status, _error, _operation != NetworkOperation.None,
                _operation == NetworkOperation.Recover, _operation != NetworkOperation.Exit,
                CreateRoom, JoinRoom, SetReady, BeginLoading, ReturnToLobbyAsync);
        }

        private void CreateRoom(TinySyncMode mode)
        {
            if (_session != null) RunCommandAsync(token => _session.CreateRoomAsync(mode, token));
        }

        private void JoinRoom(string roomId)
        {
            if (_session != null) RunCommandAsync(token => _session.JoinRoomAsync(roomId, token));
        }

        private void SetReady()
        {
            if (_session != null) RunCommandAsync(_session.SetReadyAsync);
        }

        private void BeginLoading()
        {
            if (_session != null) RunCommandAsync(_session.BeginLoadingAsync);
        }

        private async void RunCommandAsync(Func<CancellationToken, Task> command)
        {
            if (_operation != NetworkOperation.None || _lifetime == null || _session == null ||
                _session.ConnectionUnavailable) return;
            var request = BeginRequest(NetworkOperation.Command);
            var token = request.Token;
            _error = string.Empty;
            try
            {
                await command(token);
                if (!IsCurrentRequest(request, NetworkOperation.Command) || token.IsCancellationRequested)
                    return;
                _status = _session?.Status ?? _status;
                _nextPoll = 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (IsCurrentRequest(request, NetworkOperation.Command)) _error = exception.Message;
            }
            finally { EndRequest(request, NetworkOperation.Command); }
        }

        private async void PollRoomAsync()
        {
            if (_session == null || _lifetime == null || _operation != NetworkOperation.None) return;
            var session = _session;
            var request = BeginRequest(NetworkOperation.Poll);
            var token = request.Token;
            try
            {
                await session.PollAsync(token);
                if (IsCurrentRequest(request, NetworkOperation.Poll) &&
                    !token.IsCancellationRequested) _status = session.Status;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (IsCurrentRequest(request, NetworkOperation.Poll)) _error = exception.Message;
            }
            finally { EndRequest(request, NetworkOperation.Poll); }
        }

        private CancellationTokenSource BeginRequest(NetworkOperation operation)
        {
            var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime!.Token);
            _activeRequest = request;
            _operation = operation;
            return request;
        }

        private bool IsCurrentRequest(CancellationTokenSource request, NetworkOperation operation) =>
            !_destroyed && ReferenceEquals(_activeRequest, request) && _operation == operation;

        private void EndRequest(CancellationTokenSource request, NetworkOperation operation)
        {
            if (ReferenceEquals(_activeRequest, request))
            {
                _activeRequest = null;
                if (!_destroyed && _operation == operation) _operation = NetworkOperation.None;
            }
            request.Dispose();
        }

        private void CancelActiveRequest()
        {
            var request = _activeRequest;
            if (request == null) return;
            _activeRequest = null;
            _operation = NetworkOperation.None;
            request.Cancel();
        }

        private async void ReturnToLobbyAsync()
        {
            if (_destroyed || _operation == NetworkOperation.Exit) return;
            var wasIdle = _operation == NetworkOperation.None;
            if (!wasIdle)
            {
                _operation = NetworkOperation.Exit;
                _activeRequest?.Cancel();
                _lifetime?.Cancel();
                SceneManager.LoadSceneAsync(_returnScene, LoadSceneMode.Single);
                return;
            }
            if (_lifetime == null)
            {
                SceneManager.LoadSceneAsync(_returnScene, LoadSceneMode.Single);
                return;
            }
            var token = _lifetime.Token;
            _operation = NetworkOperation.Exit;
            try
            {
                if (_session != null && !_session.ConnectionUnavailable)
                    await _session.LeaveLobbyAsync(token);
                if (_destroyed || token.IsCancellationRequested) return;
                SceneManager.LoadSceneAsync(_returnScene, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                if (!_destroyed) { _error = exception.Message; _operation = NetworkOperation.None; }
            }
        }

        private void OnDestroy()
        {
            _destroyed = true;
            _activeRequest?.Cancel();
            _lifetime?.Cancel();
            try
            {
                if (_viewModules?.IsAttached == true)
                    _viewModules.Detach(in _viewContext);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                _session?.Dispose();
                _lifetime?.Dispose();
            }
        }
    }
}
