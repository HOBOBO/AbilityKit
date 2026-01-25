using System;

namespace AbilityKit.Ability.Share.Impl.Moba
{
    public static class MobaEntitasContextsExtensions
    {
        public static global::ActorContext Actor(this global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            return ((global::Contexts)contexts).actor;
        }

        public static global::GameContext Game(this global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            return ((global::Contexts)contexts).game;
        }

        public static global::InputContext Input(this global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            return ((global::Contexts)contexts).input;
        }

        public static global::ServiceContext Service(this global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            return ((global::Contexts)contexts).service;
        }
    }
}
