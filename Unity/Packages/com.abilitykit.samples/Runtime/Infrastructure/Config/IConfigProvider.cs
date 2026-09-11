#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 閰嶇疆鎻愪緵鍣ㄦ帴鍙?
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>
        /// 鑾峰彇閰嶇疆鑺?
        /// </summary>
        T GetSection<T>(string sectionName) where T : class, new();

        /// <summary>
        /// 鑾峰彇閰嶇疆鑺傦紙鍙繑鍥?null锛?
        /// </summary>
        T? GetSectionOrDefault<T>(string sectionName) where T : class;

        /// <summary>
        /// 鑾峰彇閰嶇疆鍊?
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
