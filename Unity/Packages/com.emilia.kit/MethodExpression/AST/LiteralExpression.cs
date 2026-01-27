using System;
using System.Globalization;
using UnityEngine;

namespace Emilia.Expressions
{
    public enum LiteralType
    {
        Integer,
        Float,
        Double,
        String,
        Boolean,
        Null
    }

    [Serializable]
    public class LiteralExpression : Expression
    {
        [SerializeField] public LiteralType literalType;
        [SerializeField] public string rawValue;

        [NonSerialized] private object _cachedValue;
        [NonSerialized] private bool _isCached;

        public LiteralExpression() { }

        public LiteralExpression(LiteralType literalType, string rawValue)
        {
            this.literalType = literalType;
            this.rawValue = rawValue;
        }

        public override object Evaluate(ExpressionContext context)
        {
            if (_isCached) return _cachedValue;
            _cachedValue = ParseValue();
            _isCached = true;
            return _cachedValue;
        }

        private object ParseValue()
        {
            switch (literalType)
            {
                case LiteralType.Integer:
                    return long.TryParse(rawValue, out long longVal) ? longVal : 0L;

                case LiteralType.Float:
                    string floatStr = rawValue;
                    if (floatStr.EndsWith("f") || floatStr.EndsWith("F")) floatStr = floatStr.Substring(0, floatStr.Length - 1);
                    return float.TryParse(floatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal) ? floatVal : 0f;

                case LiteralType.Double:
                    string doubleStr = rawValue;
                    if (doubleStr.EndsWith("d") || doubleStr.EndsWith("D")) doubleStr = doubleStr.Substring(0, doubleStr.Length - 1);
                    return double.TryParse(doubleStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleVal) ? doubleVal : 0d;

                case LiteralType.String:
                    return rawValue;

                case LiteralType.Boolean:
                    return rawValue == "true";

                default:
                    return null;
            }
        }
    }
}