using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.World.ECS;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudFeature : IGamePhaseFeature
    {
        private readonly GameplayPresentationModuleRegistry _gameplayModules;
        private IBattleHudPresentationPort _ctx;
        private IBattleHudInputSink _hudInput;
        private Camera _camera;

        private EC.IEntity _hudNode;

        private BattleHudConfig _config;
        private BattleHudBinder _binder;
        private BattleHudCanvasController _canvasController;
        private BattleHudInputController _inputController;
        private BattleHudSnapshotController _snapshotController;
        private BattleHudSkillFailurePresenter _skillFailurePresenter;
        private IGameplayHudModule _gameplayHud;
        private string _gameplayHudModuleId;
        private BattleGameplayPresentationState _gameplayHudState;
        private BattleHudEntityLifecycleBinding _entityLifecycle;
        private BattlePresentationSessionContext _presentation;
        private readonly BattlePresentationSessionResolver _presentationSessions = new BattlePresentationSessionResolver();
        private readonly BattleHudFeatureControllerFactory _controllers = new BattleHudFeatureControllerFactory();
        private readonly BattleHudSessionModel _session = new BattleHudSessionModel();

        private BattleHudAimPreview _aimPreview;

        public BattleHudFeature()
            : this(null)
        {
        }

        internal BattleHudFeature(GameplayPresentationModuleRegistry gameplayModules)
        {
            _gameplayModules = gameplayModules ?? GameplayPresentationModuleRegistry.Default;
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            try
            {
                _ctx = ctx.RequireFeature<IBattleHudPresentationPort>(nameof(BattleHudFeature));
                _hudInput = ctx.RequireFeature<IBattleHudInputSink>(nameof(BattleHudFeature));
                _camera = Camera.main;
                _config = BattleHudConfig.Default;
                _presentation = _presentationSessions.Resolve(ctx);
                SynchronizeSessionFromContext();

                if (_ctx != null && _ctx.EntityNode.IsValid)
                    _hudNode = _ctx.EntityNode.AddChild("BattleHud");

                _canvasController = _controllers.CreateCanvas();
                _canvasController.Create("BattleHudCanvas");
                _binder = _controllers.CreateBinder(_config, _canvasController.Root, _camera, _ctx);
                EnsureGameplayHudModule(ctx.GameplayPresentation);
                _skillFailurePresenter = _controllers.CreateSkillFailures();
                _skillFailurePresenter.Bind(_ctx, _canvasController.Root);
                CreateInputController();
                ApplyLaunchSpecSkillTemplates();
                SubscribeEntityLifecycle();
                SubscribeSnapshots();
            }
            catch (Exception attachFailure)
            {
                Debug.LogWarning($"[BattleHudFeature] Attach failed: {attachFailure}");
                try { OnDetach(ctx); }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("HUD attach and rollback failed.",
                        attachFailure, cleanupFailure);
                }
                throw;
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            var context = ctx;
            BattleFeatureResourceCleanup.ReleaseAll(
                () => { _snapshotController?.Dispose(); _snapshotController = null; },
                () => { _entityLifecycle?.Dispose(); _entityLifecycle = null; },
                () => { _binder?.Clear(); _binder = null; },
                () => { _skillFailurePresenter?.Dispose(); _skillFailurePresenter = null; },
                () =>
                {
                    _gameplayHud?.Dispose();
                    _gameplayHud = null;
                    _gameplayHudModuleId = null;
                    _gameplayHudState = null;
                },
                () => { _inputController?.Dispose(); _inputController = null; },
                () => { _aimPreview?.Clear(); _aimPreview = null; },
                () => _session.Reset(),
                () => { _canvasController?.Dispose(); _canvasController = null; },
                () => { if (_hudNode.IsValid) _hudNode.Destroy(); _hudNode = default; },
                () => { _presentationSessions.Release(context, _presentation); _presentation = null; },
                () => { _ctx = null; _hudInput = null; _camera = null; });
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_binder == null) return;

            EnsureSnapshotSubscription();
            EnsureLocalControlSkillTemplates();
            EnsureGameplayHudModule(ctx.GameplayPresentation);
            _gameplayHud?.SetLocalActorId(_session.LocalActorId);
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
            SynchronizeSessionFromContext();
            ApplyLaunchSpecSkillTemplates();
        }

        private void EnsureLocalControlSkillTemplates()
        {
            SynchronizeSessionFromContext();
            if (_session.RequiresLoadoutBinding)
            {
                ApplyLaunchSpecSkillTemplates();
            }
        }

        private void ApplyLaunchSpecSkillTemplates()
        {
            if (_ctx == null) return;
            var launchSpec = _ctx.Plan.LaunchSpec;
            if (launchSpec.Players == null || launchSpec.Players.Length == 0) return;

            var worldId = !string.IsNullOrEmpty(_ctx.Plan.World.WorldId)
                ? _ctx.Plan.World.WorldId
                : launchSpec.WorldId;

            var res = new BattleEnterGameSnapshot(
                worldId,
                launchSpec.LocalPlayerId.Value,
                _session.LocalActorId,
                launchSpec.RandomSeed,
                launchSpec.TickRate,
                launchSpec.InputDelayFrames,
                playersLoadout: BattleEnterGameSnapshotMapper.MapLoadouts(
                _ctx.BuildEffectivePlayerLoadouts()));

            ApplySkillButtonTemplates(res);
        }

        private void SubscribeEntityLifecycle()
        {
            _entityLifecycle ??= _controllers.CreateEntityLifecycle();
            _entityLifecycle.Bind(_ctx, _binder);
        }

        private void EnsureGameplayHudModule(BattleGameplayPresentationState state)
        {
            if (state == null || _canvasController == null) return;
            var moduleId = state.HudModuleId;
            if (string.IsNullOrWhiteSpace(moduleId)) return;
            if (_gameplayHud != null && _gameplayHudModuleId == moduleId &&
                ReferenceEquals(_gameplayHudState, state)) return;
            var next = _gameplayModules.CreateHud(moduleId);
            try
            {
                next.Bind(_canvasController.Root, state);
                next.SetLocalActorId(_session.LocalActorId);
            }
            catch
            {
                next.Dispose();
                throw;
            }
            _gameplayHud?.Dispose();
            _gameplayHud = next;
            _gameplayHudModuleId = moduleId;
            _gameplayHudState = state;
        }

        private void SubscribeSnapshots()
        {
            _snapshotController = _controllers.CreateSnapshots();
            EnsureSnapshotSubscription();
        }

        private void EnsureSnapshotSubscription()
        {
            if (_snapshotController == null || _snapshotController.IsBoundTo(_ctx)) return;

            _snapshotController.Bind(_ctx, OnEnterGameSnapshot, OnDamageEventSnapshot,
                OnSkillStateSnapshot, OnPresentationCueSnapshot);
        }

        private void OnEnterGameSnapshot(BattleEnterGameSnapshot res)
        {
            if (_ctx == null) return;
            SynchronizeSessionFromContext();
            _session.ApplyEnterGameSnapshot(res.PlayerId, res.LocalActorId);
            PublishLocalActorId();

            if (!_session.ShouldUseEnterGameLoadout(
                    res.PlayerId,
                    !string.IsNullOrEmpty(_ctx.LocalControlPlayerId)))
            {
                ApplyLaunchSpecSkillTemplates();
                return;
            }

            ApplySkillButtonTemplates(res);
        }

        private void ApplySkillButtonTemplates(BattleEnterGameSnapshot res)
        {
            if (_inputController == null ||
                !_inputController.ApplySkillButtonTemplates(res, _session.LocalPlayerId))
            {
                return;
            }

            _session.MarkLoadoutBound();
            _aimPreview?.SetSkillSpecs(_inputController.SkillSpecs);
        }

        private void OnDamageEventSnapshot(DamageEventData[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _binder?.OnDamageEvents(entries);
        }

        private void OnSkillStateSnapshot(SkillStateData[] entries)
        {
            if (_ctx == null || entries == null || entries.Length == 0) return;
            SynchronizeSessionFromContext();
            System.Predicate<SkillStateData> matchesLoadout = null;
            if (_inputController != null)
            {
                matchesLoadout = _inputController.SkillStateMatchesTemplate;
            }

            var localActorId = _session.ResolveLocalActorId(
                entries,
                matchesLoadout);
            PublishLocalActorId();

            if (_inputController != null && _inputController.ApplySkillStates(entries, localActorId))
                _aimPreview?.SetSkillSpecs(_inputController.SkillSpecs);
        }

        private void OnPresentationCueSnapshot(PresentationCueData[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _binder?.OnPresentationCues(entries);
        }

        private void SynchronizeSessionFromContext()
        {
            if (_ctx == null) return;
            _session.Synchronize(
                _ctx.ResolveLocalControlPlayerId(),
                _ctx.LocalActorId,
                _ctx.RuntimePlayerLoadoutRevision);
        }

        private void PublishLocalActorId()
        {
            if (_ctx != null && _ctx.LocalActorId <= 0 && _session.LocalActorId > 0)
            {
                _ctx.LocalActorId = _session.LocalActorId;
            }
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
            IBattleEntityContext ctx)
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

    internal static class BattleFeatureResourceCleanup
    {
        internal static void ReleaseAll(params Action[] releases)
        {
            List<Exception> failures = null;
            foreach (var release in releases)
            {
                try { release(); }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }
            if (failures != null)
                throw new AggregateException("Battle feature cleanup failed.", failures);
        }
    }
}
