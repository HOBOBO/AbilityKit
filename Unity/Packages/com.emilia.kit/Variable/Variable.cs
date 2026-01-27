using System;
using Emilia.Reference;
using Sirenix.OdinInspector;

namespace Emilia.Variables
{
    [Serializable, HideReferenceObjectPicker, InlineProperty]
    public abstract class Variable : IReference
    {
        public abstract Type type { get; }

        public abstract object GetValue();
        public abstract void SetValue(object value);

        public abstract void Clear();
    }
}