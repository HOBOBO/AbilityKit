#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Editor.Authoring.Workspace;
using AbilityKit.BehaviorTree.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    /// <summary>
    /// Golden 示例验收（与 dotnet 侧 BtAuthoringExportTests 同构）：授权 → 校验 → 导出契约
    /// 的 Unity EditMode 哨兵。任何一侧漂移都会在这里先红。
    /// </summary>
    public sealed class AuthoringGoldenExamplesTests
    {
        private static NodeRegistry BuiltinRegistry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            return registry;
        }

        [Test]
        public void GoldenExamples_ValidateCleanAndExport()
        {
            var registry = BuiltinRegistry();
            foreach (var document in AuthoringGoldenExamples.BuildAll())
            {
                var json = TreeExporter.Export(document, registry, out var errors);
                Assert.That(errors, Is.Empty);
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("golden.hero_combat"));
            }
        }

        [Test]
        public void GoldenExamples_ExportIsStable()
        {
            var registry = BuiltinRegistry();
            foreach (var document in AuthoringGoldenExamples.BuildAll())
            {
                var first = TreeExporter.Export(document, registry, out _);
                var second = TreeExporter.Export(document, registry, out _);
                Assert.That(first, Is.EqualTo(second));
            }
        }

        [Test]
        public void AuthoringJson_RoundtripsThroughAssetModel()
        {
            var document = AuthoringGoldenExamples.BuildHeroCombat();
            var json = AuthoringJson.Save(document);
            var loaded = AuthoringJson.Load(json);

            Assert.That(loaded.Layout, Has.Count.EqualTo(document.Layout.Count));
            Assert.That(loaded.Groups, Has.Count.EqualTo(document.Groups.Count));
            Assert.That(loaded.Notes, Has.Count.EqualTo(document.Notes.Count));
            Assert.That(
                loaded.Tree.ComputeDefinitionHash(),
                Is.EqualTo(document.Tree.ComputeDefinitionHash()));
        }

        [Test]
        public void ProjectAndDirectExport_ProduceIdenticalRuntimeBytesAndIncrementalUnchanged()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "AbilityKit.BtGolden." + Guid.NewGuid().ToString("N"));
            var sourceDirectory = Path.Combine(root, "source");
            var directDirectory = Path.Combine(root, "direct");
            var projectDirectory = Path.Combine(root, "project");
            Directory.CreateDirectory(sourceDirectory);

            try
            {
                var document = AuthoringGoldenExamples.BuildHeroCombat();
                var treeId = document.Tree.TreeId;
                File.WriteAllText(
                    Path.Combine(sourceDirectory, treeId + ".json"),
                    AuthoringJson.Save(document));

                var registry = BuiltinRegistry();
                var directReport = ExportPipeline.ExportAll(
                    new[] { new KeyValuePair<string, AuthoringSourceDocument>(treeId, document) },
                    new[] { directDirectory },
                    registry,
                    root);
                var manifest = new ProjectManifest
                {
                    SourceDirectory = sourceDirectory,
                    SourceKind = SourceKind.AuthoringDocument,
                    Trees = new List<string> { treeId },
                    ExportTargets = new List<string> { projectDirectory }
                };
                var projectReport = ExportPipeline.ExportProject(manifest, registry, root);

                Assert.That(directReport, Has.Count.EqualTo(1));
                Assert.That(projectReport, Has.Count.EqualTo(1));
                Assert.That(directReport[0].Status, Is.EqualTo(ExportStatus.Exported));
                Assert.That(projectReport[0].Status, Is.EqualTo(ExportStatus.Exported));
                Assert.That(
                    File.ReadAllBytes(Path.Combine(directDirectory, treeId + ".json")),
                    Is.EqualTo(File.ReadAllBytes(Path.Combine(projectDirectory, treeId + ".json"))));

                var repeated = ExportPipeline.ExportProject(manifest, registry, root);
                Assert.That(repeated[0].Status, Is.EqualTo(ExportStatus.Unchanged));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void ProjectWorkflow_CreatesEditsAndExportsRuntimeJsonFromProjectConfiguration()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var assetDirectory = "Assets/__BtWorkflow_" + suffix;
            var outputDirectory = Path.Combine(
                Path.GetTempPath(),
                "AbilityKit.BtWorkflow." + suffix);

            AssetDatabase.CreateFolder("Assets", Path.GetFileName(assetDirectory));
            Directory.CreateDirectory(outputDirectory);
            try
            {
                var project = ScriptableObject.CreateInstance<AuthoringProjectAsset>();
                project.TreeAssetDirectory = assetDirectory;
                project.ExportTargets.Clear();
                project.ExportTargets.Add(outputDirectory);
                AssetDatabase.CreateAsset(project, assetDirectory + "/Project.asset");

                const string treeId = "workflow_tree";
                var tree = AuthoringCreateWizard.CreateAsset(
                    project,
                    assetDirectory + "/" + treeId + ".asset",
                    treeId,
                    "Workflow Tree",
                    AuthoringTemplates.BuildEmpty);

                Assert.That(project.Trees, Has.Count.EqualTo(1));
                Assert.That(project.Trees[0], Is.SameAs(tree));

                var edited = tree.LoadDocument();
                edited.Tree.Nodes[0].Type = BuiltInNodeTypes.Fail;
                edited.Metadata.Description = "Edited in graph workflow";
                tree.SaveDocument(edited);
                AssetDatabase.SaveAssets();

                var report = project.ExportAll(AuthoringMenuUtility.RepositoryRoot);
                Assert.That(report, Has.Count.EqualTo(1));
                Assert.That(report[0].Status, Is.EqualTo(ExportStatus.Exported));

                var runtimePath = Path.Combine(outputDirectory, treeId + ".json");
                Assert.That(File.Exists(runtimePath), Is.True);
                var runtime = TreeJson.Load(File.ReadAllText(runtimePath));
                Assert.That(runtime.TreeId, Is.EqualTo(treeId));
                Assert.That(runtime.Nodes[0].Type, Is.EqualTo(BuiltInNodeTypes.Fail));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetDirectory);
                if (Directory.Exists(outputDirectory))
                    Directory.Delete(outputDirectory, recursive: true);
            }
        }

        [Test]
        public void RepositoryRootResolution_FindsGitRootAboveUnityAssets()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "AbilityKit.BtRepositoryRoot." + Guid.NewGuid().ToString("N"));
            var assets = Path.Combine(root, "Unity", "Assets");
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            Directory.CreateDirectory(assets);
            try
            {
                Assert.That(
                    AuthoringMenuUtility.ResolveRepositoryRoot(assets),
                    Is.EqualTo(Path.GetFullPath(root)));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void ProjectConfiguration_RejectsDuplicateIdsAndTargetsBeforeWriting()
        {
            var first = ScriptableObject.CreateInstance<AuthoringAsset>();
            var second = ScriptableObject.CreateInstance<AuthoringAsset>();
            var project = ScriptableObject.CreateInstance<AuthoringProjectAsset>();
            try
            {
                var firstDocument = AuthoringTemplates.BuildEmpty();
                firstDocument.Tree.TreeId = "duplicate";
                first.SaveDocument(firstDocument);
                var secondDocument = AuthoringTemplates.BuildEmpty();
                secondDocument.Tree.TreeId = "duplicate";
                second.SaveDocument(secondDocument);

                project.Register(first);
                project.Register(second);
                project.ExportTargets.Clear();
                project.ExportTargets.Add("Unity/Assets/Resources/bt");
                project.ExportTargets.Add("Unity\\Assets\\Resources\\bt");

                var errors = project.Validate();
                Assert.That(errors, Has.Some.Contains("TreeId 'duplicate'"));
                Assert.That(errors, Has.Some.Contains("导出目标 'Unity/Assets/Resources/bt'"));

                var report = project.ExportAll(AuthoringMenuUtility.RepositoryRoot);
                Assert.That(report, Is.Not.Empty);
                Assert.That(report, Has.All.Property("Status").EqualTo(ExportStatus.Error));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(project);
                ScriptableObject.DestroyImmediate(second);
                ScriptableObject.DestroyImmediate(first);
            }
        }

        [Test]
        public void GraphWindow_UsesTwoToolbarsAndKeepsInspectorReadable()
        {
            var window = ScriptableObject.CreateInstance<AuthoringGraphWindow>();
            try
            {
                Assert.That(
                    window.rootVisualElement.Q<Toolbar>(AuthoringGraphWindow.PrimaryToolbarName),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<Toolbar>(AuthoringGraphWindow.CommandToolbarName),
                    Is.Not.Null);

                var inspector = window.rootVisualElement.Q<VisualElement>(AuthoringGraphWindow.InspectorPaneName);
                Assert.That(inspector, Is.Not.Null);
                Assert.That(
                    inspector.style.minWidth.value.value,
                    Is.EqualTo(AuthoringGraphWindow.MinimumInspectorWidth));

                var overview = window.rootVisualElement.Q<VisualElement>(AuthoringOverviewPanel.RootElementName);
                Assert.That(overview, Is.Not.Null);
                Assert.That(
                    overview.style.maxHeight.value.value,
                    Is.EqualTo(AuthoringOverviewPanel.MaximumExpandedHeight));

                var inspectorScroll = window.rootVisualElement.Q<ScrollView>(AuthoringGraphWindow.InspectorScrollName);
                Assert.That(inspectorScroll, Is.Not.Null);
                Assert.That(
                    inspectorScroll.style.minHeight.value.value,
                    Is.EqualTo(AuthoringGraphWindow.MinimumInspectorContentHeight));
                Assert.That(window.minSize.x, Is.EqualTo(AuthoringGraphWindow.MinimumWindowWidth));
                Assert.That(window.minSize.y, Is.EqualTo(AuthoringGraphWindow.MinimumWindowHeight));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(window);
            }
        }

        [Test]
        public void CreateWizard_DisablesPrimaryActionForInvalidOrDuplicateTreeId()
        {
            var project = ScriptableObject.CreateInstance<AuthoringProjectAsset>();
            var tree = ScriptableObject.CreateInstance<AuthoringAsset>();
            try
            {
                var document = AuthoringTemplates.BuildEmpty();
                document.Tree.TreeId = "existing_tree";
                tree.SaveDocument(document);
                project.Register(tree);

                Assert.That(AuthoringCreateWizard.CanCreate(project, "new_tree"), Is.True);
                Assert.That(AuthoringCreateWizard.CanCreate(project, ""), Is.False);
                Assert.That(AuthoringCreateWizard.CanCreate(project, "invalid tree"), Is.False);
                Assert.That(AuthoringCreateWizard.CanCreate(project, "existing_tree"), Is.False);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(tree);
                ScriptableObject.DestroyImmediate(project);
            }
        }

        [Test]
        public void EditorEntryPoints_ExposeHubAndPreventRawAssetCreation()
        {
            var openMethod = typeof(AuthoringHubWindow).GetMethod(
                nameof(AuthoringHubWindow.OpenFromMenu),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var menuAttributes = openMethod?.GetCustomAttributes(typeof(MenuItem), false);
            var menu = menuAttributes is { Length: > 0 } ? (MenuItem)menuAttributes[0] : null;

            Assert.That(openMethod, Is.Not.Null);
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.menuItem, Is.EqualTo(AuthoringHubWindow.MainMenuPath));
            Assert.That(
                typeof(AuthoringAsset).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false),
                Is.Empty);
            Assert.That(
                typeof(AuthoringProjectAsset).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false),
                Is.Empty);
        }

        [Test]
        public void TreeEdge_LeavesParentVerticallyBeforeRoutingToChild()
        {
            var control = new AuthoringTreeEdgeControl
            {
                outputOrientation = UnityEditor.Experimental.GraphView.Orientation.Vertical,
                inputOrientation = UnityEditor.Experimental.GraphView.Orientation.Vertical,
                from = new Vector2(100f, 100f),
                to = new Vector2(360f, 300f),
            };

            control.ComputeControlPointsForTests();

            Assert.That(control.controlPoints, Has.Length.EqualTo(4));
            Assert.That(control.controlPoints[0], Is.EqualTo(control.from));
            Assert.That(control.controlPoints[1].x, Is.EqualTo(control.from.x));
            Assert.That(
                control.controlPoints[1].y - control.from.y,
                Is.InRange(
                    AuthoringTreeEdgeControl.MinimumDepartureLength,
                    AuthoringTreeEdgeControl.MaximumDepartureLength));
            Assert.That(control.controlPoints[2].x, Is.EqualTo(control.to.x));
            Assert.That(control.controlPoints[2].y, Is.LessThan(control.to.y));
            Assert.That(control.controlPoints[3], Is.EqualTo(control.to));
        }

        [Test]
        public void TreeEdge_AnchorsParentAndChildAtHorizontalCenter()
        {
            var parentBounds = new Rect(244f, 80f, 120f, 96f);
            var childBounds = new Rect(420f, 260f, 190f, 104f);

            var parentAnchor = AuthoringTreeEdge.ResolveNodeAnchor(
                parentBounds,
                UnityEditor.Experimental.GraphView.Direction.Output);
            var childAnchor = AuthoringTreeEdge.ResolveNodeAnchor(
                childBounds,
                UnityEditor.Experimental.GraphView.Direction.Input);

            Assert.That(parentAnchor, Is.EqualTo(new Vector2(304f, 176f)));
            Assert.That(childAnchor, Is.EqualTo(new Vector2(515f, 260f)));
        }

        [Test]
        public void TreeNode_ShowsAbortTypeAndRefreshesItInPlace()
        {
            var node = new NodeDefinition { Id = "composite", Type = BuiltInNodeTypes.Sequence };
            node.Properties.Set(
                CompositeNode.AbortTypeProperty,
                PropertyValue.Of((long)AbortType.Both));
            var view = new AuthoringNodeView(node, "Composite", isRoot: true, x: 0f, y: 0f);

            var badge = view.Q<Label>(AuthoringNodeView.AbortTypeBadgeName);
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.text, Is.EqualTo("中止：两者"));
            Assert.That(badge.parent.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            node.Properties.Set(
                CompositeNode.AbortTypeProperty,
                PropertyValue.Of((long)AbortType.LowerPriority));
            view.RefreshPropertySummary();

            Assert.That(badge.text, Is.EqualTo("中止：低优先级"));
        }

        [Test]
        public void TreeNode_ShowsBlackboardComparisonAndRefreshesItInPlace()
        {
            var blackboard = new BlackboardSchema();
            blackboard.Keys.Add(new BlackboardKeyDefinition
                { Name = "hasTarget", Type = ValueType.Bool });
            var node = new NodeDefinition { Id = "compare", Type = BuiltInNodeTypes.BlackboardCompare };
            node.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("hasTarget"));
            node.Properties.Set(BlackboardCompareNode.OpProperty, PropertyValue.Of(0L));
            node.Properties.Set(BlackboardCompareNode.RightKindProperty, PropertyValue.Of(0L));
            node.Properties.Set(BlackboardCompareNode.RightBoolProperty, PropertyValue.Of(true));

            var view = new AuthoringNodeView(
                node,
                "Has Target",
                isRoot: true,
                x: 0f,
                y: 0f,
                blackboard);
            var summary = view.Q<Label>(AuthoringNodeView.BehaviorSummaryLabelName);

            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.text, Is.EqualTo("hasTarget == true"));
            Assert.That(summary.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            node.Properties.Set(BlackboardCompareNode.OpProperty, PropertyValue.Of(1L));
            node.Properties.Set(BlackboardCompareNode.RightBoolProperty, PropertyValue.Of(false));
            view.RefreshPropertySummary();

            Assert.That(summary.text, Is.EqualTo("hasTarget != false"));
        }

        [Test]
        public void NodeSummary_FormatsTypedBlackboardOperands()
        {
            var blackboard = new BlackboardSchema();
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "hp", Type = ValueType.Int64 });
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "distance", Type = ValueType.Fixed64 });
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "state", Type = ValueType.String });
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "current", Type = ValueType.Int64 });
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "target", Type = ValueType.Int64 });
            var node = new NodeDefinition { Id = "compare", Type = BuiltInNodeTypes.BlackboardCompare };

            node.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("hp"));
            node.Properties.Set(BlackboardCompareNode.OpProperty, PropertyValue.Of(3L));
            node.Properties.Set(BlackboardCompareNode.RightInt64Property, PropertyValue.Of(30L));
            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out var summary), Is.True);
            Assert.That(summary, Is.EqualTo("hp <= 30"));

            node.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("distance"));
            node.Properties.Set(BlackboardCompareNode.OpProperty, PropertyValue.Of(2L));
            node.Properties.Set(
                BlackboardCompareNode.RightFixed64RawProperty,
                PropertyValue.Of(AbilityKit.Deterministic.Fixed64.FromRatio(25, 2)));
            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out summary), Is.True);
            Assert.That(summary, Is.EqualTo("distance < 12.5"));

            node.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("state"));
            node.Properties.Set(BlackboardCompareNode.OpProperty, PropertyValue.Of(1L));
            node.Properties.Set(BlackboardCompareNode.RightStringProperty, PropertyValue.Of("Dead"));
            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out summary), Is.True);
            Assert.That(summary, Is.EqualTo("state != \"Dead\""));

            node.Properties.Set(BlackboardCompareNode.LeftKeyProperty, PropertyValue.Of("current"));
            node.Properties.Set(BlackboardCompareNode.OpProperty, PropertyValue.Of(0L));
            node.Properties.Set(BlackboardCompareNode.RightKindProperty, PropertyValue.Of(1L));
            node.Properties.Set(BlackboardCompareNode.RightKeyProperty, PropertyValue.Of("target"));
            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out summary), Is.True);
            Assert.That(summary, Is.EqualTo("current == target"));
        }

        [Test]
        public void NodeSummary_FormatsSetBlackboardAndHandlesMissingSchemaKey()
        {
            var blackboard = new BlackboardSchema();
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "score", Type = ValueType.Int64 });
            blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "source", Type = ValueType.Int64 });
            var node = new NodeDefinition { Id = "set", Type = BuiltInNodeTypes.SetBlackboard };
            node.Properties.Set(SetBlackboardNode.KeyProperty, PropertyValue.Of("score"));
            node.Properties.Set(SetBlackboardNode.ConstInt64Property, PropertyValue.Of(10L));

            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out var summary), Is.True);
            Assert.That(summary, Is.EqualTo("score = 10"));

            node.Properties.Set(SetBlackboardNode.ValueKindProperty, PropertyValue.Of(1L));
            node.Properties.Set(SetBlackboardNode.FromKeyProperty, PropertyValue.Of("source"));
            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out summary), Is.True);
            Assert.That(summary, Is.EqualTo("score <- source"));

            node.Properties.Set(SetBlackboardNode.ValueKindProperty, PropertyValue.Of(0L));
            node.Properties.Set(SetBlackboardNode.KeyProperty, PropertyValue.Of("missing"));
            Assert.That(AuthoringNodeSummaryFormatter.TryFormat(node, blackboard, out summary), Is.True);
            Assert.That(summary, Is.EqualTo("missing = <?>"));
        }

        [Test]
        public void TreeNode_UsesCompactCenteredPortsInsteadOfFullWidthPorts()
        {
            var parent = new AuthoringNodeView(
                new NodeDefinition { Id = "parent", Type = BuiltInNodeTypes.Sequence },
                "Parent",
                isRoot: true,
                x: 0f,
                y: 0f);
            var child = new AuthoringNodeView(
                new NodeDefinition { Id = "child", Type = BuiltInNodeTypes.Succeed },
                "Child",
                isRoot: false,
                x: 0f,
                y: 160f);

            Assert.That(parent.OutputPort, Is.Not.Null);
            Assert.That(child.InputPort, Is.Not.Null);
            Assert.That(
                parent.OutputPort.style.width.value.value,
                Is.EqualTo(AuthoringNodeView.CenteredPortSize));
            Assert.That(
                child.InputPort.style.width.value.value,
                Is.EqualTo(AuthoringNodeView.CenteredPortSize));
            Assert.That(parent.OutputPort.style.flexGrow.value, Is.Zero);
            Assert.That(child.InputPort.style.flexGrow.value, Is.Zero);
            Assert.That(parent.OutputPort.style.alignSelf.value, Is.EqualTo(Align.Center));
            Assert.That(child.InputPort.style.alignSelf.value, Is.EqualTo(Align.Center));

            var loadedEdge = AuthoringTreeEdge.Connect(parent.OutputPort, child.InputPort);
            Assert.That(loadedEdge, Is.TypeOf<AuthoringTreeEdge>());
        }
    }
}
#endif
