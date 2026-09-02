#if UNITY_EDITOR
using System;
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
            string title = "Source Sync")
        {
            Inspection = inspection ?? throw new ArgumentNullException(nameof(inspection));
            Import = import;
            Export = export;
            CopyPath = copyPath;
            RevealPath = revealPath;
            Title = string.IsNullOrWhiteSpace(title) ? "Source Sync" : title;
        }

        public EditorSourceSyncInspection Inspection { get; }
        public string Title { get; }
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
                return Inspection.State switch
                {
                    EditorSourceSyncState.Untracked => "The source has no synchronized baseline.",
                    EditorSourceSyncState.InSync => "Local and source content are synchronized.",
                    EditorSourceSyncState.LocalChanged => "Local changes are ready to export.",
                    EditorSourceSyncState.SourceChanged => "External changes are ready to import.",
                    EditorSourceSyncState.Conflict => "Local and source content have diverged.",
                    EditorSourceSyncState.SourceMissing => "The bound source file is missing.",
                    EditorSourceSyncState.InvalidSource => string.IsNullOrEmpty(Error)
                        ? "The source cannot be read."
                        : Error,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }
}
#endif
