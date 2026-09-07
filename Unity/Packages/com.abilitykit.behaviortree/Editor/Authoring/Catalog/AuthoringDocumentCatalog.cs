#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using UnityEditor;
using UnityEngine;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>为观察工具提供 authoring 文档；项目可注册自己的资源来源。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "IBtAuthoringDocumentProvider")]
    public interface IAuthoringDocumentProvider
    {
        IEnumerable<AuthoringSourceDocument> LoadDocuments();
    }

    /// <summary>同一 TreeId 存在多个 authoring 文档时的来源优先级。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringDocumentPriority")]
    public static class AuthoringDocumentPriority
    {
        public const int AssetMirror = 0;
        public const int HeadlessAuthoritativeSource = 100;
        public const int ProjectOverride = 200;
    }

    /// <summary>
    /// 编辑器 authoring 文档目录。内置支持 AssetDatabase 与 tools/bt-export 清单，
    /// 窗口只消费统一结果，不依赖具体项目的资产组织方式。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringDocumentCatalog")]
    public static class AuthoringDocumentCatalog
    {
        private static readonly List<ProviderEntry> Providers = new();
        private static readonly IAuthoringDocumentProvider AssetProvider = new AssetDatabaseProvider();
        private static readonly IAuthoringDocumentProvider ManifestProvider = new HeadlessManifestProvider();
        private static long _nextRegistrationId;

        public static IDisposable RegisterProvider(
            IAuthoringDocumentProvider provider,
            int priority = AuthoringDocumentPriority.ProjectOverride)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var entry = new ProviderEntry(++_nextRegistrationId, priority, provider);
            Providers.Add(entry);
            return new ProviderRegistration(entry);
        }

        public static AuthoringSourceDocument BuildObservationDocument(
            TreeDebugView view,
            NodeRegistry registry)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var definition = view.TreeDefinition;
            var documents = LoadAll();
            foreach (var authored in documents)
            {
                if (!string.Equals(authored.Tree.TreeId, definition.TreeId, StringComparison.Ordinal)
                    || authored.Tree.ComputeDefinitionHash() != definition.ComputeDefinitionHash()) continue;
                var exact = Clone(authored);
                exact.Tree = definition.DeepClone();
                return exact;
            }

            var observation = TreeExporter.Import(definition, registry);
            var byTreeId = IndexByTreeId(documents);
            ApplySourceMetadata(view, observation, byTreeId);
            ApplyAutomaticLayout(observation);
            return observation;
        }

        private static List<AuthoringSourceDocument> LoadAll()
        {
            var result = new List<AuthoringSourceDocument>();
            foreach (var provider in EnumerateProviders())
            {
                IEnumerable<AuthoringSourceDocument> documents;
                try
                {
                    documents = provider.LoadDocuments() ?? Array.Empty<AuthoringSourceDocument>();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BtAuthoring] 文档 provider '{provider.GetType().Name}' 读取失败: {ex.Message}");
                    continue;
                }

                try
                {
                    foreach (var document in documents)
                    {
                        if (document?.Tree != null) result.Add(document);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BtAuthoring] 文档 provider '{provider.GetType().Name}' 枚举失败: {ex.Message}");
                }
            }
            return result;
        }

        private static IEnumerable<IAuthoringDocumentProvider> EnumerateProviders()
        {
            var entries = new List<ProviderEntry>(Providers)
            {
                new ProviderEntry(long.MinValue + 1, AuthoringDocumentPriority.HeadlessAuthoritativeSource, ManifestProvider),
                new ProviderEntry(long.MinValue, AuthoringDocumentPriority.AssetMirror, AssetProvider),
            };
            entries.Sort(static (left, right) =>
            {
                var priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : right.RegistrationId.CompareTo(left.RegistrationId);
            });
            foreach (var entry in entries) yield return entry.Provider;
        }

        private static Dictionary<string, AuthoringSourceDocument> IndexByTreeId(
            IEnumerable<AuthoringSourceDocument> documents)
        {
            var result = new Dictionary<string, AuthoringSourceDocument>(StringComparer.Ordinal);
            foreach (var document in documents)
            {
                var treeId = document.Tree.TreeId;
                if (!string.IsNullOrWhiteSpace(treeId) && !result.ContainsKey(treeId)) result.Add(treeId, document);
            }
            return result;
        }

        private static void ApplySourceMetadata(
            TreeDebugView view,
            AuthoringSourceDocument observation,
            IReadOnlyDictionary<string, AuthoringSourceDocument> byTreeId)
        {
            observation.NodeMetadata.Clear();
            foreach (var node in observation.Tree.Nodes)
            {
                var sourceTreeId = view.TreeId;
                if (view.NodeSourceTree != null
                    && view.NodeSourceTree.TryGetValue(node.Id, out var mappedTreeId))
                {
                    sourceTreeId = mappedTreeId;
                }

                var sourceNodeId = node.Id;
                if (view.NodeSourceNode != null
                    && view.NodeSourceNode.TryGetValue(node.Id, out var mappedNodeId))
                {
                    sourceNodeId = mappedNodeId;
                }

                if (!byTreeId.TryGetValue(sourceTreeId, out var source)
                    || !source.TryGetNodeMetadata(sourceNodeId, out var metadata)) continue;
                observation.NodeMetadata.Add(new AuthoringNodeMetadata
                {
                    NodeId = node.Id,
                    DisplayName = metadata.DisplayName,
                    Comment = metadata.Comment,
                });
            }
        }

        private static void ApplyAutomaticLayout(AuthoringSourceDocument document)
        {
            // Import creates placeholder positions. Observation mode needs the same top-down
            // layout used by the authoring graph because its ports have the same orientation.
            document.Layout.Clear();
            AuthoringLayoutUtility.EnsureLayout(document);
        }

        private static AuthoringSourceDocument Clone(AuthoringSourceDocument document)
            => AuthoringJson.Load(AuthoringJson.Save(document));

        private sealed class AssetDatabaseProvider : IAuthoringDocumentProvider
        {
            public IEnumerable<AuthoringSourceDocument> LoadDocuments()
            {
                var guids = AssetDatabase.FindAssets("t:AuthoringAsset");
                Array.Sort(guids, StringComparer.Ordinal);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<AuthoringAsset>(path);
                    if (asset == null) continue;
                    AuthoringSourceDocument document;
                    try
                    {
                        document = asset.LoadDocument();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[BtAuthoring] 跳过无法读取的资产 '{path}': {ex.Message}");
                        continue;
                    }
                    yield return document;
                }
            }
        }

        private sealed class HeadlessManifestProvider : IAuthoringDocumentProvider
        {
            public IEnumerable<AuthoringSourceDocument> LoadDocuments()
            {
                var repositoryRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(repositoryRoot)) yield break;
                var manifestRoot = Path.Combine(repositoryRoot, "tools", "bt-export");
                if (!Directory.Exists(manifestRoot)) yield break;

                var manifestPaths = new List<string>(
                    Directory.EnumerateFiles(manifestRoot, "*.json", SearchOption.TopDirectoryOnly));
                manifestPaths.Sort(StringComparer.Ordinal);
                foreach (var manifestPath in manifestPaths)
                {
                    ProjectManifest manifest;
                    try
                    {
                        manifest = AuthoringJson.LoadProjectManifest(File.ReadAllText(manifestPath));
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (manifest.SourceKind != SourceKind.AuthoringDocument
                        || manifest.Trees.Count == 0
                        || string.IsNullOrWhiteSpace(manifest.SourceDirectory)) continue;

                    var sourceDirectory = Path.IsPathRooted(manifest.SourceDirectory)
                        ? manifest.SourceDirectory
                        : Path.Combine(repositoryRoot, manifest.SourceDirectory);
                    foreach (var treeId in manifest.Trees)
                    {
                        var sourcePath = Path.Combine(sourceDirectory, treeId + ".json");
                        if (!File.Exists(sourcePath)) continue;
                        AuthoringSourceDocument document;
                        try
                        {
                            document = AuthoringJson.Load(File.ReadAllText(sourcePath));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[BtAuthoring] 跳过无法读取的 headless 源 '{sourcePath}': {ex.Message}");
                            continue;
                        }
                        yield return document;
                    }
                }
            }
        }

        private sealed class ProviderRegistration : IDisposable
        {
            private ProviderEntry? _entry;

            public ProviderRegistration(ProviderEntry entry) => _entry = entry;

            public void Dispose()
            {
                if (_entry == null) return;
                Providers.Remove(_entry);
                _entry = null;
            }
        }

        private sealed class ProviderEntry
        {
            public long RegistrationId { get; }
            public int Priority { get; }
            public IAuthoringDocumentProvider Provider { get; }

            public ProviderEntry(long registrationId, int priority, IAuthoringDocumentProvider provider)
            {
                RegistrationId = registrationId;
                Priority = priority;
                Provider = provider;
            }
        }
    }
}
