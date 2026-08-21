using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 触发器计划目录加载器实现
    /// 支持多文件分离的触发器配置加载
    /// </summary>
    public sealed class TriggerPlanDirectoryLoader : ITriggerPlanDirectoryLoader, ITriggerPlanFileEnumerator
    {
        private readonly TriggerPlanJsonDatabase.ITextLoader _textLoader;

        /// <summary>
        /// 创建一个新的目录加载器
        /// </summary>
        /// <param name="textLoader">文本加载器（用于从 Resources 或文件系统加载）</param>
        public TriggerPlanDirectoryLoader(TriggerPlanJsonDatabase.ITextLoader textLoader)
        {
            _textLoader = textLoader ?? throw new ArgumentNullException(nameof(textLoader));
        }

        /// <inheritdoc />
        public TriggerPlanJsonDatabase LoadDirectory(string directory, string pattern = "*.json")
        {
            return LoadDirectory(directory, pattern, null);
        }

        /// <inheritdoc />
        public TriggerPlanJsonDatabase LoadDirectory(string directory, string pattern, TriggerPlanDirectoryLoadOptions options)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory cannot be null or empty", nameof(directory));

            var files = GetFiles(directory, pattern).ToArray();
            return LoadFiles(files, options);
        }

        /// <inheritdoc />
        public TriggerPlanJsonDatabase LoadDirectories(IEnumerable<string> directories, string pattern = "*.json")
        {
            return LoadDirectories(directories, pattern, null);
        }

        /// <inheritdoc />
        public TriggerPlanJsonDatabase LoadDirectories(IEnumerable<string> directories, string pattern, TriggerPlanDirectoryLoadOptions options)
        {
            if (directories == null)
                throw new ArgumentNullException(nameof(directories));

            var allFiles = new List<string>();
            foreach (var dir in directories)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                allFiles.AddRange(GetFiles(dir, pattern));
            }

            return LoadFiles(allFiles, options);
        }

        /// <inheritdoc />
        public TriggerPlanJsonDatabase LoadWithManifest(string manifestPath, string moduleDirectory)
        {
            return LoadWithManifest(manifestPath, moduleDirectory, null);
        }

        /// <inheritdoc />
        public TriggerPlanJsonDatabase LoadWithManifest(string manifestPath, string moduleDirectory, TriggerPlanDirectoryLoadOptions options)
        {
            if (string.IsNullOrEmpty(manifestPath))
                throw new ArgumentException("manifestPath cannot be null or empty", nameof(manifestPath));
            if (string.IsNullOrEmpty(moduleDirectory))
                throw new ArgumentException("moduleDirectory cannot be null or empty", nameof(moduleDirectory));

            if (!_textLoader.TryLoad(manifestPath, out var manifestContent) || string.IsNullOrEmpty(manifestContent))
            {
                throw new InvalidOperationException($"Manifest file not found or empty: {manifestPath}");
            }

            var manifest = JsonConvert.DeserializeObject<TriggerPlanManifest>(manifestContent);
            if (manifest?.Entries == null || manifest.Entries.Count == 0)
            {
                return new TriggerPlanJsonDatabase();
            }

            var allFiles = new List<string>();
            foreach (var entry in manifest.Entries)
            {
                if (string.IsNullOrEmpty(entry.Path)) continue;
                var fullPath = NormalizePath(moduleDirectory, entry.Path);
                allFiles.Add(fullPath);
            }

            return LoadFiles(allFiles, options);
        }

        /// <inheritdoc />
        public IEnumerable<string> GetFiles(string directory, string pattern)
        {
            if (string.IsNullOrEmpty(directory))
                return Enumerable.Empty<string>();

            if (_textLoader is IFileSystemTextLoader fsLoader)
            {
                return fsLoader.GetFiles(directory, pattern);
            }

#if UNITY_EDITOR || (!UNITY_IOS && !UNITY_ANDROID)
            try
            {
                if (Directory.Exists(directory))
                {
                    return Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch
            {
            }
#endif

            return Enumerable.Empty<string>();
        }

        /// <inheritdoc />
        public bool TryReadFile(string path, out string content)
        {
            return _textLoader.TryLoad(path, out content);
        }

        private TriggerPlanJsonDatabase LoadFiles(IEnumerable<string> files, TriggerPlanDirectoryLoadOptions options)
        {
            options = options ?? TriggerPlanDirectoryLoadOptions.Default;
            var parseOptions = options.ToParseOptions();
            var parser = new TriggerPlanJsonParser();
            var db = new TriggerPlanJsonDatabase { CueFactory = options.CueFactory };
            var mergedDto = new TriggerPlanJsonDatabase.TriggerPlanDatabaseDto
            {
                FormatVersion = 1,
                Triggers = new List<TriggerPlanJsonDatabase.TriggerPlanDto>(),
                Strings = new Dictionary<int, string>(),
                Blackboards = new List<AbilityKit.Triggering.Blackboard.BlackboardInitializationPlan>()
            };

            foreach (var file in files)
            {
                if (!_textLoader.TryLoad(file, out var content) || string.IsNullOrEmpty(content))
                {
                    HandleFileLoadFailure(
                        options,
                        file,
                        "Trigger plan file is empty or missing",
                        null);
                    continue;
                }

                try
                {
                    var parseResult = parser.Parse(content, file, parseOptions);
                    options.AddDiagnostics(parseResult.Diagnostics);
                    if (!parseResult.Success || parseResult.Dto == null)
                    {
                        var error = parseResult.FirstError;
                        var message = string.IsNullOrEmpty(error.Message)
                            ? "Unknown trigger plan json parse error"
                            : error.Message;
                        HandleFileLoadFailure(options, file, message, error.Exception);
                        continue;
                    }

                    var runtimeDto = parseResult.Dto;
                    if (runtimeDto?.Triggers != null)
                    {
                        mergedDto.Triggers.AddRange(runtimeDto.Triggers);
                    }

                    if (runtimeDto?.Strings != null)
                    {
                        foreach (var kvp in runtimeDto.Strings)
                        {
                            mergedDto.Strings[kvp.Key] = kvp.Value;
                        }
                    }

                    if (runtimeDto?.Blackboards != null)
                    {
                        mergedDto.Blackboards.AddRange(runtimeDto.Blackboards);
                    }
                }
                catch (Exception ex)
                {
                    HandleFileLoadFailure(options, file, ex.Message, ex);
                }
            }

            db.LoadFromDto(mergedDto);
            return db;
        }

        private static void HandleFileLoadFailure(
            TriggerPlanDirectoryLoadOptions options,
            string file,
            string message,
            Exception exception)
        {
            var diagnostic = new TriggerPlanJsonDiagnostic(
                TriggerPlanJsonDiagnosticSeverity.Error,
                message,
                file,
                exception: exception);
            options.AddDiagnostic(diagnostic);

            var formattedMessage = $"Failed to load trigger plan file {file}: {message}";
            if (options.ThrowOnFileParseError)
            {
                throw new InvalidOperationException(formattedMessage, exception);
            }

            LogWarning($"[TriggerPlanDirectoryLoader] {formattedMessage}");
        }

        private static string NormalizePath(string baseDir, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return baseDir;
            return Path.Combine(baseDir, relativePath).Replace('\\', '/');
        }

        private static void LogWarning(string message)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.LogWarning(message);
#else
            Console.Error.WriteLine(message);
#endif
        }

        #region JSON DTOs

#pragma warning disable 0649 // DTO fields are populated by JSON deserialization.
        private class TriggerPlanManifest
        {
            [JsonProperty("entries")]
            public List<ManifestEntry> Entries;
        }

        private class ManifestEntry
        {
            [JsonProperty("trigger_id")]
            public int TriggerId;

            [JsonProperty("path")]
            public string Path;
        }
#pragma warning restore 0649

        #endregion
    }

    /// <summary>
    /// 文件系统文本加载器接口
    /// </summary>
    public interface IFileSystemTextLoader : TriggerPlanJsonDatabase.ITextLoader
    {
        /// <summary>
        /// 获取目录下匹配的文件
        /// </summary>
        IEnumerable<string> GetFiles(string directory, string pattern);
    }
}
