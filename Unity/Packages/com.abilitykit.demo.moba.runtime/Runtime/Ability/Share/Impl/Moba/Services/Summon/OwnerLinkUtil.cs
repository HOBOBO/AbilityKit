namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class OwnerLinkUtil
    {
        public static int ResolveRootOwner(global::ActorEntity entity)
        {
            if (entity == null) return 0;

            if (entity.hasOwnerLink && entity.ownerLink != null)
            {
                var link = entity.ownerLink;
                if (link.RootOwnerActorId > 0) return link.RootOwnerActorId;
                if (link.OwnerActorId > 0) return link.OwnerActorId;
            }

            if (entity.hasActorId) return entity.actorId.Value;
            return 0;
        }
    }
}
