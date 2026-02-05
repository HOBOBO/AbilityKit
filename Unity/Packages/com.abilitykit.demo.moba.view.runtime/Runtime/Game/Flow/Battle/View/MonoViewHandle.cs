using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public interface IMonoViewHandleRegistry
    {
        void OnMonoViewHandleDestroyed(MonoViewHandle handle);
    }

    public sealed class MonoViewHandle : MonoBehaviour
    {
        public int ActorId;

        public IMonoViewHandleRegistry Registry;

        private void OnDestroy()
        {
            Registry?.OnMonoViewHandleDestroyed(this);
        }
    }
}
