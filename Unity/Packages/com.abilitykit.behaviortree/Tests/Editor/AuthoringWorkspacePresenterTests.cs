#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.State;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Editor.Authoring.Workspace;
using AbilityKit.BehaviorTree.Nodes;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class AuthoringWorkspacePresenterTests
    {
        [Test]
        public void WorkspaceState_PersistsDocumentScopedSearchViewportAndPanelState()
        {
            var store = new MemoryStateStore();
            var state = new AuthoringWorkspaceState(store);

            state.SetDocumentScope("tree/a");
            state.NodeSearch = "attack";
            state.SelectedNodeId = "n1";
            state.SetViewport(12f, -24f, 0.75f);
            state.SetFoldoutExpanded("overview", false);
            state.SetPanelVisible("validation", true);

            state.SetDocumentScope("tree/b");
            Assert.That(state.NodeSearch, Is.Empty);
            Assert.That(state.TryGetViewport(out _), Is.False);
            Assert.That(state.GetFoldoutExpanded("overview"), Is.True);
            Assert.That(state.GetPanelVisible("validation", false), Is.False);

            state.SetDocumentScope("tree/a");
            Assert.That(state.NodeSearch, Is.EqualTo("attack"));
            Assert.That(state.SelectedNodeId, Is.EqualTo("n1"));
            Assert.That(state.TryGetViewport(out var viewport), Is.True);
            Assert.That(viewport.X, Is.EqualTo(12f));
            Assert.That(viewport.Y, Is.EqualTo(-24f));
            Assert.That(viewport.Scale, Is.EqualTo(0.75f));
            Assert.That(state.GetFoldoutExpanded("overview"), Is.False);
            Assert.That(state.GetPanelVisible("validation", false), Is.True);

            state.InspectorWidth = 120f;
            Assert.That(state.InspectorWidth, Is.EqualTo(240f));
        }

        [Test]
        public void Presenter_BuildsOverviewAndSearchesDisplayNameTypeAndCategory()
        {
            var controller = new AuthoringWorkspaceController();
            controller.Open(CreateDocument(12));
            var presenter = new AuthoringWorkspacePresenter(controller);

            var overview = presenter.BuildOverview("node 11");

            Assert.That(overview.NodeCount, Is.EqualTo(12));
            Assert.That(overview.EdgeCount, Is.EqualTo(11));
            Assert.That(overview.RootNodeId, Is.EqualTo("n0"));
            Assert.That(overview.Search.Hits, Has.Count.EqualTo(1));
            Assert.That(overview.Search.Hits[0].NodeId, Is.EqualTo("n11"));

            var typeSearch = presenter.SearchNodes("builtin.sequence");
            Assert.That(typeSearch.Hits.Count, Is.GreaterThan(0));
        }

        [TestCase(100, 120, 40, 500)]
        [TestCase(500, 260, 90, 1600)]
        [TestCase(1000, 500, 160, 3500)]
        public void AuthoringOpenSearchAndLayout_StayInsideLargeTreeBudgets(
            int nodeCount,
            int openBudgetMs,
            int searchBudgetMs,
            int layoutBudgetMs)
        {
            var document = CreateDocument(nodeCount);
            var controller = new AuthoringWorkspaceController();

            var openMs = Measure(() =>
            {
                controller.Open(document);
                _ = new AuthoringWorkspacePresenter(controller).BuildOverview();
            });
            Assert.That(openMs, Is.LessThanOrEqualTo(openBudgetMs), "open/overview budget");

            var presenter = new AuthoringWorkspacePresenter(controller);
            var searchMs = Measure(() =>
            {
                var result = presenter.SearchNodes("node " + (nodeCount - 1));
                Assert.That(result.Hits, Has.Count.EqualTo(1));
            });
            Assert.That(searchMs, Is.LessThanOrEqualTo(searchBudgetMs), "search budget");

            var layoutMs = Measure(() =>
            {
                Assert.That(
                    presenter.ApplyLayout(AuthoringLayoutOptions.Full, null, out var result),
                    Is.True);
                Assert.That(result.NodePositions, Has.Count.EqualTo(nodeCount));
            });
            Assert.That(layoutMs, Is.LessThanOrEqualTo(layoutBudgetMs), "layout budget");
        }

        private static double Measure(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private static AuthoringSourceDocument CreateDocument(int nodeCount)
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "perf-" + nodeCount;
            document.Tree.RootNodeId = "n0";
            document.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "target",
                Type = ValueType.String,
                Default = PropertyValue.Of(string.Empty),
            });

            for (var i = 0; i < nodeCount; i++)
            {
                var node = new NodeDefinition
                {
                    Id = "n" + i,
                    Type = i * 2 + 1 < nodeCount ? BuiltInNodeTypes.Sequence : BuiltInNodeTypes.Succeed,
                };
                var left = i * 2 + 1;
                var right = i * 2 + 2;
                if (left < nodeCount) node.ChildIds.Add("n" + left);
                if (right < nodeCount) node.ChildIds.Add("n" + right);
                document.Tree.Nodes.Add(node);
                document.NodeMetadata.Add(new AuthoringNodeMetadata
                {
                    NodeId = node.Id,
                    DisplayName = "Node " + i,
                });
            }

            return document;
        }

        private sealed class MemoryStateStore : IEditorUserStateStore
        {
            private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

            public bool HasKey(string key) => _values.ContainsKey(key);
            public string GetString(string key, string defaultValue = "") => Get(key, defaultValue);
            public void SetString(string key, string value) => _values[key] = value;
            public int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);
            public void SetInt(string key, int value) => _values[key] = value;
            public float GetFloat(string key, float defaultValue = 0f) => Get(key, defaultValue);
            public void SetFloat(string key, float value) => _values[key] = value;
            public bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);
            public void SetBool(string key, bool value) => _values[key] = value;
            public void DeleteKey(string key) => _values.Remove(key);

            private T Get<T>(string key, T fallback)
            {
                return _values.TryGetValue(key, out var value) && value is T typed
                    ? typed
                    : fallback;
            }
        }
    }
}
#endif
