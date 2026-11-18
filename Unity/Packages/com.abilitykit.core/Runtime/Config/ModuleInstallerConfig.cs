using System;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留至下一主版本移除窗口的可变模块安装配置，新代码使用所属引导包的窄契约。
    /// </summary>
    /// <remarks>
    /// 仅保存名称并提供基本非空判断，不解析类型、不验证方法，也不执行安装或管理依赖顺序。
    /// </remarks>
    [Obsolete("Module installation policy no longer belongs in Core. Move it to the owning bootstrap package; this compatibility API will be removed in the next major version.")]
    public sealed class ModuleInstallerConfig
    {
        /// <summary>
        /// 模块查找键，默认空字符串；配置集按序号区分大小写匹配，不裁剪空白。
        /// </summary>
        public string ModuleKey = string.Empty;

        /// <summary>
        /// 安装器类型名称，默认空字符串；本配置不验证名称能否解析。
        /// </summary>
        public string InstallerType = string.Empty;

        /// <summary>
        /// 安装方法名称，默认空字符串；空引用或空字符串采用兼容默认方法名。
        /// </summary>
        public string InstallerMethod = string.Empty;

        /// <summary>
        /// 获取模块键及类型名称是否均非空；空白字符串有效，方法名不参与判断。
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(ModuleKey) && !string.IsNullOrEmpty(InstallerType);

        /// <summary>
        /// 返回配置的方法名，空引用或空字符串回退为 InstallAsCurrent；不校验方法是否存在。
        /// </summary>
        public string GetEffectiveMethod() => string.IsNullOrEmpty(InstallerMethod) ? "InstallAsCurrent" : InstallerMethod;
    }
}
