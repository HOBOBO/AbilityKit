#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class TreePreviewSessionTests
    {
        [SetUp]
        public void SetUp() => DebugRegistry.ClearForTests();

        [TearDown]
        public void TearDown() => DebugRegistry.ClearForTests();

        [Test]
        public void TryStart_ExpandsSubtreesBeforePreviewBegins()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var child = Document("child-" + suffix, "leaf", BuiltInNodeTypes.Succeed);
            var parent = new AuthoringSourceDocument();
            parent.Tree.TreeId = "parent-" + suffix;
            parent.Tree.RootNodeId = "subtree";
            var subtree = new NodeDefinition { Id = "subtree", Type = BuiltInNodeTypes.Subtree };
            subtree.Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of(child.Tree.TreeId));
            parent.Tree.Nodes.Add(subtree);

            var registry = Registry();
            using var registration = AuthoringDocumentCatalog.RegisterProvider(new StaticProvider(child));
            var resolver = AuthoringDocumentCatalog.CreateTreeResolver(parent);

            Assert.That(TreePreviewSession.TryStart(
                parent,
                registry,
                resolver,
                "subtree-preview",
                out var session,
                out var error), Is.True, error);
            using (var activeSession = session!)
            {
                Assert.That(activeSession.Runtime.Definition.Nodes.Exists(
                    node => node.Type == BuiltInNodeTypes.Subtree), Is.False);
                Assert.That(activeSession.Runtime.NodeSourceTree, Is.Not.Null);
                Assert.That(activeSession.Runtime.NodeSourceTree!["subtree.leaf"], Is.EqualTo(child.Tree.TreeId));
            }
        }

        [Test]
        public void TryStart_MissingSubtreeFailsWithoutCreatingSession()
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "missing-parent-" + Guid.NewGuid().ToString("N");
            document.Tree.RootNodeId = "subtree";
            var subtree = new NodeDefinition { Id = "subtree", Type = BuiltInNodeTypes.Subtree };
            subtree.Properties.Set(
                SubtreeNode.TreeIdProperty,
                PropertyValue.Of("missing-child-" + Guid.NewGuid().ToString("N")));
            document.Tree.Nodes.Add(subtree);

            var started = TreePreviewSession.TryStart(
                document,
                Registry(),
                AuthoringDocumentCatalog.CreateTreeResolver(document),
                "missing-subtree-preview",
                out var session,
                out var error);

            Assert.That(started, Is.False);
            Assert.That(session, Is.Null);
            Assert.That(error, Does.Contain("不存在的行为树"));
        }

        [Test]
        public void EditorDiagnostics_MissingSubtreeReportsBuildCodeAndLocatesReferenceNode()
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "diagnostic-parent-" + Guid.NewGuid().ToString("N");
            document.Tree.RootNodeId = "subtree";
            var subtree = new NodeDefinition { Id = "subtree", Type = BuiltInNodeTypes.Subtree };
            subtree.Properties.Set(
                SubtreeNode.TreeIdProperty,
                PropertyValue.Of("missing-child-" + Guid.NewGuid().ToString("N")));
            document.Tree.Nodes.Add(subtree);
            string? located = null;

            var diagnostics = EditorDiagnostics.Analyze(
                document,
                Registry(),
                AuthoringDocumentCatalog.CreateTreeResolver(document),
                nodeId => located = nodeId);

            var diagnostic = diagnostics.Items.SingleOrDefault(item =>
                item.Message.Contains(BehaviorTreeBuildPipeline.ExpansionFailedCode));
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic!.Path, Is.EqualTo("nodes/subtree"));
            Assert.That(diagnostic.CanLocate, Is.True);
            diagnostic.Locate?.Invoke();
            Assert.That(located, Is.EqualTo("subtree"));
        }

        [Test]
        public void DirectExport_MissingSubtreeDoesNotOverwriteExistingArtifact()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assetDirectory = "Assets/__BtBuildPipeline_" + suffix;
            var runtimeDirectory = assetDirectory + "/Runtime";
            var assetPath = assetDirectory + "/parent.asset";
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(assetDirectory));
            var asset = ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                var document = new AuthoringSourceDocument();
                document.Tree.TreeId = "parent-" + suffix;
                document.Tree.RootNodeId = "subtree";
                var subtree = new NodeDefinition { Id = "subtree", Type = BuiltInNodeTypes.Subtree };
                subtree.Properties.Set(
                    SubtreeNode.TreeIdProperty,
                    PropertyValue.Of("missing-child-" + suffix));
                document.Tree.Nodes.Add(subtree);
                asset.SaveDocument(document);
                AssetDatabase.CreateAsset(asset, assetPath);

                var serialized = new SerializedObject(asset);
                serialized.FindProperty("_runtimeExportPath").stringValue = runtimeDirectory;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();

                var absoluteDirectory = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", runtimeDirectory));
                Directory.CreateDirectory(absoluteDirectory);
                var artifactPath = Path.Combine(absoluteDirectory, document.Tree.TreeId + ".json");
                File.WriteAllText(artifactPath, "old artifact");

                var report = AuthoringRuntimeExporter.Export(asset);

                Assert.That(report.Success, Is.False);
                Assert.That(report.Messages, Has.Some.Contains(BehaviorTreeBuildPipeline.ExpansionFailedCode));
                Assert.That(File.ReadAllText(artifactPath), Is.EqualTo("old artifact"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetDirectory);
                if (asset != null && !AssetDatabase.Contains(asset))
                    ScriptableObject.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Tick_RuntimeExceptionFaultsOnceAndDoesNotEscapeEditorUpdate()
        {
            var registry = Registry();
            registry.Register(new NodeDescriptor(
                ThrowingTickNode.TypeId,
                "抛出异常",
                "测试",
                NodeKind.Action,
                0,
                0,
                () => new ThrowingTickNode()));
            var document = Document("fault-preview", "root", ThrowingTickNode.TypeId);

            Assert.That(TreePreviewSession.TryStart(
                document,
                registry,
                "fault-preview",
                out var session,
                out var error), Is.True, error);
            using (var activeSession = session!)
            {
                var notifications = 0;
                activeSession.Faulted += _ => notifications++;

                Assert.DoesNotThrow(activeSession.Tick);
                Assert.That(activeSession.IsFaulted, Is.True);
                Assert.That(activeSession.FaultMessage, Does.Contain("preview tick failed"));
                Assert.That(activeSession.Runtime.IsFaulted, Is.True);
                Assert.That(notifications, Is.EqualTo(1));

                Assert.DoesNotThrow(activeSession.Tick);
                Assert.That(activeSession.Frame, Is.EqualTo(1));
                Assert.That(notifications, Is.EqualTo(1));
            }
        }

        [Test]
        public void PreviewWindow_DoesNotReplaceOwnersDirtyDocumentOrUndoHistory()
        {
            var owner = ScriptableObject.CreateInstance<AuthoringGraphWindow>();
            AuthoringGraphWindow? preview = null;
            TreePreviewSession? session = null;
            try
            {
                var edited = Document("edited-tree", "root", BuiltInNodeTypes.Succeed);
                owner.DocumentSession.Open(edited);
                var before = AuthoringJson.Save(edited);
                edited.Tree.Nodes[0].Type = BuiltInNodeTypes.Fail;
                Assert.That(owner.DocumentSession.RecordChange(before), Is.True);

                var originalSession = owner.DocumentSession;
                Assert.That(TreePreviewSession.TryStart(
                    edited,
                    Registry(),
                    AuthoringDocumentCatalog.CreateTreeResolver(edited),
                    "preserve-editor",
                    out session,
                    out var error), Is.True, error);

                preview = AuthoringGraphWindow.OpenPreview(session!, owner, edited);
                session = null;

                Assert.That(owner.DocumentSession, Is.SameAs(originalSession));
                Assert.That(owner.DocumentSession.Document.Tree.Nodes[0].Type, Is.EqualTo(BuiltInNodeTypes.Fail));
                Assert.That(owner.DocumentSession.IsDirty, Is.True);
                Assert.That(owner.DocumentSession.CanUndo, Is.True);
                Assert.That(preview.IsObservation, Is.True);
            }
            finally
            {
                session?.Dispose();
                if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PreviewWindow_ShowsFaultMessageAfterTickFailure()
        {
            var registry = Registry();
            registry.Register(new NodeDescriptor(
                ThrowingTickNode.TypeId,
                "抛出异常",
                "测试",
                NodeKind.Action,
                0,
                0,
                () => new ThrowingTickNode()));
            var document = Document("fault-window", "root", ThrowingTickNode.TypeId);
            var owner = ScriptableObject.CreateInstance<AuthoringGraphWindow>();
            AuthoringGraphWindow? preview = null;
            TreePreviewSession? session = null;
            try
            {
                Assert.That(TreePreviewSession.TryStart(
                    document,
                    registry,
                    "fault-window",
                    out session,
                    out var error), Is.True, error);
                var activeSession = session!;
                preview = AuthoringGraphWindow.OpenPreview(activeSession, owner, document);

                Assert.DoesNotThrow(activeSession.Tick);

                var faultLabel = preview.rootVisualElement.Q<Label>(AuthoringGraphWindow.PreviewFaultLabelName);
                Assert.That(faultLabel, Is.Not.Null);
                Assert.That(faultLabel!.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(faultLabel.text, Does.Contain("preview tick failed"));
                session = null;
            }
            finally
            {
                session?.Dispose();
                if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static NodeRegistry Registry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            return registry;
        }

        private static AuthoringSourceDocument Document(string treeId, string nodeId, string nodeType)
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = treeId;
            document.Tree.RootNodeId = nodeId;
            document.Tree.Nodes.Add(new NodeDefinition { Id = nodeId, Type = nodeType });
            return document;
        }

        private sealed class StaticProvider : IAuthoringDocumentProvider
        {
            private readonly AuthoringSourceDocument _document;

            public StaticProvider(AuthoringSourceDocument document)
            {
                _document = document;
            }

            public IEnumerable<AuthoringSourceDocument> LoadDocuments()
            {
                yield return _document;
            }
        }

        private sealed class ThrowingTickNode : NodeBase
        {
            public const string TypeId = "test.preview.throwing-tick";

            public override NodeState OnTick(ExecutionContext context)
                => throw new InvalidOperationException("preview tick failed");
        }
    }
}
#endif
