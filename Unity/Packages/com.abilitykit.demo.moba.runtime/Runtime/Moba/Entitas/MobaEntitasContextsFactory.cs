using AbilityKit.Ability.World.Entitas;
using AbilityKit.Core.Common.Log;

namespace AbilityKit.Demo.Moba.EntitasAdapters
{
    public sealed class MobaEntitasContextsFactory : IEntitasContextsFactory
    {
        public global::Entitas.IContexts Create()
        {
            return new global::Contexts();
        }

        public void Release(global::Entitas.IContexts contexts)
        {
            try
            {
                (contexts as global::Contexts)?.Reset();
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[MobaEntitasContextsFactory] contexts reset failed");
            }
        }
    }
}
