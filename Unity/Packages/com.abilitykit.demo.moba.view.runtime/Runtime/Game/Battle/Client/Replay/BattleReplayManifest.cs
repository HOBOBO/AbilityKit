using System;
using System.Collections.Generic;
using AbilityKit.Core.Recording.FrameRecord;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    /// <summary>
    /// Versioned description of a battle replay artifact.
    /// The manifest describes compatibility and seek anchors only; it never
    /// claims that transport snapshots are directly restorable world checkpoints.
    /// </summary>
    public sealed class BattleReplayManifest
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; private set; }
        public string WorldId { get; private set; }
        public string WorldType { get; private set; }
        public string PlayerId { get; private set; }
        public int TickRate { get; private set; }
        public int RandomSeed { get; private set; }
        public long StartedAtUnixMs { get; private set; }
        public int FirstFrame { get; private set; }
        public int LastFrame { get; private set; }
        public IReadOnlyList<BattleReplaySeekAnchor> SeekAnchors { get; private set; }

        private BattleReplayManifest()
        {
        }

        public static bool TryCreate(FrameRecordFile file, out BattleReplayManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            if (file == null)
            {
                error = "Replay file is empty.";
                return false;
            }

            var meta = file.Meta;
            if (meta == null)
            {
                error = "Replay file is missing metadata.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(meta.WorldId) || string.IsNullOrWhiteSpace(meta.WorldType))
            {
                error = "Replay metadata requires WorldId and WorldType.";
                return false;
            }

            if (meta.TickRate <= 0)
            {
                error = "Replay metadata requires a positive tick rate.";
                return false;
            }

            var anchors = BuildSeekAnchors(file);
            ResolveFrameRange(file, anchors, out var firstFrame, out var lastFrame);
            manifest = new BattleReplayManifest
            {
                SchemaVersion = CurrentSchemaVersion,
                WorldId = meta.WorldId,
                WorldType = meta.WorldType,
                PlayerId = meta.PlayerId ?? string.Empty,
                TickRate = meta.TickRate,
                RandomSeed = meta.RandomSeed,
                StartedAtUnixMs = meta.StartedAtUnixMs,
                FirstFrame = firstFrame,
                LastFrame = lastFrame,
                SeekAnchors = anchors,
            };
            return true;
        }

        public bool IsCompatibleWith(string worldId, string worldType, int tickRate, out string error)
        {
            error = null;
            if (SchemaVersion != CurrentSchemaVersion)
            {
                error = $"Unsupported replay manifest schema version: {SchemaVersion}.";
                return false;
            }

            if (!string.Equals(WorldId, worldId, StringComparison.Ordinal))
            {
                error = $"Replay WorldId '{WorldId}' does not match requested WorldId '{worldId}'.";
                return false;
            }

            if (!string.Equals(WorldType, worldType, StringComparison.Ordinal))
            {
                error = $"Replay WorldType '{WorldType}' does not match requested WorldType '{worldType}'.";
                return false;
            }

            var effectiveTickRate = tickRate > 0 ? tickRate : 30;
            if (TickRate != effectiveTickRate)
            {
                error = $"Replay tick rate {TickRate} does not match requested tick rate {effectiveTickRate}.";
                return false;
            }

            return true;
        }

        public BattleReplaySeekAnchor ResolveSeekAnchor(int targetFrame)
        {
            if (SeekAnchors == null || SeekAnchors.Count == 0)
            {
                return BattleReplaySeekAnchor.Start;
            }

            var target = Math.Max(0, Math.Min(targetFrame, LastFrame));
            var result = BattleReplaySeekAnchor.Start;
            for (var i = 0; i < SeekAnchors.Count; i++)
            {
                var candidate = SeekAnchors[i];
                if (candidate.StartFrame > target) break;
                result = candidate;
            }

            return result;
        }

        private static IReadOnlyList<BattleReplaySeekAnchor> BuildSeekAnchors(FrameRecordFile file)
        {
            var index = file.Index;
            if (index == null || index.Count == 0)
            {
                return Array.Empty<BattleReplaySeekAnchor>();
            }

            var anchors = new List<BattleReplaySeekAnchor>(index.Count);
            for (var i = 0; i < index.Count; i++)
            {
                var chunk = index[i];
                if (chunk == null || chunk.EndFrame < chunk.StartFrame) continue;
                anchors.Add(new BattleReplaySeekAnchor(chunk.StartFrame, chunk.EndFrame));
            }

            anchors.Sort((left, right) => left.StartFrame.CompareTo(right.StartFrame));
            return anchors;
        }

        private static void ResolveFrameRange(
            FrameRecordFile file,
            IReadOnlyList<BattleReplaySeekAnchor> anchors,
            out int firstFrame,
            out int lastFrame)
        {
            firstFrame = int.MaxValue;
            lastFrame = 0;
            IncludeFrames(file.Inputs, static item => item.Frame, ref firstFrame, ref lastFrame);
            IncludeFrames(file.StateHashes, static item => item.Frame, ref firstFrame, ref lastFrame);
            IncludeFrames(file.Snapshots, static item => item.Frame, ref firstFrame, ref lastFrame);

            if (anchors != null)
            {
                for (var i = 0; i < anchors.Count; i++)
                {
                    var anchor = anchors[i];
                    firstFrame = Math.Min(firstFrame, anchor.StartFrame);
                    lastFrame = Math.Max(lastFrame, anchor.EndFrame);
                }
            }

            if (firstFrame == int.MaxValue) firstFrame = 0;
        }

        private static void IncludeFrames<T>(
            IList<T> items,
            Func<T, int> getFrame,
            ref int firstFrame,
            ref int lastFrame)
            where T : class
        {
            if (items == null) return;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                var frame = getFrame(item);
                firstFrame = Math.Min(firstFrame, frame);
                lastFrame = Math.Max(lastFrame, frame);
            }
        }
    }

    public readonly struct BattleReplaySeekAnchor
    {
        public static readonly BattleReplaySeekAnchor Start = new BattleReplaySeekAnchor(0, 0);

        public BattleReplaySeekAnchor(int startFrame, int endFrame)
        {
            StartFrame = startFrame;
            EndFrame = endFrame;
        }

        public int StartFrame { get; }
        public int EndFrame { get; }
    }
}
