using AbilityKit.Game.Flow.Battle.Hud;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudBinder : IBattleHudActorPositionResolver
    {
        private readonly BattleHudActorResolver _actors;
        private readonly BattleHudHpBarController _hpBars;
        private readonly BattleHudFloatingTextController _floatingTexts;
        private readonly BattleHudDamageEventPresenter _damageEvents;
        private readonly BattleHudBuffBarController _buffBars;
        private readonly BattleHudChargeBarController _chargeBars;

        public BattleHudBinder(
            BattleHudConfig cfg,
            RectTransform root,
            Camera camera,
            IBattleEntityContext ctx,
            BattleHudBinderControllerFactory controllers = null)
        {
            controllers ??= new BattleHudBinderControllerFactory();

            _actors = controllers.CreateActors(ctx);

            var projector = controllers.CreateProjector(root, camera);
            var fallbackUi = controllers.CreateFallbackUi();
            _hpBars = controllers.CreateHpBars(cfg, root, projector, this, fallbackUi);
            _floatingTexts = controllers.CreateFloatingTexts(cfg, root, projector, this, fallbackUi);
            _damageEvents = controllers.CreateDamageEvents(_hpBars, _floatingTexts);
            _buffBars = controllers.CreateBuffBars(cfg, root, projector, this);
            _chargeBars = controllers.CreateChargeBars(cfg, root, projector, this);
        }

        public void OnDamageEvents(DamageEventData[] entries)
        {
            _damageEvents.Present(entries);
        }

        public void OnPresentationCues(PresentationCueData[] entries)
        {
            if (entries == null || entries.Length == 0) return;
            _buffBars.HandleCues(entries);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Domain == PresentationCueDomain.Buff &&
                    entry.TargetActorId > 0 &&
                    !string.IsNullOrEmpty(entry.InstanceKey) &&
                    entry.InstanceKey.StartsWith("gameplay-counter:", System.StringComparison.Ordinal) &&
                    PresentationCueSemantics.Classify(entry.Stage) != PresentationCueTransition.Stop)
                {
                    _floatingTexts.SpawnStatus(
                        entry.TargetActorId,
                        entry.StackCount.ToString(),
                        new Color(entry.ColorR, entry.ColorG, entry.ColorB, entry.ColorA),
                        450);
                    continue;
                }
                if (entry.Domain != PresentationCueDomain.Tag ||
                    entry.Stage != PresentationCueStage.Started ||
                    entry.TargetActorId <= 0) continue;
                if (BattleHudStatusCueFilter.TryResolveHeadText(in entry, out var text, out var color, out var durationMs))
                    _floatingTexts.SpawnStatus(entry.TargetActorId, text, color, durationMs);
            }
        }

        public void OnSkillStates(SkillStateData[] entries, int localActorId)
        {
            _chargeBars.Apply(entries, localActorId);
        }

        public void OnShieldStates(ShieldStateData[] entries)
        {
            _hpBars.UpdateShields(entries);
        }

        public void Tick(float deltaTime)
        {
            _hpBars.Tick();
            _floatingTexts.Tick(deltaTime);
            _buffBars.Tick(deltaTime);
            _chargeBars.Tick();
        }

        public void OnEntityDestroyed(EC.IEntityId id)
        {
            if (!_actors.TryResolveActorId(id, out var actorId)) return;

            _hpBars.RemoveActor(actorId);
            _floatingTexts.RemoveActor(actorId);
            _buffBars.RemoveActor(actorId);
            _chargeBars.RemoveActor(actorId);
        }

        public void Clear()
        {
            _hpBars.Clear();
            _floatingTexts.Clear();
            _buffBars.Clear();
            _chargeBars.Clear();
        }

        public void ClearPresentationCues()
        {
            _buffBars.Clear();
        }

        public bool TryGetActorWorldPos(int actorId, out Vector3 pos)
        {
            return _actors.TryGetActorWorldPos(actorId, out pos);
        }
    }

    internal sealed class BattleHudBinderControllerFactory
    {
        public BattleHudActorResolver CreateActors(IBattleEntityContext ctx)
        {
            return new BattleHudActorResolver(ctx);
        }

        public BattleHudCanvasProjector CreateProjector(RectTransform root, Camera camera)
        {
            return new BattleHudCanvasProjector(root, camera);
        }

        public BattleHudFallbackUiFactory CreateFallbackUi()
        {
            return new BattleHudFallbackUiFactory();
        }

        public BattleHudHpBarController CreateHpBars(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver actors,
            BattleHudFallbackUiFactory fallbackUi)
        {
            return new BattleHudHpBarController(
                cfg,
                root,
                projector,
                actors,
                new BattleHudHpBarFactory(fallbackUi));
        }

        public BattleHudFloatingTextController CreateFloatingTexts(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver actors,
            BattleHudFallbackUiFactory fallbackUi)
        {
            return new BattleHudFloatingTextController(
                cfg,
                root,
                projector,
                actors,
                new BattleHudFloatingTextPool(root, fallbackUi));
        }

        public BattleHudDamageEventPresenter CreateDamageEvents(
            BattleHudHpBarController hpBars,
            BattleHudFloatingTextController floatingTexts)
        {
            return new BattleHudDamageEventPresenter(hpBars, floatingTexts);
        }

        public BattleHudBuffBarController CreateBuffBars(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver actors)
        {
            return new BattleHudBuffBarController(cfg, root, projector, actors);
        }

        public BattleHudChargeBarController CreateChargeBars(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver actors)
        {
            return new BattleHudChargeBarController(cfg, root, projector, actors);
        }
    }
}
