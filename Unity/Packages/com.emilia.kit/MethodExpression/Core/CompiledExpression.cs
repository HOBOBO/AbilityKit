using System;
using UnityEngine;

namespace Emilia.Expressions
{
    /// <summary>
    /// 已编译的表达式
    /// </summary>
    [Serializable]
    public class CompiledExpression
    {
        [SerializeField] private string _sourceExpression;
        [SerializeField] private Expression _compiledExpression;
        [SerializeField] private bool _isCompiled;
        [SerializeField] private string _compileError;

        public string sourceExpression
        {
            get => _sourceExpression;
            set
            {
                if (this._sourceExpression == value) return;
                this._sourceExpression = value;
                this._isCompiled = false;
                this._compiledExpression = null;
                this._compileError = null;
            }
        }

        public Expression compiledExpressionAST => _compiledExpression;
        public bool isCompiled => _isCompiled;
        public string compileError => _compileError;
        public bool hasError => ! string.IsNullOrEmpty(_compileError);

        public CompiledExpression() { }

        public CompiledExpression(string expression)
        {
            _sourceExpression = expression;
        }

        public bool Compile(ExpressionConfig config)
        {
            if (string.IsNullOrEmpty(_sourceExpression))
            {
                _compileError = "表达式为空";
                _isCompiled = false;
                return false;
            }

            if (config == null)
            {
                _compileError = "配置为空";
                _isCompiled = false;
                return false;
            }

            try
            {
                _compiledExpression = ExpressionUtility.Parse(_sourceExpression, config);
                _isCompiled = true;
                _compileError = null;
                return true;
            }
            catch (ExpressionParseException e)
            {
                _compileError = e.Message;
                _isCompiled = false;
                _compiledExpression = null;
                return false;
            }
            catch (Exception e)
            {
                _compileError = $"编译错误: {e.Message}";
                _isCompiled = false;
                _compiledExpression = null;
                return false;
            }
        }

        public object Evaluate(ExpressionContext context)
        {
            if (! _isCompiled)
            {
                if (! Compile(context.config)) throw new ExpressionEvaluateException(_compileError);
            }

            return _compiledExpression.Evaluate(context);
        }

        public T Evaluate<T>(ExpressionContext context)
        {
            object result = Evaluate(context);
            return ExpressionUtility.ConvertTo<T>(result);
        }

        public override string ToString() => _sourceExpression ?? "";
    }
}