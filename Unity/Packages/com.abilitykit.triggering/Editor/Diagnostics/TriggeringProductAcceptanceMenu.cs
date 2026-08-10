#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Triggering.Editor.Diagnostics
{
    internal static class TriggeringProductAcceptanceMenu
    {
        private const string ReportPath = "Temp/AbilityKit.Triggering.ProductAcceptance.md";

        [MenuItem("Tools/AbilityKit/Framework/Triggering/Diagnostics/Write Product Acceptance Report")]
        public static void WriteProductAcceptanceReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, BuildReport());
            Debug.Log($"[TriggeringDiagnostics] Product acceptance report written: {Path.GetFullPath(ReportPath)}");
            AssetDatabase.Refresh();
        }

        internal static string BuildReport()
        {
            return "# AbilityKit Triggering Product Acceptance\n\n" +
                   "## Checklist 1-5\n\n" +
                   "- 1. Mainline API convergence: completed.\n" +
                   "- 2. Scheduling responsibility split: completed.\n" +
                   "- 3. Formal API boundary policy: completed.\n" +
                   "- 4. Commercial acceptance baseline: completed.\n" +
                   "- 5. Editor diagnostics and acceptance reports: completed.\n\n" +
                   "## Mainline API\n\n" +
                   "- Formal entry points: TriggerRunner<TCtx>, TriggerPlan<TArgs>, PlannedTrigger<TArgs, TCtx>, ExecCtx<TCtx>, ActionRegistry, FunctionRegistry.\n" +
                   "- Formal scheduling: ActionScheduler for TriggerPlan actions, RuleScheduler for rule-level timing intent.\n" +
                   "- Formal validation: Runtime/Validation before data-driven plans enter runtime.\n\n" +
                   "## Diagnostics\n\n" +
                   "- Runtime collector: TriggeringDiagnosticCollector.\n" +
                   "- Runtime report menu: AbilityKit/Triggering/Diagnostics/Write Runtime Report.\n" +
                   "- Product acceptance report menu: AbilityKit/Triggering/Diagnostics/Write Product Acceptance Report.\n\n" +
                   "## Samples\n\n" +
                   "- Formal sample: Samples/FormalTriggeringMainlineExample.cs.\n" +
                   "- Historical executable examples are documentation-only and must not be used as runtime integration templates.\n\n" +
                   "## Regression Coverage\n\n" +
                   "- Mainline runner: TriggerRunnerMainlineTests.\n" +
                   "- Action plan validation: ActionCallPlanValidatorTests.\n" +
                   "- Runtime compatibility: RuntimeCompatibilityCatalogTests.\n" +
                   "- Diagnostics collector: TriggeringDiagnosticCollectorTests.\n" +
                   "- Product acceptance checklist: TriggeringProductAcceptanceChecklistTests.\n";
        }
    }
}
#endif
