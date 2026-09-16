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
using AbilityKit.Deterministic;
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
        public void InitialOverrides_ApplyOnlyToPreviewAndRejectUnknownOrWrongType()
        {
            var document = Document("override-preview", "root", BuiltInNodeTypes.Succeed);
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "score", Type = AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                Default = PropertyValue.Of(3L),
            });
            var overrides = new Dictionary<string, PropertyValue> { ["score"] = PropertyValue.Of(9L) };

            Assert.That(TreePreviewSession.TryStart(document, Registry(), null, "override-preview",
                overrides, out var session, out var error), Is.True, error);
            using (var active = session!)
            {
                Assert.That(active.Runtime.Blackboard.GetInt64("score"), Is.EqualTo(9L));
                Assert.That(active.InitialBlackboard.TryGetInt64("score", out var initial), Is.True);
                Assert.That(initial, Is.EqualTo(9L));
                active.Runtime.Blackboard.SetInt64("score", 17L);
                Assert.That(active.InitialBlackboard.TryGetInt64("score", out initial), Is.True);
                Assert.That(initial, Is.EqualTo(9L));
            }
            Assert.That(document.Tree.Blackboard.Keys[0].Default!.Int64Value, Is.EqualTo(3L));

            foreach (var invalid in new[]
            {
                new Dictionary<string, PropertyValue> { ["other"] = PropertyValue.Of(9L) },
                new Dictionary<string, PropertyValue> { ["score"] = PropertyValue.Of(true) },
            })
            {
                Assert.That(TreePreviewSession.TryStart(document, Registry(), null, "invalid-preview",
                    invalid, out var rejected, out error), Is.False);
                Assert.That(rejected, Is.Null);
                Assert.That(error, Does.Contain("黑板初始值无效"));
            }
        }

        [Test]
        public void PauseStepAndReset_RestoreFrameBlackboardAndInitialSnapshot()
        {
            var document = Document("control-preview", "root", BuiltInNodeTypes.Succeed);
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "score", Type = AbilityKit.BehaviorTree.Definition.ValueType.Int64,
                Default = PropertyValue.Of(3L),
            });
            Assert.That(TreePreviewSession.TryStart(document, Registry(), null, "control-preview",
                new Dictionary<string, PropertyValue> { ["score"] = PropertyValue.Of(9L) },
                out var session, out var error), Is.True, error);
            using (var active = session!)
            {
                active.Pause();
                active.Tick();
                Assert.That(active.Frame, Is.Zero);
                Assert.That(active.Step(), Is.True);
                Assert.That(active.Frame, Is.EqualTo(1));
                active.Tick();
                Assert.That(active.Frame, Is.EqualTo(1));
                active.Runtime.Blackboard.SetInt64("score", 42L);
                Assert.That(active.TryReset(out error), Is.True, error);
                Assert.That(active.IsPaused, Is.True);
                Assert.That(active.Frame, Is.Zero);
                Assert.That(active.Runtime.Blackboard.GetInt64("score"), Is.EqualTo(9L));
                Assert.That(active.InitialBlackboard.TryGetInt64("score", out var initial), Is.True);
                Assert.That(initial, Is.EqualTo(9L));
                Assert.That(active.Step(), Is.True);
                Assert.That(active.Frame, Is.EqualTo(1));
                active.Resume();
                Assert.That(active.Step(), Is.False);
                active.Tick();
                Assert.That(active.Frame, Is.EqualTo(2));
            }
        }

        [Test]
        public void FaultedPreview_ResetAllowsAnotherStep()
        {
            var registry = Registry();
            registry.Register(new NodeDescriptor(ThrowingTickNode.TypeId, "抛出异常", "测试",
                NodeKind.Action, 0, 0, () => new ThrowingTickNode()));
            Assert.That(TreePreviewSession.TryStart(Document("reset-fault", "root", ThrowingTickNode.TypeId),
                registry, "reset-fault", out var session, out var error), Is.True, error);
            using (var active = session!)
            {
                active.Pause();
                Assert.That(active.Step(), Is.False);
                Assert.That(active.IsFaulted, Is.True);
                Assert.That(active.TryReset(out error), Is.True, error);
                Assert.That(active.IsFaulted, Is.False);
                Assert.That(active.Runtime.IsEnabled, Is.True);
                Assert.That(active.Frame, Is.Zero);
                Assert.That(active.Step(), Is.False);
                Assert.That(active.Frame, Is.EqualTo(1));
            }
        }

        [Test]
        public void Reset_RewindsDeterministicRandomStream()
        {
            var document = Document("random-preview", "root", BuiltInNodeTypes.Probability);
            Assert.That(TreePreviewSession.TryStart(document, Registry(), "random-preview",
                out var session, out var error), Is.True, error);
            using (var active = session!)
            {
                active.Pause();
                var initial = active.Runtime.CaptureState().Nodes[0].RandomSequence;
                Assert.That(active.Step(), Is.True);
                var afterStep = active.Runtime.CaptureState().Nodes[0].RandomSequence;
                Assert.That(afterStep, Is.GreaterThan(initial));
                Assert.That(active.TryReset(out error), Is.True, error);
                Assert.That(active.Runtime.CaptureState().Nodes[0].RandomSequence, Is.EqualTo(initial));
                Assert.That(active.Step(), Is.True);
                Assert.That(active.Runtime.CaptureState().Nodes[0].RandomSequence, Is.EqualTo(afterStep));
            }
        }

        [Test]
        public void PreviewSetup_Fixed64InputPreservesPrecisionAndBlocksInvalidValue()
        {
            var owner = ScriptableObject.CreateInstance<AuthoringGraphWindow>();
            PreviewBlackboardSetupWindow? setup = null;
            try
            {
                var schema = new BlackboardSchema();
                schema.Keys.Add(new BlackboardKeyDefinition
                {
                    Name = "distance",
                    Type = AbilityKit.BehaviorTree.Definition.ValueType.Fixed64,
                    Default = PropertyValue.Of(Fixed64.FromRaw(1L)),
                });
                PreviewBlackboardSetupWindow.Open(owner, schema, _ => { });
                setup = Resources.FindObjectsOfTypeAll<PreviewBlackboardSetupWindow>().Last();
                var field = setup.rootVisualElement.Q<TextField>();
                var toggle = setup.rootVisualElement.Q<Toggle>();
                var start = setup.rootVisualElement.Query<Button>().ToList()
                    .Single(button => button.text == "开始预览");

                Assert.That(field, Is.Not.Null);
                Assert.That(toggle, Is.Not.Null);
                Assert.That(field!.value, Is.EqualTo(Fixed64.FromRaw(1L).ToString()));
                toggle!.value = true;
                field.value = "not-a-number";
                Assert.That(start.enabledSelf, Is.False);
                field.value = "0.5";
                Assert.That(start.enabledSelf, Is.True);
                toggle.value = false;
                Assert.That(field.value, Is.EqualTo(Fixed64.FromRaw(1L).ToString()));
            }
            finally
            {
                if (setup != null) UnityEngine.Object.DestroyImmediate(setup);
                UnityEngine.Object.DestroyImmediate(owner);
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
                Assert.That(preview.titleContent.text, Is.EqualTo("行为树预览"));
                var step = preview.rootVisualElement.Q<Button>(AuthoringGraphWindow.PreviewStepButtonName);
                var reset = preview.rootVisualElement.Q<Button>(AuthoringGraphWindow.PreviewResetButtonName);
                Assert.That(step, Is.Not.Null);
                Assert.That(reset, Is.Not.Null);
                Assert.That(step!.enabledSelf, Is.False);
                Assert.That(reset!.enabledSelf, Is.True);
                Assert.That(((IAuthoringInspectorHost)preview).InitialRuntimeBlackboard, Is.Not.Null);
                Assert.That(((IAuthoringInspectorHost)preview).InitialRuntimeBlackboard!.Count, Is.Zero);
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
