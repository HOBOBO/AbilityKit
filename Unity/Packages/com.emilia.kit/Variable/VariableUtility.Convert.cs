using System;
using UnityEngine;

namespace Emilia.Variables
{
    public partial class VariableUtility
    {
        public static Variable Convert(Variable form, Variable to)
        {
            ConvertMatching convertMatching = new ConvertMatching(form.GetType(), to.GetType());
            if (variableConvert.TryGetValue(convertMatching, out var convert)) return convert(form);
            Debug.LogError($"{form} 无法转换为 {to}");
            return null;
        }

        struct ConvertMatching : IEquatable<ConvertMatching>
        {
            public Type form;
            public Type to;

            public ConvertMatching(Type form, Type to)
            {
                this.form = form;
                this.to = to;
            }

            public bool Equals(ConvertMatching other)
            {
                return this.form == other.form && this.to == other.to;
            }

            public override bool Equals(object obj)
            {
                return obj is ConvertMatching other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(this.form, this.to);
            }
        }
    }
}