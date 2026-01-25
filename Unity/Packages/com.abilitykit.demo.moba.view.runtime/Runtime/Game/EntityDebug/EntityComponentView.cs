using UnityEngine;

namespace AbilityKit.Game.EntityDebug
{
    public sealed class EntityComponentView : MonoBehaviour
    {
        [SerializeField] private EntityView _entity;
        [SerializeField] private bool _dirty;

        public EntityView Entity => _entity;

        public void Bind(EntityView entity)
        {
            _entity = entity;
            _dirty = true;
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public bool ConsumeDirty()
        {
            var d = _dirty;
            _dirty = false;
            return d;
        }
    }
}
