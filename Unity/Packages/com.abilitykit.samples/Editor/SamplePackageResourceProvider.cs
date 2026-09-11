#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Samples.Abstractions;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Samples.Editor
{
    /// <summary>
    /// Unity 宿主的资源提供器。
    ///
    /// 示例内容只依赖 <see cref="IResourceProvider"/>：.NET 宿主读文件系统，Unity 宿主读
    /// 包内资源。示例代码拼出来的是宿主形状的路径（<c>AppContext.BaseDirectory</c> + 文件名），
    /// 在 Unity 下没有意义，所以这里按**文件名**在包内查找，而不是按目录解析。
    ///
    /// 找不到时返回 false 而不抛异常，示例会走各自的回退分支（例如 HFSM 示例退回内联配置）。
    /// </summary>
    internal sealed class SamplePackageResourceProvider : IResourceProvider
    {
        internal const string PackageRoot = "Packages/com.abilitykit.samples";

        private readonly Dictionary<string, string> _assetPathByFileName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal SamplePackageResourceProvider()
        {
            Rebuild();
        }

        /// <summary>
        /// 建立"文件名 → 资源路径"索引。宿主重建目录后需要重新扫描。
        /// </summary>
        internal void Rebuild()
        {
            _assetPathByFileName.Clear();

            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { PackageRoot });
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(assetPath);
                if (!_assetPathByFileName.ContainsKey(fileName))
                {
                    _assetPathByFileName.Add(fileName, assetPath);
                }
            }
        }

        public string LoadText(string path)
        {
            if (!TryLoadText(path, out var content))
            {
                throw new FileNotFoundException(
                    "Sample resource not found in " + PackageRoot + ": " + path);
            }

            return content;
        }

        public bool TryLoadText(string path, out string content)
        {
            content = string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (!_assetPathByFileName.TryGetValue(Path.GetFileName(path), out var assetPath))
            {
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                return false;
            }

            content = asset.text;
            return true;
        }

        public bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   _assetPathByFileName.ContainsKey(Path.GetFileName(path));
        }
    }
}
