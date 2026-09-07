#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Localization;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class EditorPlatformIntegrationTests
    {
        [Test]
        public void LocalizationSourceProvidesBilingualStableKeys()
        {
            var localization = new EditorLocalizationService();
            using var registration = localization.RegisterSource(EditorLocalization.CreateSource());

            localization.UserLanguageOverride = "en";
            Assert.That(
                localization.Get("abilitykit.behaviortree.command.save"),
                Is.EqualTo("Save"));

            localization.UserLanguageOverride = "zh-CN";
            Assert.That(
                localization.Get("abilitykit.behaviortree.command.save"),
                Is.EqualTo("保存"));
        }

        [Test]
        public void LocalizationRaisesLanguageChangedAndResolvesEveryCommandLabel()
        {
            var localization = new EditorLocalizationService();
            using var sourceRegistration = localization.RegisterSource(EditorLocalization.CreateSource());
            var changes = 0;
            localization.LanguageChanged += () => changes++;

            var commands = CreateCommands();
            foreach (var language in new[] { "en", "zh-CN" })
            {
                localization.UserLanguageOverride = language;
                foreach (var command in commands)
                {
                    Assert.That(
                        localization.Get(command.LabelKey),
                        Is.Not.EqualTo(command.LabelKey),
                        $"Missing {language} resource for {command.Id}.");
                }
            }

            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void CommandIdsAreUniqueAndStable()
        {
            var commands = CreateCommands();

            Assert.That(commands.Select(command => command.Id), Is.Unique);
            Assert.That(commands.Count, Is.EqualTo(13));
            Assert.That(commands.All(command => command.Id.StartsWith("bt.graph.", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void CommandsRespectReadOnlyAndHistoryCapabilities()
        {
            var readOnly = true;
            var canUndo = false;
            var executions = 0;
            Action execute = () => executions++;
            var commands = EditorCommandFactory.Create(
                execute, execute, execute, execute, execute, execute, execute,
                execute, execute, execute, execute, execute, execute,
                () => readOnly,
                () => canUndo,
                () => false);
            var registry = new EditorCommandRegistry();
            var registrations = commands.Select(registry.Register).ToArray();

            try
            {
                Assert.That(registry.Execute(EditorCommandIds.Save), Is.False);
                Assert.That(registry.Execute(EditorCommandIds.Undo), Is.False);
                Assert.That(registry.Execute(EditorCommandIds.FrameAll), Is.True);
                Assert.That(executions, Is.EqualTo(1));

                readOnly = false;
                canUndo = true;
                Assert.That(registry.Execute(EditorCommandIds.Save), Is.True);
                Assert.That(registry.Execute(EditorCommandIds.Undo), Is.True);
                Assert.That(executions, Is.EqualTo(3));
            }
            finally
            {
                foreach (var registration in registrations)
                    registration.Dispose();
            }
        }

        [Test]
        public void DiagnosticsPreserveMessagePathSeverityAndLocateAction()
        {
            var definition = new TreeDefinition
            {
                TreeId = "diagnostics",
                RootNodeId = "root"
            };
            definition.Nodes.Add(new NodeDefinition
            {
                Id = "root",
                Type = "missing.type"
            });
            string? located = null;

            var diagnostics = EditorDiagnostics.Analyze(
                definition,
                new NodeRegistry(),
                nodeId => located = nodeId);
            var diagnostic = diagnostics.Items.Single(item => item.Path == "nodes/root");

            Assert.That(diagnostic.Code, Is.EqualTo(EditorDiagnostics.ValidationErrorCode));
            Assert.That(diagnostic.Severity, Is.EqualTo(EditorDiagnosticSeverity.Error));
            Assert.That(diagnostic.Message, Does.Contain("root"));
            Assert.That(diagnostic.CanLocate, Is.True);

            diagnostic.Locate?.Invoke();
            Assert.That(located, Is.EqualTo("root"));
        }

        [Test]
        public void DiagnosticsDoNotMatchUnquotedNodeIdFragments()
        {
            var definition = new TreeDefinition
            {
                TreeId = "diagnostics",
                RootNodeId = "node"
            };
            definition.Nodes.Add(new NodeDefinition
            {
                Id = "node",
                Type = BuiltInNodeTypes.Succeed
            });

            var diagnostics = EditorDiagnostics.FromValidationMessages(
                definition,
                new[] { "A property named nodeSuffix is invalid." },
                _ => Assert.Fail("Unquoted fragments must not create locate actions."));

            Assert.That(diagnostics.Items.Single().Path, Is.EqualTo("tree"));
            Assert.That(diagnostics.Items.Single().CanLocate, Is.False);
        }

        private static IReadOnlyList<EditorCommand> CreateCommands()
        {
            Action execute = () => { };
            return EditorCommandFactory.Create(
                execute, execute, execute, execute, execute, execute, execute,
                execute, execute, execute, execute, execute, execute,
                () => false,
                () => true,
                () => true);
        }
    }
}
