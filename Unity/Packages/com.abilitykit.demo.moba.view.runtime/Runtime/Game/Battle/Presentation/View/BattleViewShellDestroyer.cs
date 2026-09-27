using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Destroys (or returns to pool) entity shell GameObjects.
    /// </summary>
    internal sealed class BattleViewShellDestroyer
    {
        private readonly BattleViewShellHandleBinder _handleBinder;
        private readonly BattleViewShellPool _pool;

        public BattleViewShellDestroyer(
            BattleViewShellHandleBinder handleBinder,
            BattleViewShellPool pool = null)
        {
            _handleBinder = handleBinder;
            _pool = pool;
        }

        public void Destroy(BattleViewHandle handle, bool immediate)
        {
            if (handle == null) return;

            _handleBinder?.Unbind(handle);

            if (handle.GameObject != null)
            {
                if (immediate)
                {
                    Object.DestroyImmediate(handle.GameObject);
                }
                else
                {
                    ReturnToPoolOrDestroy(handle);
                }
            }

            handle.GameObject = null;
        }

        private void ReturnToPoolOrDestroy(BattleViewHandle handle)
        {
            var go = handle.GameObject;
            if (go == null) return;

            // Try to return to pool using the modelId stored on the handle.
            // Fall back to the tag on the GameObject if modelId is not set.
            int modelId = handle.ModelId;
            if (modelId <= 0)
            {
                modelId = BattleShellPoolableTag.ReadModelId(go);
            }

            if (_pool != null && modelId > 0 && _pool.TryReturn(modelId, go))
            {
                return;
            }

            // Pool unavailable or miss — destroy normally
            Object.Destroy(go);
        }
    }
}
