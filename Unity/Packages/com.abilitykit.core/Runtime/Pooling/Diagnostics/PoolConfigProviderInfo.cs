namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 描述对象池配置提供者的诊断信息，便于大型项目排查配置来源和覆盖顺序。
    /// </summary>
    public readonly struct PoolConfigProviderInfo
    {
        /// <summary>
        /// 配置提供者名称。
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// 配置来源，通常为程序集名、包名或资源路径。
        /// </summary>
        public readonly string Source;

        /// <summary>
        /// 配置优先级；数值越大越优先。
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// 配置提供者注册顺序；优先级相同时，后注册者优先。
        /// </summary>
        public readonly int RegistrationOrder;

        /// <summary>
        /// 创建配置提供者诊断信息。
        /// </summary>
        /// <param name="name">配置提供者名称。</param>
        /// <param name="source">配置来源。</param>
        /// <param name="priority">配置优先级。</param>
        /// <param name="registrationOrder">注册顺序。</param>
        public PoolConfigProviderInfo(string name, string? source, int priority, int registrationOrder)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed" : name;
            Source = source ?? string.Empty;
            Priority = priority;
            RegistrationOrder = registrationOrder;
        }

        /// <summary>
        /// 返回便于日志输出的配置提供者描述。
        /// </summary>
        /// <returns>配置提供者描述。</returns>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Source)
                ? $"{Name}, priority={Priority}, order={RegistrationOrder}"
                : $"{Name}, source={Source}, priority={Priority}, order={RegistrationOrder}";
        }
    }
}
