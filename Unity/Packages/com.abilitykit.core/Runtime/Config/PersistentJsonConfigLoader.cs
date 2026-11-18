using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留至下一主版本移除窗口的持久化配置读取兼容入口，新代码由应用所有者管理加载与缓存。
    /// </summary>
    /// <remarks>
    /// 按完整路径及请求类型缓存原始对象，以文件写入时间相等判断命中；不比较文件内容或序列化器。
    /// 字典访问有锁，文件读取及回调在锁外，不提供单次加载合并、完整文件快照或清空代次屏障。
    /// </remarks>
    [Obsolete("Persistent application config loading no longer belongs in Core. Move it to the owning bootstrap package; this compatibility API will be removed in the next major version.")]
    public static class PersistentJsonConfigLoader
    {
        /// <summary>
        /// 以完整路径的序号相等性及请求类型区分静态缓存项，不按文件系统大小写规则合并。
        /// </summary>
        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly string Path;
            public readonly Type Type;

            public CacheKey(string path, Type type)
            {
                Path = path;
                Type = type;
            }

            public bool Equals(CacheKey other) => string.Equals(Path, other.Path) && Type == other.Type;
            public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Path, Type);
        }

        /// <summary>
        /// 保存读取前取得的文件写入时间及反序列化原对象，值允许为空且不进行深复制。
        /// </summary>
        private sealed class CacheEntry
        {
            public DateTime LastWriteTimeUtc;
            public object? Value;
        }

        private static readonly object s_gate = new object();
        private static readonly Dictionary<CacheKey, CacheEntry> s_cache = new Dictionary<CacheKey, CacheEntry>(16);

        /// <summary>
        /// 返回加载结果，结果为空时每次创建新的默认实例；默认实例不加入缓存。
        /// </summary>
        /// <remarks>
        /// 默认构造位于 TryLoad 的异常隔离之外，构造失败会传播，不转换成空值。
        /// </remarks>
        /// <typeparam name="T">具有公开无参构造的配置引用类型。</typeparam>
        /// <param name="filePath">配置文件路径。</param>
        /// <param name="deserialize">同步反序列化器；为空时不查询缓存，直接进入默认构造。</param>
        /// <returns>缓存或加载的原对象，或新建的默认对象。</returns>
        public static T LoadOrDefault<T>(string filePath, Func<string, T?>? deserialize) where T : class, new()
        {
            var v = TryLoad<T>(filePath, deserialize);
            return v ?? new T();
        }

        /// <summary>
        /// 同步读取配置；同路径、请求类型及文件写入时间命中时返回缓存原对象，否则在锁外反序列化。
        /// </summary>
        /// <remarks>
        /// 空委托、空路径、文件不存在、空文本或捕获异常时返回空值；空白文本仍交给回调。
        /// 回调正常返回的空值也会缓存；读取失败、空文本或回调抛出不会写入或清除旧缓存。
        /// 不同回调可共享同一缓存对象，并发未命中可重复加载并以后提交者覆盖，调用方负责更强一致性。
        /// </remarks>
        /// <typeparam name="T">参与缓存键的请求配置引用类型。</typeparam>
        /// <param name="filePath">经 Path.GetFullPath 解析的文件路径；不解析文件别名或链接身份。</param>
        /// <param name="deserialize">接收原始非空文件文本的同步回调，不参与缓存键。</param>
        /// <returns>缓存或加载的原对象，或无结果及捕获失败时的空值。</returns>
        public static T? TryLoad<T>(string filePath, Func<string, T?>? deserialize) where T : class
        {
            try
            {
                if (deserialize == null) return null;
                if (string.IsNullOrEmpty(filePath)) return null;

                var path = Path.GetFullPath(filePath);
                if (!File.Exists(path)) return null;

                var lastWrite = File.GetLastWriteTimeUtc(path);

                var key = new CacheKey(path, typeof(T));
                lock (s_gate)
                {
                    if (s_cache.TryGetValue(key, out var entry) && entry != null)
                    {
                        if (entry.LastWriteTimeUtc == lastWrite)
                        {
                            return entry.Value as T;
                        }
                    }
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return null;

                var v = deserialize(json);
                /*
                 * 在锁外读取与解析后再提交；即使回调期间清空缓存，此次结果仍可能重新写入。
                 */
                lock (s_gate)
                {
                    s_cache[key] = new CacheEntry
                    {
                        LastWriteTimeUtc = lastWrite,
                        Value = v,
                    };
                }
                return v;
            }
            catch
            {
                /*
                 * 兼容加载统一将路径、文件和回调失败映射为空值，不删除此前缓存或暴露异常。
                 */
                return null;
            }
        }

        /// <summary>
        /// 在锁内清空当前静态缓存，不释放缓存对象，也不取消或阻止在途读取重新提交。
        /// </summary>
        public static void ClearCache()
        {
            lock (s_gate)
            {
                s_cache.Clear();
            }
        }
    }
}
