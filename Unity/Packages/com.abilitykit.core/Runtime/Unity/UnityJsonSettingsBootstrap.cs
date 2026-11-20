using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AbilityKit.Core.Configuration
{
    /// <summary>
    /// 保留旧应用设置的 Unity 路径、首次复制与持久层读写入口。
    /// </summary>
    /// <remarks>
    /// 已弃用，仅兼容到下一主版本；新的设置引导应归应用所有者包。
    /// 不保证线程安全、路径约束、原子保存或失败回滚；Unity 调用应由宿主在允许的执行上下文中推进。
    /// 文件名仅在空引用时使用默认值，空字符串和空白原样参与路径拼接；不校验相对路径、越界或扩展名。
    /// </remarks>
    [Obsolete("Application settings bootstrap no longer belongs in Core.Unity. Move it to the owning application package; this compatibility API will be removed in the next major version.")]
    public static class UnityJsonSettingsBootstrap
    {
        /// <summary>
        /// 文件名参数为空引用时使用的历史默认名称。
        /// </summary>
        public const string DefaultFileName = "abilitykit.settings.json";

        /// <summary>
        /// 在目标文件不存在时，尝试将 StreamingAssets 文本复制到持久路径。
        /// </summary>
        /// <param name="fileName">参与平台路径拼接的文件名，仅空引用采用默认名称。</param>
        /// <returns>延迟执行的枚举器，须由 Unity 协程宿主推进并等待请求。</returns>
        /// <remarks>
        /// 持久根路径为空、目标已存在、请求返回失败或文本为空时静默结束；不验证已有文件或 JSON 内容。
        /// 路径不含协议分隔符时只添加字面的 file:// 前缀，不进行 URI 转义或标准化。
        /// 仅最终目录创建与文件写入隔离异常；路径拼接、请求创建、发送、结果读取与释放异常可能传播。
        /// 写入直接覆盖，无二次存在检查、原子替换或回滚，并发调用可能覆盖下载期间新建的目标。
        /// 请求跨等待点持有，在枚举器正常退出或被释放时通过 using 释放；宿主停止行为需按实际 Unity 验证。
        /// </remarks>
        public static IEnumerator EnsurePersistentCopy(string fileName = null)
        {
            fileName ??= DefaultFileName;

            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) yield break;

            var persistentPath = Path.Combine(persistentBase, fileName);
            if (File.Exists(persistentPath)) yield break;

            var streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
            var url = streamingPath;
            if (!string.IsNullOrEmpty(url) && !url.Contains("://"))
            {
                url = "file://" + url;
            }

            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            var json = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (string.IsNullOrEmpty(json)) yield break;

            /*
             * 此处只隔离落盘阶段失败，不为前面的路径和请求操作提供总兜底。
             * 兼容实现直接写入，不回滚已创建目录或可能发生的文件变更。
             */
            try
            {
                var dir = Path.GetDirectoryName(persistentPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(persistentPath, json);
            }
            catch
            {
                yield break;
            }
        }

        /// <summary>
        /// 等待首次复制尝试后读取持久文件，并整层替换目标存储的持久设置。
        /// </summary>
        /// <param name="store">目标存储；空引用使枚举器直接结束。</param>
        /// <param name="deserialize">文本解释回调；空引用仍先尝试复制，随后以空设置替换持久层。</param>
        /// <param name="fileName">参与平台路径拼接的文件名，仅空引用采用默认名称。</param>
        /// <returns>延迟执行的枚举器，首个等待项为复制操作的嵌套枚举器。</returns>
        /// <remarks>
        /// 需要 Unity 协程宿主推进嵌套枚举器；仅对外层调用 MoveNext 不会自动执行复制。
        /// 复制静默失败后仍读取目标文件；复制和读取分别取得持久根路径，不构成路径快照。
        /// 持久根路径为空时保留原层；文件工具返回空设置时清空原持久层，基础层与覆盖层保持。
        /// 替换后同步发送空键变更通知，即使新层为空也通知；通知异常传播，已提交设置不回滚。
        /// 文件读取与解释的隔离遵循 JsonSettingsFiles，外层路径拼接等异常仍可传播。
        /// </remarks>
        public static IEnumerator LoadPersistentInto(LayeredJsonSettingsStore store, Func<string, Dictionary<string, object>> deserialize, string fileName = null)
        {
            if (store == null) yield break;

            fileName ??= DefaultFileName;

            yield return EnsurePersistentCopy(fileName);

            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) yield break;
            var persistentPath = Path.Combine(persistentBase, fileName);
            var settings = JsonSettingsFiles.LoadFlatOrEmpty(persistentPath, deserialize);
            store.ReplacePersistent(settings);
        }

        /// <summary>
        /// 同步读取已有持久文件并整层替换持久设置，不执行 StreamingAssets 复制。
        /// </summary>
        /// <param name="store">目标存储；空引用直接返回。</param>
        /// <param name="deserialize">文本解释回调；空引用直接返回并保留旧层，与协程入口不同。</param>
        /// <param name="fileName">参与平台路径拼接的文件名，仅空引用采用默认名称。</param>
        /// <remarks>
        /// 持久根路径为空时无动作；读取或解释失败返回空设置时仍清空旧持久层并同步发送空键通知。
        /// 基础层与覆盖层保持；外层路径拼接和通知异常可能传播，通知失败不回滚已提交设置。
        /// 同步文件访问占用调用线程，不发送 UnityWebRequest。
        /// </remarks>
        public static void LoadPersistentIntoSync(LayeredJsonSettingsStore store, Func<string, Dictionary<string, object>> deserialize, string fileName = null)
        {
            if (store == null) return;
            if (deserialize == null) return;

            fileName ??= DefaultFileName;

            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) return;

            var persistentPath = Path.Combine(persistentBase, fileName);
            var settings = JsonSettingsFiles.LoadFlatOrEmpty(persistentPath, deserialize);
            store.ReplacePersistent(settings);
        }

        /// <summary>
        /// 将覆盖值序列化后直接写入持久文件，不更新存储或缓存，也不发送设置通知。
        /// </summary>
        /// <param name="overrides">原样交给回调的覆盖值；空引用由文件工具替换为新的空字典。</param>
        /// <param name="serialize">文本生成回调；空引用返回假，返回空引用则写入空文本。</param>
        /// <param name="fileName">参与平台路径拼接的文件名，仅空引用采用默认名称。</param>
        /// <returns>文件工具完成写入时为真；回调为空、持久根路径为空或工具捕获失败时为假。</returns>
        /// <remarks>
        /// 路径拼接在文件工具的异常隔离之外，Try 前缀不保证全部异常转换为假。
        /// 创建目录、回调及直接覆盖写入遵循 JsonSettingsFiles；失败不回滚其副作用，无原子保存保证。
        /// </remarks>
        public static bool TrySaveOverridesToPersistent(IReadOnlyDictionary<string, object> overrides, Func<IReadOnlyDictionary<string, object>, string> serialize, string fileName = null)
        {
            if (serialize == null) return false;
            fileName ??= DefaultFileName;
            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) return false;
            var persistentPath = Path.Combine(persistentBase, fileName);
            return JsonSettingsFiles.TrySaveOverrides(persistentPath, overrides, serialize);
        }
    }
}
