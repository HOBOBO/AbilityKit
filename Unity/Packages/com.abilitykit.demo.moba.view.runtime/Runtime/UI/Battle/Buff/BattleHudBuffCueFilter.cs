using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Pure-data helper that extracts Buff-relevant entries from a Moba presentation cue stream.
    /// It is intentionally Unity-free so it can be unit-tested against minimal stubs.
    /// </summary>
    internal static class BattleHudBuffCueFilter
    {
        public const string BuffOwnerKind = "Buff";

        /// <summary>
        /// True when the entry represents a Buff lifecycle event we can render in the HUD.
        /// Skill/executor/area cues are skipped.
        /// </summary>
        public static bool IsBuffCue(in PresentationCueData entry)
        {
            if (!string.Equals(entry.OwnerKind, BuffOwnerKind, System.StringComparison.Ordinal))
            {
                return false;
            }

            // Skip cues with no actor target or no instance key — they cannot be tracked in the HUD.
            if (entry.TargetActorId <= 0) return false;
            if (string.IsNullOrEmpty(entry.InstanceKey)) return false;
            return true;
        }

        /// <summary>
        /// Stages that mean "this buff is now active". Used to (re)create the icon.
        /// </summary>
        public static bool IsBuffActiveStage(PresentationCueStage stage)
        {
            return stage == PresentationCueStage.Started
                || stage == PresentationCueStage.Refreshed
                || stage == PresentationCueStage.Ticked
                || stage == PresentationCueStage.StackChanged;
        }

        /// <summary>
        /// Stages that mean "this buff instance is now removed".
        /// </summary>
        public static bool IsBuffRemoveStage(PresentationCueStage stage)
        {
            return stage == PresentationCueStage.Expired
                || stage == PresentationCueStage.Removed
                || stage == PresentationCueStage.Completed
                || stage == PresentationCueStage.Interrupted;
        }

        /// <summary>
        /// Iterate all buff-cue entries from the source stream. Reuses the supplied buffer to avoid allocations.
        /// </summary>
        public static void CollectBuffCues(
            IReadOnlyList<PresentationCueData> source,
            List<PresentationCueData> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            if (source == null) return;
            for (var i = 0; i < source.Count; i++)
            {
                var e = source[i];
                if (IsBuffCue(in e)) buffer.Add(e);
            }
        }
    }
}
