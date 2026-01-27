using System;
using System.Diagnostics;

namespace Emilia.Kit
{
    [Conditional("UNITY_EDITOR"), AttributeUsage(AttributeTargets.Field)]
    public class BoolToggleButtonAttribute : Attribute
    {
        public string trueText;
        public string falseText;

        public BoolToggleButtonAttribute(string trueText, string falseText)
        {
            this.trueText = trueText;
            this.falseText = falseText;
        }
    }
}