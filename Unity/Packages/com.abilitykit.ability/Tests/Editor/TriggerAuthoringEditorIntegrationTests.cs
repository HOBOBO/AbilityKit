#if UNITY_EDITOR
using System;
using System.Linq;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Localization;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringEditorIntegrationTests
    {
        [Test]
        public void CommandsHaveStableUniqueIdsAndLocalizedLabels()
        {
            Action execute = () => { };
            var commands = TriggerAuthoringCommandFactory.CreateModule(
                execute,
                execute,
                execute,
                execute,
                () => true)
                .Concat(TriggerAuthoringCommandFactory.CreateWorkspace(
                    execute,
                    execute,
                    execute,
                    execute,
                    execute,
                    () => true))
                .ToArray();

            Assert.That(commands.Select(command => command.Id), Is.Unique);
            Assert.That(commands.All(command => command.Id.StartsWith("trigger.", StringComparison.Ordinal)), Is.True);

            foreach (var language in new[] { "en", "zh-CN" })
            {
                var localization = new EditorLocalizationService();
                using (localization.RegisterSource(TriggerAuthoringEditorIntegration.CreateLocalizationSource()))
                {
                    localization.UserLanguageOverride = language;
                    foreach (var command in commands)
                    {
                        Assert.That(localization.Get(command.LabelKey), Is.Not.EqualTo(command.LabelKey));
                        Assert.That(localization.Get(command.TooltipKey), Is.Not.EqualTo(command.TooltipKey));
                    }
                }
            }
        }

        [Test]
        public void DiagnosticAdapterPreservesDataAndProvidesLocateAction()
        {
            var locatedPath = string.Empty;
            var diagnostics = TriggerAuthoringDiagnosticAdapter.Adapt(
                new[]
                {
                    new TriggerAuthoringDiagnostic(
                        "TRG9999",
                        TriggerAuthoringDiagnosticSeverity.Error,
                        "module.triggers[2].actions",
                        "Broken action")
                },
                locatePath: path => locatedPath = path);

            Assert.That(diagnostics.ErrorCount, Is.EqualTo(1));
            Assert.That(diagnostics.Items[0].Severity, Is.EqualTo(EditorDiagnosticSeverity.Error));
            Assert.That(diagnostics.Items[0].CanLocate, Is.True);

            diagnostics.Items[0].Locate();
            Assert.That(locatedPath, Is.EqualTo("module.triggers[2].actions"));
        }

        [Test]
        public void DisabledModuleCommandsDoNotExecute()
        {
            var executions = 0;
            var command = TriggerAuthoringCommandFactory.CreateModule(
                () => executions++,
                () => executions++,
                () => executions++,
                () => executions++,
                () => false)[0];

            Assert.That(command.TryExecute(new EditorCommandContext()), Is.False);
            Assert.That(executions, Is.Zero);
        }
    }
}
#endif
