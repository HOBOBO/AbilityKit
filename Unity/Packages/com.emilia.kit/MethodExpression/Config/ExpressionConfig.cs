using System;
using System.Collections.Generic;

namespace Emilia.Expressions
{
    /// <summary>
    /// 表达式配置
    /// </summary>
    public class ExpressionConfig
    {
        private Dictionary<string, object> _constants = new();
        private Dictionary<string, Func<object[], ExpressionContext, object>> _functions = new();

        public ExpressionConfig RegisterConstant(string name, object value)
        {
            _constants[name] = value;
            return this;
        }

        public bool TryGetConstant(string name, out object value) => _constants.TryGetValue(name, out value);

        public bool IsConstant(string name) => _constants.ContainsKey(name);

        public ExpressionConfig RegisterFunction(string name, Func<object[], ExpressionContext, object> func)
        {
            _functions[name] = func;
            return this;
        }

        public bool TryGetFunction(string name, out Func<object[], ExpressionContext, object> function) => _functions.TryGetValue(name, out function);

        public bool IsFunction(string name) => _functions.ContainsKey(name);
    }
}