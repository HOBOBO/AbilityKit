#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    /// <summary>
    /// Optional state-owned rollback payload. Validation is called for every participant before
    /// the runtime mutates structural state during restore.
    /// </summary>
    public interface IStateSnapshotParticipant
    {
        int SnapshotVersion { get; }

        string CaptureSnapshot();

        void ValidateSnapshot(int version, string payload);

        void RestoreSnapshot(int version, string payload);
    }
}
