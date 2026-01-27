using System;
using Sirenix.OdinInspector;

namespace Emilia.Kit.Editor
{
    [Serializable, HideReferenceObjectPicker]
    public abstract class Agent : IAgent
    {
        public virtual void Start() { }
        public virtual void Update() { }
        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void OnDestroy() { }
    }
}