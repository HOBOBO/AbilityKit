using System;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留至下一主版本移除窗口的可变模块配置数组，新代码使用所属引导包的数据契约。
    /// </summary>
    /// <remarks>
    /// 不复制数组或配置，不保证线程安全，也不执行安装、去重或排序。
    /// </remarks>
    [Obsolete("Module installation policy no longer belongs in Core. Move it to the owning bootstrap package; this compatibility API will be removed in the next major version.")]
    public sealed class ModuleInstallerConfigSet
    {
        /// <summary>
        /// 模块配置数组，默认空数组；调用方可替换数组或修改元素，后续查找会读取当前数据。
        /// </summary>
        public ModuleInstallerConfig[] Modules = Array.Empty<ModuleInstallerConfig>();

        /// <summary>
        /// 按数组顺序返回首个有效且键按序号相等的配置原对象，重复键不合并。
        /// </summary>
        /// <remarks>
        /// 空数组引用、空数组、空元素及无效配置均被忽略；空查询键没有匹配结果。
        /// 查询开始时保存当前数组引用，不提供并发变更或元素快照保证。
        /// </remarks>
        /// <param name="moduleKey">区分大小写且不裁剪空白的模块键。</param>
        /// <returns>首个匹配配置的原始引用，或未找到时的空值。</returns>
        public ModuleInstallerConfig? FindModule(string moduleKey)
        {
            var ms = Modules;
            if (ms == null || ms.Length == 0) return null;

            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i];
                if (m == null || !m.IsValid) continue;
                if (string.Equals(m.ModuleKey, moduleKey, StringComparison.Ordinal)) return m;
            }

            return null;
        }
    }
}
