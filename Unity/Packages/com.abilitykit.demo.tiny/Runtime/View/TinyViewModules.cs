#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.View.Modules;
using AbilityKit.Protocol.Room;
using UnityEngine;

namespace AbilityKit.Demo.Tiny.View
{
    public readonly struct TinyViewModuleContext
    {
        public TinyViewModuleContext(Transform root, Func<TinyBattleSession?> getSession,
            CancellationToken cancellationToken, Action<Exception> reportError)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            GetSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
            CancellationToken = cancellationToken;
            ReportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
        }

        public Transform Root { get; }
        public Func<TinyBattleSession?> GetSession { get; }
        public CancellationToken CancellationToken { get; }
        public Action<Exception> ReportError { get; }
    }

    public sealed class TinyInputModule : IGameModule<TinyViewModuleContext>,
        IGameModuleTick<TinyViewModuleContext>, IGameModuleId
    {
        private Task? _submission;
        private readonly TinyInputBuffer _buffer = new TinyInputBuffer();
        private float _nextInput;

        public string Id => "input";

        public void OnAttach(in TinyViewModuleContext ctx)
        {
            _submission = null;
            _buffer.Clear();
            _nextInput = 0;
        }

        public void Tick(in TinyViewModuleContext ctx, float deltaTime)
        {
            var session = ctx.GetSession();
            if (session == null || !session.CanSubmitInput)
            {
                _buffer.Clear();
                return;
            }

            var x = (sbyte)((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0));
            var y = (sbyte)((Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            var attack = Input.GetKeyDown(KeyCode.Space);
            _buffer.Capture(new TinyInput(x, y, attack));
            if ((_submission != null && !_submission.IsCompleted) ||
                Time.unscaledTime < _nextInput || !_buffer.TryTake(out var input))
                return;
            _nextInput = Time.unscaledTime +
                (session.SyncMode == TinySyncMode.State ? 0.1f : 1f / TinySyncSettings.TickRate);
            _submission = SubmitAsync(session, input, ctx);
        }

        public void OnDetach(in TinyViewModuleContext ctx)
        {
            _submission = null;
            _buffer.Clear();
            _nextInput = 0;
        }

        private static async Task SubmitAsync(TinyBattleSession session, TinyInput input,
            TinyViewModuleContext ctx)
        {
            try { await session.SubmitInputAsync(input, ctx.CancellationToken); }
            catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested) { }
            catch (Exception exception) { ctx.ReportError(exception); }
        }
    }

    public sealed class TinyActorViewModule : IGameModule<TinyViewModuleContext>,
        IGameModuleTick<TinyViewModuleContext>, IGameModuleId
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private readonly Dictionary<int, GameObject> _actors = new Dictionary<int, GameObject>();
        private readonly List<GameObject> _sceneObjects = new List<GameObject>();
        private readonly MaterialPropertyBlock _colorBlock = new MaterialPropertyBlock();
        private TinyBattleSession? _boundSession;
        private string _boundBattleId = string.Empty;
        private ulong _boundWorldId;

        public string Id => "actors";

        public void OnAttach(in TinyViewModuleContext ctx)
        {
            try
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                _sceneObjects.Add(floor);
                floor.name = "Tiny Arena";
                floor.transform.SetParent(ctx.Root, false);
                floor.transform.localScale = new Vector3(2, 1, 2);
                SetColor(floor, new Color(0.18f, 0.22f, 0.24f));

                var cameraObject = new GameObject("Tiny Camera");
                _sceneObjects.Add(cameraObject);
                cameraObject.transform.SetParent(ctx.Root, false);
                cameraObject.transform.position = new Vector3(0, 13, -8);
                cameraObject.transform.rotation = Quaternion.Euler(60, 0, 0);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 9;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.1f, 0.13f, 0.15f);

                var lightObject = new GameObject("Tiny Light");
                _sceneObjects.Add(lightObject);
                lightObject.transform.SetParent(ctx.Root, false);
                lightObject.transform.rotation = Quaternion.Euler(55, -35, 0);
                lightObject.AddComponent<Light>().type = LightType.Directional;
            }
            catch
            {
                OnDetach(in ctx);
                throw;
            }
        }

        public void Tick(in TinyViewModuleContext ctx, float deltaTime)
        {
            var session = ctx.GetSession();
            var battleId = session?.BattleId ?? string.Empty;
            var worldId = session?.WorldId ?? 0;
            if (!ReferenceEquals(session, _boundSession) || battleId != _boundBattleId ||
                worldId != _boundWorldId)
            {
                ClearActors();
                _boundSession = session;
                _boundBattleId = battleId;
                _boundWorldId = worldId;
            }
            if (session != null && !string.IsNullOrEmpty(battleId) && worldId != 0 &&
                session.TryGetNewSnapshot(out var snapshot))
                ApplySnapshot(in snapshot, session.PlayerId, ctx.Root);
        }

        public void OnDetach(in TinyViewModuleContext ctx)
        {
            ClearActors();
            _boundSession = null;
            _boundBattleId = string.Empty;
            _boundWorldId = 0;
            foreach (var sceneObject in _sceneObjects) DestroyView(sceneObject);
            _sceneObjects.Clear();
        }

        private void ClearActors()
        {
            foreach (var actor in _actors.Values) DestroyView(actor);
            _actors.Clear();
        }

        private void ApplySnapshot(in WireStateSyncSnapshotPush snapshot, uint localPlayerId, Transform root)
        {
            if (snapshot.Actors == null) return;
            var present = new HashSet<int>();
            foreach (var actor in snapshot.Actors)
            {
                present.Add(actor.ActorId);
                if (!_actors.TryGetValue(actor.ActorId, out var view))
                {
                    view = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    view.name = $"Tiny Actor {actor.ActorId}";
                    view.transform.SetParent(root, false);
                    _actors.Add(actor.ActorId, view);
                }
                SetColor(view, actor.ActorId == localPlayerId
                    ? new Color(0.12f, 0.68f, 0.56f)
                    : new Color(0.88f, 0.32f, 0.28f));
                var height = Mathf.Max(0.1f, actor.Hp / 100f);
                view.transform.position = new Vector3(actor.X, height * 0.5f, actor.Z);
                view.transform.localScale = new Vector3(0.8f, height, 0.8f);
            }
            if (!snapshot.IsFullSnapshot) return;
            foreach (var actorId in new List<int>(_actors.Keys))
            {
                if (present.Contains(actorId)) continue;
                DestroyView(_actors[actorId]);
                _actors.Remove(actorId);
            }
        }

        private void SetColor(GameObject view, Color color)
        {
            _colorBlock.Clear();
            _colorBlock.SetColor(ColorProperty, color);
            view.GetComponent<Renderer>().SetPropertyBlock(_colorBlock);
        }

        private static void DestroyView(GameObject view)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(view);
            else UnityEngine.Object.DestroyImmediate(view);
        }
    }
}
