#if UNITY_EDITOR
using System.IO;
using AbilityKit.Triggering.Runtime.Compatibility;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Triggering.Editor.Diagnostics
{
    internal static class TriggeringDiagnosticsMenu
    {
        private const string ReportPath = "Temp/AbilityKit.Triggering.Diagnostics.md";

        [MenuItem("Tools/AbilityKit/Framework/Triggering/Diagnostics/Write Runtime Report")]
        public static void WriteRuntimeReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, BuildReport());
            Debug.Log($"[TriggeringDiagnostics] Runtime report written: {Path.GetFullPath(ReportPath)}");
            AssetDatabase.Refresh();
        }

        private static string BuildReport()
        {
            return "# AbilityKit Triggering Diagnostics\n\n" +
                   "## Runtime Compatibility\n\n" +
                   $"- Root compatibility entries: {RootRuntimeCompatibilityCatalog.Entries.Count}\n" +
                   "- Expected new entry path: TriggerRunner / TriggerPlan / ActionScheduler / RuleScheduler\n" +
                   "- Legacy caller regression is covered by RuntimeCompatibilityCatalogTests.PackageExternalSources_DoNotCallLegacyRuntimeEntries\n\n" +
                   "## Runtime Statistics\n\n" +
                   "- Use TriggeringDiagnosticCollector to aggregate validation, JSON load, execution, schedule, and legacy-hit records.\n" +
                   "- Export TriggeringDiagnosticCollector.Snapshot in project-specific diagnostic windows or release logs.\n";
        }
    }
}
#endif
