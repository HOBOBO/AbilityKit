#if UNITY_EDITOR
using System;

namespace AbilityKit.Editor.Platform.Synchronization
{
    public enum EditorSourceSyncState
    {
        Untracked = 0,
        InSync = 1,
        LocalChanged = 2,
        SourceChanged = 3,
        Conflict = 4,
        SourceMissing = 5,
        InvalidSource = 6
    }

    public enum EditorSourceSyncDirection
    {
        Import = 0,
        Export = 1
    }

    public enum EditorSourceSyncOperationDisposition
    {
        Allowed = 0,
        RequiresForce = 1,
        Blocked = 2
    }

    /// <summary>
    /// Domain-neutral inputs for three-way source synchronization classification.
    /// Domain packages remain responsible for path resolution, parsing, canonical hashing,
    /// validation, import/export IO, and persisting the synchronized baseline.
    /// </summary>
    public sealed class EditorSourceSyncSnapshot
    {
        public EditorSourceSyncSnapshot(
            string localHash,
            string sourceHash,
            string baselineHash,
            bool isTracked,
            bool sourceExists,
            bool sourceIsValid = true,
            string sourcePath = "",
            string error = "")
        {
            LocalHash = localHash ?? throw new ArgumentNullException(nameof(localHash));
            SourceHash = sourceHash ?? string.Empty;
            BaselineHash = baselineHash ?? string.Empty;
            IsTracked = isTracked;
            SourceExists = sourceExists;
            SourceIsValid = sourceIsValid;
            SourcePath = sourcePath ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public string LocalHash { get; }
        public string SourceHash { get; }
        public string BaselineHash { get; }
        public bool IsTracked { get; }
        public bool SourceExists { get; }
        public bool SourceIsValid { get; }
        public string SourcePath { get; }
        public string Error { get; }
    }

    public sealed class EditorSourceSyncInspection
    {
        public EditorSourceSyncInspection(EditorSourceSyncState state, EditorSourceSyncSnapshot snapshot)
        {
            State = state;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public EditorSourceSyncState State { get; }
        public EditorSourceSyncSnapshot Snapshot { get; }
        public bool LocalChanged => State == EditorSourceSyncState.LocalChanged || State == EditorSourceSyncState.Conflict;
        public bool SourceChanged => State == EditorSourceSyncState.SourceChanged || State == EditorSourceSyncState.Conflict;
        public bool CanImportWithoutForce => State == EditorSourceSyncState.InSync || State == EditorSourceSyncState.SourceChanged;
        public bool CanExportWithoutForce => State == EditorSourceSyncState.InSync || State == EditorSourceSyncState.LocalChanged;
    }

    public sealed class EditorSourceSyncOperationAssessment
    {
        public EditorSourceSyncOperationAssessment(
            EditorSourceSyncDirection direction,
            EditorSourceSyncOperationDisposition disposition,
            EditorSourceSyncState state)
        {
            Direction = direction;
            Disposition = disposition;
            State = state;
        }

        public EditorSourceSyncDirection Direction { get; }
        public EditorSourceSyncOperationDisposition Disposition { get; }
        public EditorSourceSyncState State { get; }
        public bool CanExecute => Disposition != EditorSourceSyncOperationDisposition.Blocked;
        public bool RequiresForce => Disposition == EditorSourceSyncOperationDisposition.RequiresForce;
    }

    /// <summary>
    /// Domain-neutral overwrite policy. Domains still decide whether an untracked local document
    /// contains authored content and remain responsible for confirmation, validation, and IO.
    /// </summary>
    public static class EditorSourceSyncOperationPolicy
    {
        public static EditorSourceSyncOperationAssessment Assess(
            EditorSourceSyncInspection inspection,
            EditorSourceSyncDirection direction,
            bool localHasAuthoredContent = false)
        {
            if (inspection == null) throw new ArgumentNullException(nameof(inspection));

            var disposition = direction switch
            {
                EditorSourceSyncDirection.Import => AssessImport(inspection.State, localHasAuthoredContent),
                EditorSourceSyncDirection.Export => AssessExport(inspection),
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown source sync direction.")
            };
            return new EditorSourceSyncOperationAssessment(direction, disposition, inspection.State);
        }

        private static EditorSourceSyncOperationDisposition AssessImport(
            EditorSourceSyncState state,
            bool localHasAuthoredContent)
        {
            return state switch
            {
                EditorSourceSyncState.InSync => EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.SourceChanged => EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.Untracked => localHasAuthoredContent
                    ? EditorSourceSyncOperationDisposition.RequiresForce
                    : EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.LocalChanged => EditorSourceSyncOperationDisposition.RequiresForce,
                EditorSourceSyncState.Conflict => EditorSourceSyncOperationDisposition.RequiresForce,
                EditorSourceSyncState.SourceMissing => EditorSourceSyncOperationDisposition.Blocked,
                EditorSourceSyncState.InvalidSource => EditorSourceSyncOperationDisposition.Blocked,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown source sync state.")
            };
        }

        private static EditorSourceSyncOperationDisposition AssessExport(EditorSourceSyncInspection inspection)
        {
            return inspection.State switch
            {
                EditorSourceSyncState.InSync => EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.LocalChanged => EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.SourceMissing => EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.Untracked => inspection.Snapshot.SourceExists
                    ? EditorSourceSyncOperationDisposition.RequiresForce
                    : EditorSourceSyncOperationDisposition.Allowed,
                EditorSourceSyncState.SourceChanged => EditorSourceSyncOperationDisposition.RequiresForce,
                EditorSourceSyncState.Conflict => EditorSourceSyncOperationDisposition.RequiresForce,
                EditorSourceSyncState.InvalidSource => EditorSourceSyncOperationDisposition.RequiresForce,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(inspection.State),
                    inspection.State,
                    "Unknown source sync state.")
            };
        }
    }

    /// <summary>Pure three-way classifier shared by source-backed editor domains.</summary>
    public static class EditorSourceSyncClassifier
    {
        public static EditorSourceSyncInspection Inspect(EditorSourceSyncSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (!snapshot.IsTracked)
            {
                return Result(EditorSourceSyncState.Untracked, snapshot);
            }

            if (!snapshot.SourceExists)
            {
                return Result(EditorSourceSyncState.SourceMissing, snapshot);
            }

            if (!snapshot.SourceIsValid)
            {
                return Result(EditorSourceSyncState.InvalidSource, snapshot);
            }

            // Independently converged content is in sync even if an older baseline differs.
            if (HashesEqual(snapshot.LocalHash, snapshot.SourceHash))
            {
                return Result(EditorSourceSyncState.InSync, snapshot);
            }

            if (string.IsNullOrEmpty(snapshot.BaselineHash))
            {
                return Result(EditorSourceSyncState.Untracked, snapshot);
            }

            var localChanged = !HashesEqual(snapshot.LocalHash, snapshot.BaselineHash);
            var sourceChanged = !HashesEqual(snapshot.SourceHash, snapshot.BaselineHash);
            if (localChanged && sourceChanged) return Result(EditorSourceSyncState.Conflict, snapshot);
            if (localChanged) return Result(EditorSourceSyncState.LocalChanged, snapshot);
            if (sourceChanged) return Result(EditorSourceSyncState.SourceChanged, snapshot);
            return Result(EditorSourceSyncState.InSync, snapshot);
        }

        private static bool HashesEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static EditorSourceSyncInspection Result(
            EditorSourceSyncState state,
            EditorSourceSyncSnapshot snapshot)
        {
            return new EditorSourceSyncInspection(state, snapshot);
        }
    }
}
#endif
