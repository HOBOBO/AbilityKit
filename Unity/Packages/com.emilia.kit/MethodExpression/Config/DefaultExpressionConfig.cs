namespace Emilia.Expressions
{
    /// <summary>
    /// 默认表达式配置
    /// </summary>
    public static class DefaultExpressionConfig
    {
        private static ExpressionConfig _instance;

        /// <summary>
        /// 获取默认配置实例
        /// </summary>
        public static ExpressionConfig instance
        {
            get
            {
                if (_instance != null) return _instance;
                
                _instance = new ExpressionConfig();
                BuiltInFunctions.RegisterAll(_instance);
                
                return _instance;
            }
        }
    }
}