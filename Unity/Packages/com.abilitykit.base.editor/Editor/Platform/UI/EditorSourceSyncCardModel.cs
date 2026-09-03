#if UNITY_EDITOR
using System;
using AbilityKit.Editor.Platform.Core;
using AbilityKit.Editor.Platform.Localization;
using AbilityKit.Editor.Platform.Synchronization;

namespace AbilityKit.Editor.Platform.UI
{
    /// <summary>
    /// Domain-neutral presentation model for source synchronization cards.
    /// Domains own inspection refresh, confirmation dialogs, import/export IO, and path actions.
    /// </summary>
    public sealed class EditorSourceSyncCardModel
    {
        public EditorSourceSyncCardModel(
            EditorSourceSyncInspection inspection,
            Action import,
            Action export,
            Action copyPath = null,
            Action revealPath = null,
            string title = null,
            IEditorLocalization localization = null)
        {
            Inspection = inspection ?? throw new ArgumentNullException(nameof(inspection));
            Import = import;
            Export = export;
            CopyPath = copyPath;
            RevealPath = revealPath;
            Localization = localization ?? AbilityKitEditorPlatform.Localization;
            CustomTitle = string.IsNullOrWhiteSpace(title) ? null : title;
        }

        public EditorSourceSyncInspection Inspection { get; }
        public IEditorLocalization Localization { get; }
        public string CustomTitle { get; }
        public string Title => CustomTitle ?? Localization.Get("abilitykit.editor.sourceSync.title");
        public string StateLabel => Localization.Get("abilitykit.editor.sourceSync.state." + Inspection.State);
        public string PathLabel => Localization.Get("abilitykit.editor.sourceSync.path");
        public string UnboundLabel => Localization.Get("abilitykit.editor.sourceSync.unbound");
        public string ImportLabel => Localization.Get("abilitykit.editor.sourceSync.import");
        public string ExportLabel => Localization.Get("abilitykit.editor.sourceSync.export");
        public string CopyPathLabel => Localization.Get("abilitykit.editor.sourceSync.copyPath");
        public string RevealLabel => Localization.Get("abilitykit.editor.sourceSync.reveal");
        public Action Import { get; }
        public Action Export { get; }
        public Action CopyPath { get; }
        public Action RevealPath { get; }
        public string SourcePath => Inspection.Snapshot.SourcePath;
        public string Error => Inspection.Snapshot.Error;
        public bool HasSourcePath => !string.IsNullOrWhiteSpace(SourcePath);
        public bool CanImport => Import != null;
        public bool CanExport => Export != null;
        public bool CanCopyPath => HasSourcePath && CopyPath != null;
        public bool CanRevealPath => HasSourcePath && RevealPath != null;

        public string StatusMessage
        {
            get
            {
                if (Inspection.State == EditorSourceSyncState.InvalidSource
                    && !string.IsNullOrEmpty(Error))
                {
                    return Error;
                }

                return Localization.Get(
                    "abilitykit.editor.sourceSync.message." + Inspection.State);
            }
        }
    }
}
#endif
