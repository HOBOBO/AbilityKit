using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private IBattleHudInputSink _hudInput;
        private Camera _camera;

        private EC.IEntity _hudNode;

        private BattleHudConfig _config;
        private BattleHudBinder _binder;
        private BattleHudCanvasController _canvasController;
        private BattleHudInputController _inputController;
        private BattleHudSnapshotController _snapshotController;
        private BattleHudSkillFailurePresenter _skillFailurePresenter;
        private BattleHudEntityLifecycleBinding _entityLifecycle;
        private BattlePresentationSessionContext _presentation;
        private readonly BattlePresentationSessionResolver _presentationSessions = new BattlePresentationSessionResolver();
        private readonly BattleHudFeatureControllerFactory _controllers = new BattleHudFeatureControllerFactory();

        private BattleHudAimPreview _aimPreview;
        private readonly BattleHudSkillTemplateBindingState _skillTemplateBinding = new BattleHudSkillTemplateBindingState();

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Features.TryGet(out _ctx);
            _hudInput = _ctx;

            _camera = Camera.main;
            _config = BattleHudConfig.Default;
            _presentation = _presentationSessions.Resolve(ctx);

            if (_ctx != null && _ctx.EntityNode.IsValid)
            {
                _hudNode = _ctx.EntityNode.AddChild("BattleHud");
            }

            _canvasController = _controllers.CreateCanvas();
            _canvasController.Create("BattleHudCanvas");

            _binder = _controllers.CreateBinder(_config, _canvasController.Root, _camera, _ctx);
            _skillFailurePresenter = _controllers.CreateSkillFailures();
            _skillFailurePresenter.Bind(_ctx, _canvasController.Root);
            CreateInputController();
            ApplyLaunchSpecSkillTemplates();
            SubscribeEntityLifecycle();
            SubscribeSnapshots();
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _snapshotController?.Dispose();
            _snapshotController = null;

            _entityLifecycle?.Dispose();
            _entityLifecycle = null;

            _binder?.Clear();
            _binder = null;

            _skillFailurePresenter?.Dispose();
            _skillFailurePresenter = null;

            _inputController?.Dispose();
            _inputController = null;

            _aimPreview?.Clear();
            _aimPreview = null;
            _skillTemplateBinding.Reset();

            _canvasController?.Dispose();
            _canvasController = null;
            _hudNode = default;

            _presentationSessions.Release(ctx, _presentation);
            _ctx = null;
            _hudInput = null;
            _camera = null;
            _presentation = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_binder == null) return;

            EnsureSnapshotSubscription();
            EnsureLocalControlSkillTemplates();
            _skillFailurePresenter?.Tick(deltaTime);
            _binder.Tick(deltaTime);
            _aimPreview ??= _controllers.CreateAimPreview();
            _aimPreview.SetSkillSpecs(_inputController?.SkillSpecs);
            _aimPreview.Tick(_ctx, deltaTime);
        }

        private void CreateInputController()
        {
            var cameraTransform = _camera != null ? _camera.transform : null;
            _inputController = _controllers.CreateInput(
                _hudInput,
                _canvasController.Root,
                _canvasController.Canvas,
                cameraTransform,
                _presentation?.Resources);
            _inputController.Ensure();
        }

        public void RefreshLocalControlSkillTemplates()
        {
            ApplyLaunchSpecSkillTemplates();
        }

        private void EnsureLocalControlSkillTemplates()
        {
            var revision = _ctx != null ? _ctx.RuntimePlayerLoadoutRevision : 0;
            if (_skillTemplateBinding.RequiresBinding(ResolveLocalPlayerId(), revision))
            {
                ApplyLaunchSpecSkillTemplates();
            }
        }

        private void ApplyLaunchSpecSkillTemplates()
        {
            if (_ctx == null) return;
            var launchSpec = _ctx.Plan.LaunchSpec;
            if (launchSpec.Players == null || launchSpec.Players.Length == 0) return;

            var playerId = ResolveLocalPlayerId();
            var worldId = !string.IsNullOrEmpty(_ctx.Plan.World.WorldId)
                ? _ctx.Plan.World.WorldId
                : launchSpec.WorldId;

            var res = new EnterMobaGameRes(
                new WorldId(worldId),
                launchSpec.LocalPlayerId,
                _ctx.LocalActorId,
                launchSpec.RandomSeed,
                launchSpec.TickRate,
                launchSpec.InputDelayFrames,
                playersLoadout: _ctx.BuildEffectivePlayerLoadouts());

            ApplySkillButtonTemplates(res, playerId);
        }

        private void SubscribeEntityLifecycle()
        {
            _entityLifecycle ??= _controllers.CreateEntityLifecycle();
            _entityLifecycle.Bind(_ctx, _binder);
        }

        private void SubscribeSnapshots()
        {
            _snapshotController = _controllers.CreateSnapshots();
            EnsureSnapshotSubscription();
        }

        private void EnsureSnapshotSubscription()
        {
            if (_snapshotController == null || _snapshotController.IsBound) return;

            _snapshotController.Bind(_ctx, OnEnterGameSnapshot, OnDamageEventSnapshot, OnSkillStateSnapshot, OnPresentationCueSnapshot);
        }

        private void OnEnterGameSnapshot(EnterMobaGameRes res)
        {
            if (_ctx == null) return;
            if (res.LocalActorId > 0 && _ctx.LocalActorId <= 0)
            {
                _ctx.LocalActorId = res.LocalActorId;
            }

            var controlledPlayerId = _ctx.LocalControlPlayerId;
            if (!string.IsNullOrEmpty(controlledPlayerId) &&
                !string.Equals(controlledPlayerId, res.PlayerId.Value, System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyLaunchSpecSkillTemplates();
                return;
            }

            ApplySkillButtonTemplates(res, ResolveLocalPlayerId(res));
        }

        private void ApplySkillButtonTemplates(EnterMobaGameRes res, string playerId)
        {
            if (_inputController == null ||
                !_inputController.ApplySkillButtonTemplates(res, playerId))
            {
                return;
            }

            _skillTemplateBinding.MarkBound(
                playerId,
                _ctx != null ? _ctx.RuntimePlayerLoadoutRevision : 0);
            _aimPreview?.SetSkillSpecs(_inputController.SkillSpecs);
        }

        private void OnDamageEventSnapshot(MobaDamageEventSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _binder?.OnDamageEvents(entries);
        }

        private void OnSkillStateSnapshot(MobaSkillStateSnapshotEntry[] entries)
        {
            if (_ctx == null || entries == null || entries.Length == 0) return;
            var localActorId = ResolveLocalActorId(entries);
            if (localActorId > 0 && _ctx.LocalActorId <= 0)
            {
                _ctx.LocalActorId = localActorId;
            }

            _inputController?.ApplySkillStates(entries, localActorId);
        }

        private void OnPresentationCueSnapshot(MobaPresentationCueSnapshotEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _binder?.OnPresentationCues(entries);
        }

        private string ResolveLocalPlayerId(EnterMobaGameRes res = default)
        {
            if (_ctx == null) return string.Empty;
            var controlledPlayerId = _ctx.ResolveLocalControlPlayerId();
            if (!string.IsNullOrEmpty(controlledPlayerId)) return controlledPlayerId;
            if (!string.IsNullOrEmpty(res.PlayerId.Value)) return res.PlayerId.Value;
            return _ctx.Plan.LaunchSpec.LocalPlayerId.Value;
        }

        private int ResolveLocalActorId(MobaSkillStateSnapshotEntry[] entries)
        {
            if (_ctx == null) return 0;
            if (_ctx.LocalActorId > 0) return _ctx.LocalActorId;
            return _inputController != null
                ? _inputController.ResolveActorIdFromSkillStates(entries)
                : 0;
        }

    }

    internal sealed class BattleHudSkillTemplateBindingState
    {
        private string _playerId;
        private int _loadoutRevision = -1;

        public bool RequiresBinding(string playerId, int loadoutRevision)
        {
            return !string.IsNullOrEmpty(playerId) &&
                   (!string.Equals(
                        playerId,
                        _playerId,
                        System.StringComparison.OrdinalIgnoreCase) ||
                    loadoutRevision != _loadoutRevision);
        }

        public void MarkBound(string playerId, int loadoutRevision)
        {
            _playerId = playerId;
            _loadoutRevision = loadoutRevision;
        }

        public void Reset()
        {
            _playerId = null;
            _loadoutRevision = -1;
        }
    }

    internal sealed class BattleHudFeatureControllerFactory
    {
        public BattleHudCanvasController CreateCanvas()
        {
            return new BattleHudCanvasController();
        }

        public BattleHudBinder CreateBinder(
            BattleHudConfig config,
            RectTransform root,
            Camera camera,
            BattleContext ctx)
        {
            return new BattleHudBinder(config, root, camera, ctx);
        }

        public BattleHudInputController CreateInput(
            IBattleHudInputSink hudInput,
            RectTransform root,
            Canvas canvas,
            Transform cameraTransform,
            BattleViewResourceProvider resources)
        {
            return new BattleHudInputController(hudInput, root, canvas, cameraTransform, resources);
        }

        public BattleHudEntityLifecycleBinding CreateEntityLifecycle()
        {
            return new BattleHudEntityLifecycleBinding();
        }

        public BattleHudSnapshotController CreateSnapshots()
        {
            return new BattleHudSnapshotController();
        }

        public BattleHudAimPreview CreateAimPreview()
        {
            return new BattleHudAimPreview();
        }

        public BattleHudSkillFailurePresenter CreateSkillFailures()
        {
            return new BattleHudSkillFailurePresenter();
        }
    }
}
