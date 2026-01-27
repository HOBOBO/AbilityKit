using System;
using Sirenix.OdinInspector;

namespace Emilia.Expressions
{
    [Serializable, HideReferenceObjectPicker]
    public abstract class Expression
    {
        public abstract object Evaluate(ExpressionContext context);
    }
}