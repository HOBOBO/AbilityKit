using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    public interface IPresentationActorPlacementSource
    {
        bool TryGetPosition(int actorId, out SnapshotVec3 position);
        bool HasActor(int actorId);
    }

    public static class BattlePresentationCuePlacementResolver
    {
        public static SnapshotVec3 ResolvePosition(
            in BattlePresentationCueSpawnRequest request,
            IPresentationActorPlacementSource actors)
        {
            var position = request.HasExplicitPosition
                ? request.ExplicitPosition
                : ResolveActorPosition(in request, actors);
            return new SnapshotVec3(
                position.X + request.Offset.X,
                position.Y + request.Offset.Y,
                position.Z + request.Offset.Z);
        }

        public static int ResolveFollowActorId(
            in BattlePresentationCueSpawnRequest request,
            IPresentationActorPlacementSource actors)
        {
            if (actors == null) return 0;
            if (request.TargetActorId > 0 && actors.HasActor(request.TargetActorId)) return request.TargetActorId;
            if (request.FirstTargetActorId > 0 && actors.HasActor(request.FirstTargetActorId)) return request.FirstTargetActorId;
            if (request.SourceActorId > 0 && actors.HasActor(request.SourceActorId)) return request.SourceActorId;
            return 0;
        }

        private static SnapshotVec3 ResolveActorPosition(
            in BattlePresentationCueSpawnRequest request,
            IPresentationActorPlacementSource actors)
        {
            if (actors != null)
            {
                if (request.TargetActorId > 0 && actors.TryGetPosition(request.TargetActorId, out var target)) return target;
                if (request.FirstTargetActorId > 0 && actors.TryGetPosition(request.FirstTargetActorId, out var first)) return first;
                if (request.SourceActorId > 0 && actors.TryGetPosition(request.SourceActorId, out var source)) return source;
            }

            return default;
        }
    }
}
