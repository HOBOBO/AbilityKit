#nullable enable

using System.Collections.Generic;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 配置提供器接口
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>
        /// 获取配置节
        /// </summary>
        T GetSection<T>(string sectionName) where T : class, new();

        /// <summary>
        /// 获取配置节（可返回 null）
        /// </summary>
        T? GetSectionOrDefault<T>(string sectionName) where T : class;

        /// <summary>
        /// 获取配置值
        /// </summary>
        T GetValue<T>(string key, T defaultValue = default);

        /// <summary>
        /// 获取字典配置
        /// </summary>
        Dictionary<string, T> GetDictionary<T>(string sectionName) where T : class, new();

        /// <summary>
        /// 检查配置节是否存在
        /// </summary>
        bool HasSection(string sectionName);

        /// <summary>
        /// 检查配置键是否存在
        /// </summary>
        bool HasKey(string key);
    }
}
