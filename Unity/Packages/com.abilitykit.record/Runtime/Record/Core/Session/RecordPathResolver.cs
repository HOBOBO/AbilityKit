#nullable enable annotations

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("AbilityKit.Record.Unity")]
[assembly: InternalsVisibleTo("AbilityKit.Record.Tests")]

namespace AbilityKit.Core.Recording
{
    /// <summary>
    /// 统一处理录制文件路径，只对相对路径请求平台基目录。
    /// </summary>
    internal static class RecordPathResolver
    {
        private static Func<bool, string>? s_baseDirectory;

        internal static void SetBaseDirectoryProvider(Func<bool, string>? provider) =>
            Volatile.Write(ref s_baseDirectory, provider);

        [return: NotNullIfNotNull("path")]
        internal static string? Resolve(string? path, bool persistentDataOnly = false)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path)) return path;

            var provider = Volatile.Read(ref s_baseDirectory);
            var baseDirectory = provider == null ? Environment.CurrentDirectory : provider(persistentDataOnly);
            return Path.Combine(baseDirectory, path);
        }
    }
}
