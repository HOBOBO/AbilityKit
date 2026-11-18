using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留至下一主版本移除窗口的设置文件读写兼容入口，新代码由应用所有者管理持久化策略。
    /// </summary>
    /// <remarks>
    /// 同步访问文件，不缓存、不调度回调；读写、序列化和并发协调均由调用方负责。
    /// 失败返回空设置或假，不提供诊断输出、原子替换或失败回滚。
    /// </remarks>
    [Obsolete("Application settings no longer belong in Core. Move settings to the owning application package; this compatibility API will be removed in the next major version.")]
    public static class JsonSettingsFiles
    {
        /// <summary>
        /// 读取完整路径对应的文件并通过 FlatJsonSettings.FromJson 构建设置，失败返回新的空设置。
        /// </summary>
        /// <remarks>
        /// 空路径、文件不存在、读取或反序列化失败均返回空设置；空文本和空委托不调用反序列化器。
        /// 不额外校验 JSON 语法或裁剪文本；成功时复制字典结构，值对象继续共享。
        /// </remarks>
        /// <param name="filePath">由 Path.GetFullPath 解析的文件路径。</param>
        /// <param name="deserialize">接收原始文件文本的同步反序列化器；兼容实现允许空值。</param>
        /// <returns>读取的设置或无法区分具体失败原因的空设置。</returns>
        public static FlatJsonSettings LoadFlatOrEmpty(string filePath, Func<string, Dictionary<string, object>> deserialize)
        {
            if (string.IsNullOrEmpty(filePath)) return FlatJsonSettings.Empty();
            try
            {
                var full = Path.GetFullPath(filePath);
                if (!File.Exists(full)) return FlatJsonSettings.Empty();
                var json = File.ReadAllText(full);
                return FlatJsonSettings.FromJson(json, deserialize);
            }
            catch
            {
                /*
                 * 兼容读取将路径、文件及设置构建失败统一映射为空设置，不向调用方暴露异常。
                 */
                return FlatJsonSettings.Empty();
            }
        }

        /// <summary>
        /// 创建父目录后序列化并直接覆盖目标文件；失败返回假，已发生的目录或文件变更不回滚。
        /// </summary>
        /// <remarks>
        /// 空路径或空序列化器直接失败；空设置引用按新空字典处理，非空设置原样传给回调。
        /// 序列化器返回空引用时写入空文本；目录创建先于回调，文件写入不采用临时文件和原子替换。
        /// </remarks>
        /// <param name="filePath">由 Path.GetFullPath 解析的目标路径。</param>
        /// <param name="overrides">待保存设置；兼容实现允许空引用，不复制非空字典。</param>
        /// <param name="serialize">同步序列化器；返回值不经过 JSON 校验。</param>
        /// <returns>文本写入正常返回时为真；校验拒绝或捕获异常时为假。</returns>
        public static bool TrySaveOverrides(string filePath, IReadOnlyDictionary<string, object> overrides, Func<IReadOnlyDictionary<string, object>, string> serialize)
        {
            if (serialize == null) return false;
            if (string.IsNullOrEmpty(filePath)) return false;

            try
            {
                var full = Path.GetFullPath(filePath);
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var json = serialize(overrides ?? new Dictionary<string, object>(0));
                if (json == null) json = string.Empty;
                File.WriteAllText(full, json);
                return true;
            }
            catch
            {
                /*
                 * 保留既有失败结果；返回假不能证明之前的目录创建或文件写入没有副作用。
                 */
                return false;
            }
        }
    }
}
