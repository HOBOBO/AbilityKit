using System.Collections.Generic;
using Emilia.Reference;

namespace Emilia.Expressions
{
    /// <summary>
    /// 表达式求值上下文
    /// </summary>
    public class ExpressionContext : IReference
    {
        private Dictionary<string, object> _variables = new();

        public ExpressionConfig config { get; set; }
        public object userData { get; set; }

        public void Clear()
        {
            _variables.Clear();
            config = null;
            userData = null;
        }

        public ExpressionContext SetVariable(string name, object value)
        {
            _variables[name] = value;
            return this;
        }

        public bool TryGetVariable(string name, out object value) => _variables.TryGetValue(name, out value);
    }
}