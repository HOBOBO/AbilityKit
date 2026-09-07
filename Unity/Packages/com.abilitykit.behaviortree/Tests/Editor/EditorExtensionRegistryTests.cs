#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor.Authoring.Extensions;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class EditorExtensionRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorExtensionRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EditorExtensionRegistry.Reset();
        }

        // ---- Inspector section contributor ----

        [Test]
        public void InspectorSections_EnumerateInPriorityOrder()
        {
            var context = NewSectionContext();
            using var low = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("low")), 10);
            using var high = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("high")), 20);

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Is.EqualTo(new[] { "high", "low" }));
        }

        [Test]
        public void InspectorSections_EqualPriority_OrderedByRegistration()
        {
            var context = NewSectionContext();
            using var first = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("first")), 10);
            using var second = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("second")), 10);

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void InspectorSections_Dispose_RemovesOnlyThatRegistration()
        {
            var context = NewSectionContext();
            var keep = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("keep")), 10);
            var drop = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("drop")), 20);
            drop.Dispose();

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Is.EqualTo(new[] { "keep" }));
            keep.Dispose();
        }

        [Test]
        public void InspectorSections_DuplicateInstance_IndependentLifetimes()
        {
            var context = NewSectionContext();
            var contributor = new StaticSectionContributor(Section("shared"));
            var first = EditorExtensionRegistry.RegisterInspectorSectionContributor(contributor, 10);
            var second = EditorExtensionRegistry.RegisterInspectorSectionContributor(contributor, 20);
            second.Dispose();

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Is.EqualTo(new[] { "shared" }));
            first.Dispose();
        }

        [Test]
        public void InspectorSections_ThrowingContributor_IsIsolated()
        {
            var context = NewSectionContext();
            using var bad = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new ThrowingSectionContributor(), 20);
            using var good = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("good")), 10);

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Is.EqualTo(new[] { "good" }));
        }

        [Test]
        public void InspectorSections_ThrowingEnumerator_IsIsolated()
        {
            var context = NewSectionContext();
            using var partial = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new PartialThenThrowSectionContributor(), 20);
            using var good = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("good")), 10);

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Does.Contain("partial"));
            Assert.That(titles, Does.Contain("good"));
        }

        [Test]
        public void InspectorSections_NullReturn_IsTreatedAsEmpty()
        {
            var context = NewSectionContext();
            using var empty = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new NullSectionContributor(), 20);
            using var good = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("good")), 10);

            var titles = EditorExtensionRegistry.EnumerateInspectorSections(context)
                .Select(s => s.Title).ToList();

            Assert.That(titles, Is.EqualTo(new[] { "good" }));
        }

        // ---- Property field editor ----

        [Test]
        public void PropertyFieldEditor_ResolvesByTypeAndFieldName()
        {
            using var reg = EditorExtensionRegistry.RegisterPropertyFieldEditor(
                new StaticPropertyFieldEditor(Binding("hp", "moba.hero")));

            Assert.That(EditorExtensionRegistry.ResolvePropertyFieldEditor("moba.hero", "hp"), Is.Not.Null);
            Assert.That(EditorExtensionRegistry.ResolvePropertyFieldEditor("moba.hero", "mp"), Is.Null);
            Assert.That(EditorExtensionRegistry.ResolvePropertyFieldEditor("moba.minion", "hp"), Is.Null);
        }

        [Test]
        public void PropertyFieldEditor_EmptyTypeId_MatchesAnyType()
        {
            using var reg = EditorExtensionRegistry.RegisterPropertyFieldEditor(
                new StaticPropertyFieldEditor(Binding("hp", null)));

            Assert.That(EditorExtensionRegistry.ResolvePropertyFieldEditor("any.type", "hp"), Is.Not.Null);
        }

        [Test]
        public void PropertyFieldEditor_HigherPriorityWins()
        {
            var low = Binding("hp", null);
            var high = Binding("hp", null);
            using var lowReg = EditorExtensionRegistry.RegisterPropertyFieldEditor(
                new StaticPropertyFieldEditor(low), 10);
            using var highReg = EditorExtensionRegistry.RegisterPropertyFieldEditor(
                new StaticPropertyFieldEditor(high), 20);

            var resolved = EditorExtensionRegistry.ResolvePropertyFieldEditor("t", "hp");

            Assert.That(resolved, Is.SameAs(high));
        }

        [Test]
        public void PropertyFieldEditor_ThrowingEditor_IsIsolated()
        {
            using var bad = EditorExtensionRegistry.RegisterPropertyFieldEditor(
                new ThrowingPropertyFieldEditor(), 20);
            using var good = EditorExtensionRegistry.RegisterPropertyFieldEditor(
                new StaticPropertyFieldEditor(Binding("hp", null)), 10);

            Assert.That(EditorExtensionRegistry.ResolvePropertyFieldEditor("t", "hp"), Is.Not.Null);
        }

        // ---- Authoring diagnostic contributor ----

        [Test]
        public void Diagnostics_CollectInPriorityOrder_Isolated()
        {
            var document = new AuthoringSourceDocument();
            var registry = new NodeRegistry();
            var warning = new EditorDiagnostic("BTEXT001", EditorDiagnosticSeverity.Warning, "warn");
            var error = new EditorDiagnostic("BTEXT002", EditorDiagnosticSeverity.Error, "error");

            using var low = EditorExtensionRegistry.RegisterDiagnosticContributor(
                new StaticDiagnosticContributor(warning), 10);
            using var bad = EditorExtensionRegistry.RegisterDiagnosticContributor(
                new ThrowingDiagnosticContributor(), 20);
            using var high = EditorExtensionRegistry.RegisterDiagnosticContributor(
                new StaticDiagnosticContributor(error), 30);

            var diagnostics = EditorExtensionRegistry.Analyze(document, registry);

            Assert.That(diagnostics.Select(d => d.Code).ToList(), Is.EqualTo(new[] { "BTEXT002", "BTEXT001" }));
        }

        [Test]
        public void Diagnostics_ContributorReceivesDocumentAndRegistry()
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = "tree-a";
            var registry = new NodeRegistry();
            using var reg = EditorExtensionRegistry.RegisterDiagnosticContributor(
                new CapturingDiagnosticContributor());

            EditorExtensionRegistry.Analyze(document, registry);

            Assert.That(CapturingDiagnosticContributor.LastContext.Document.Tree.TreeId, Is.EqualTo("tree-a"));
            Assert.That(CapturingDiagnosticContributor.LastContext.Registry, Is.SameAs(registry));
        }

        // ---- Node catalog source ----

        [Test]
        public void CatalogSources_DedupeByTypeId_FirstInPriorityWins()
        {
            using var high = EditorExtensionRegistry.RegisterNodeCatalogSource(
                new StaticCatalogSource(Descriptor("dup", "high-wins"), Descriptor("only-high", "h")), 20);
            using var low = EditorExtensionRegistry.RegisterNodeCatalogSource(
                new StaticCatalogSource(Descriptor("dup", "low-loses"), Descriptor("only-low", "l")), 10);

            var descriptors = EditorExtensionRegistry.CollectCatalogDescriptors();

            Assert.That(descriptors.Select(d => d.TypeId).ToList(),
                Is.EqualTo(new[] { "dup", "only-high", "only-low" }));
            Assert.That(descriptors[0].DisplayName, Is.EqualTo("high-wins"));
        }

        [Test]
        public void CatalogSources_ThrowingSource_IsIsolated()
        {
            using var bad = EditorExtensionRegistry.RegisterNodeCatalogSource(
                new ThrowingCatalogSource(), 20);
            using var good = EditorExtensionRegistry.RegisterNodeCatalogSource(
                new StaticCatalogSource(Descriptor("good", "g")), 10);

            var descriptors = EditorExtensionRegistry.CollectCatalogDescriptors();

            Assert.That(descriptors.Select(d => d.TypeId).ToList(), Is.EqualTo(new[] { "good" }));
        }

        // ---- Registry hygiene ----

        [Test]
        public void Reset_ClearsAllRegistrations()
        {
            var context = NewSectionContext();
            using var reg = EditorExtensionRegistry.RegisterInspectorSectionContributor(
                new StaticSectionContributor(Section("x")));

            EditorExtensionRegistry.Reset();

            Assert.That(EditorExtensionRegistry.EnumerateInspectorSections(context), Is.Empty);
        }

        [Test]
        public void RegisterNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                EditorExtensionRegistry.RegisterInspectorSectionContributor(null!));
            Assert.Throws<ArgumentNullException>(() =>
                EditorExtensionRegistry.RegisterPropertyFieldEditor(null!));
            Assert.Throws<ArgumentNullException>(() =>
                EditorExtensionRegistry.RegisterDiagnosticContributor(null!));
            Assert.Throws<ArgumentNullException>(() =>
                EditorExtensionRegistry.RegisterNodeCatalogSource(null!));
        }

        [Test]
        public void PriorityConstants_AreOrdered()
        {
            Assert.That(EditorExtensionPriority.Framework, Is.LessThan(EditorExtensionPriority.Package));
            Assert.That(EditorExtensionPriority.Package, Is.LessThan(EditorExtensionPriority.Project));
        }

        // ---- Helpers ----

        private static InspectorSectionContext NewSectionContext()
        {
            var document = new AuthoringSourceDocument();
            var node = new NodeDefinition { Id = "n1", Type = "builtin.succeed" };
            return new InspectorSectionContext(document, node, isReadOnly: false);
        }

        private static InspectorSection Section(string title)
            => new InspectorSection(title, () => null);

        private static PropertyFieldEditorBinding Binding(string fieldName, string? typeId)
            => new PropertyFieldEditorBinding(fieldName, _ => null, typeId);

        private static NodeDescriptor Descriptor(string typeId, string displayName)
            => new NodeDescriptor(typeId, displayName, "test", NodeKind.Action, 0, 0, () => null);

        private sealed class StaticSectionContributor : IInspectorSectionContributor
        {
            private readonly InspectorSection[] _sections;
            public StaticSectionContributor(params InspectorSection[] sections) => _sections = sections;
            public IEnumerable<InspectorSection> BuildSections(InspectorSectionContext context) => _sections;
        }

        private sealed class ThrowingSectionContributor : IInspectorSectionContributor
        {
            public IEnumerable<InspectorSection> BuildSections(InspectorSectionContext context)
                => throw new InvalidOperationException("boom");
        }

        private sealed class PartialThenThrowSectionContributor : IInspectorSectionContributor
        {
            public IEnumerable<InspectorSection> BuildSections(InspectorSectionContext context)
            {
                yield return Section("partial");
                throw new InvalidOperationException("boom");
            }
        }

        private sealed class NullSectionContributor : IInspectorSectionContributor
        {
            public IEnumerable<InspectorSection> BuildSections(InspectorSectionContext context) => null!;
        }

        private sealed class StaticPropertyFieldEditor : IPropertyFieldEditor
        {
            private readonly PropertyFieldEditorBinding[] _bindings;
            public StaticPropertyFieldEditor(params PropertyFieldEditorBinding[] bindings) => _bindings = bindings;
            public IEnumerable<PropertyFieldEditorBinding> GetBindings() => _bindings;
        }

        private sealed class ThrowingPropertyFieldEditor : IPropertyFieldEditor
        {
            public IEnumerable<PropertyFieldEditorBinding> GetBindings()
                => throw new InvalidOperationException("boom");
        }

        private sealed class StaticDiagnosticContributor : IAuthoringDiagnosticContributor
        {
            private readonly EditorDiagnostic[] _diagnostics;
            public StaticDiagnosticContributor(params EditorDiagnostic[] diagnostics) => _diagnostics = diagnostics;
            public IEnumerable<EditorDiagnostic> Analyze(AuthoringDiagnosticContext context) => _diagnostics;
        }

        private sealed class ThrowingDiagnosticContributor : IAuthoringDiagnosticContributor
        {
            public IEnumerable<EditorDiagnostic> Analyze(AuthoringDiagnosticContext context)
                => throw new InvalidOperationException("boom");
        }

        private sealed class CapturingDiagnosticContributor : IAuthoringDiagnosticContributor
        {
            public static AuthoringDiagnosticContext LastContext = null!;
            public IEnumerable<EditorDiagnostic> Analyze(AuthoringDiagnosticContext context)
            {
                LastContext = context;
                yield break;
            }
        }

        private sealed class StaticCatalogSource : INodeCatalogSource
        {
            private readonly NodeDescriptor[] _descriptors;
            public StaticCatalogSource(params NodeDescriptor[] descriptors) => _descriptors = descriptors;
            public IEnumerable<NodeDescriptor> GetDescriptors() => _descriptors;
        }

        private sealed class ThrowingCatalogSource : INodeCatalogSource
        {
            public IEnumerable<NodeDescriptor> GetDescriptors()
                => throw new InvalidOperationException("boom");
        }
    }
}
#endif
