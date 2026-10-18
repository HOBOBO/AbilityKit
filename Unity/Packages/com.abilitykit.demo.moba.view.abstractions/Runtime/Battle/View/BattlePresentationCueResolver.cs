using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.View.Abstractions.Shared.Types;

namespace AbilityKit.Demo.Moba.View.Abstractions.Battle.View
{
    public static class BattlePresentationCueResolver
    {
        public static BattlePresentationCueDecision Resolve(in BattlePresentationCueData data)
        {
            var requestKey = BattlePresentationCueRequestKey.From(in data);
            if (requestKey.IsEmpty) return BattlePresentationCueDecision.None;

            if (ShouldStart(data.Stage) || ShouldKeepActive(data.Stage))
            {
                var spawnRequest = CreateSpawnRequest(requestKey, in data);
                if (spawnRequest.IsEmpty) return BattlePresentationCueDecision.None;

                return new BattlePresentationCueDecision(BattlePresentationCueDecisionKind.Play, requestKey, spawnRequest);
            }

            if (ShouldStop(data.Stage))
            {
                return new BattlePresentationCueDecision(BattlePresentationCueDecisionKind.Stop, requestKey, default);
            }

            return BattlePresentationCueDecision.None;
        }

        // 迁移分类转发到共享权威实现，避免第四份拷贝再次走样。
        public static bool ShouldStart(BattlePresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.Start;
        }

        public static bool ShouldKeepActive(BattlePresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.KeepActive;
        }

        public static bool ShouldStop(BattlePresentationCueStage stage)
        {
            return PresentationCueSemantics.Classify((int)stage) == PresentationCueTransition.Stop;
        }

        public static int ResolveVfxId(in BattlePresentationCueData data)
        {
            if (data.VfxId > 0) return data.VfxId;
            if (data.TemplateId > 0) return data.TemplateId;
            return 0;
        }

        public static BattlePresentationCueSpawnRequest CreateSpawnRequest(
            BattlePresentationCueRequestKey requestKey,
            in BattlePresentationCueData data)
        {
            var vfxId = ResolveVfxId(in data);
            if (requestKey.IsEmpty || vfxId <= 0) return default;

            var hasExplicitPosition = data.Positions != null && data.Positions.Count > 0;

            return new BattlePresentationCueSpawnRequest(
                requestKey,
                vfxId,
                data.SourceActorId,
                data.TargetActorId,
                ResolveFirstTargetActorId(in data),
                hasExplicitPosition,
                hasExplicitPosition ? data.Positions[0] : default,
                data.Offset);
        }

        public static int ResolveFirstTargetActorId(in BattlePresentationCueData data)
        {
            if (data.Targets != null && data.Targets.Count > 0) return data.Targets[0];
            return 0;
        }
    }
}
