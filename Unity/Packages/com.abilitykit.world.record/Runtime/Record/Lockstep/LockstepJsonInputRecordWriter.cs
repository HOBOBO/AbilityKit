using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Host;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Core.Common.Record.Lockstep
{
    public sealed class LockstepJsonInputRecordWriter : ILockstepInputRecordWriter
    {
        private const int DefaultIndexChunkFrames = 300;

        private readonly string _outputPath;
        private readonly LockstepInputRecordMeta _meta;
        private readonly List<LockstepInputRecordFrame> _inputs = new List<LockstepInputRecordFrame>(2048);
        private readonly List<LockstepStateHashRecordFrame> _stateHashes = new List<LockstepStateHashRecordFrame>(256);
        private readonly List<LockstepSnapshotRecordFrame> _snapshots = new List<LockstepSnapshotRecordFrame>(256);
        private bool _disposed;

        public LockstepJsonInputRecordWriter(string outputPath, LockstepInputRecordMeta meta)
        {
            _outputPath = ResolvePath(outputPath);
            _meta = meta;
        }

        public void Append(in PlayerInputCommand cmd)
        {
            if (_disposed) return;

            var payload = cmd.Payload;
            var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;

            _inputs.Add(new LockstepInputRecordFrame
            {
                Frame = cmd.Frame.Value,
                PlayerId = cmd.Player.Value,
                OpCode = cmd.OpCode,
                PayloadBase64 = base64,
            });
        }

        public void AppendStateHash(int frame, int version, uint hash)
        {
            if (_disposed) return;

            _stateHashes.Add(new LockstepStateHashRecordFrame
            {
                Frame = frame,
                Version = version,
                Hash = hash,
            });
        }

        public void AppendSnapshot(int frame, int opCode, byte[] payload)
        {
            if (_disposed) return;

            var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;
            _snapshots.Add(new LockstepSnapshotRecordFrame
            {
                Frame = frame,
                OpCode = opCode,
                PayloadBase64 = base64,
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var index = BuildIndex(_inputs, _stateHashes, _snapshots, DefaultIndexChunkFrames);

            var file = new LockstepInputRecordFile
            {
                Meta = _meta,
                Inputs = _inputs,
                StateHashes = _stateHashes,
                Snapshots = _snapshots,
                Index = index,
            };

            var json = JsonConvert.SerializeObject(file, Formatting.Indented);

            var dir = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(_outputPath, json);
        }

        private static List<LockstepRecordChunkIndex> BuildIndex(
            List<LockstepInputRecordFrame> inputs,
            List<LockstepStateHashRecordFrame> hashes,
            List<LockstepSnapshotRecordFrame> snapshots,
            int chunkFrames)
        {
            if (chunkFrames <= 0) chunkFrames = DefaultIndexChunkFrames;

            var hasInputs = inputs != null && inputs.Count > 0;
            var hasHashes = hashes != null && hashes.Count > 0;
            var hasSnapshots = snapshots != null && snapshots.Count > 0;
            if (!hasInputs && !hasHashes && !hasSnapshots) return null;

            var minFrame = int.MaxValue;
            var maxFrame = int.MinValue;
            if (hasInputs)
            {
                minFrame = System.Math.Min(minFrame, inputs[0].Frame);
                maxFrame = System.Math.Max(maxFrame, inputs[inputs.Count - 1].Frame);
            }
            if (hasHashes)
            {
                minFrame = System.Math.Min(minFrame, hashes[0].Frame);
                maxFrame = System.Math.Max(maxFrame, hashes[hashes.Count - 1].Frame);
            }
            if (hasSnapshots)
            {
                minFrame = System.Math.Min(minFrame, snapshots[0].Frame);
                maxFrame = System.Math.Max(maxFrame, snapshots[snapshots.Count - 1].Frame);
            }

            if (minFrame == int.MaxValue || maxFrame == int.MinValue) return null;

            var list = new List<LockstepRecordChunkIndex>(System.Math.Max(1, (maxFrame - minFrame) / chunkFrames + 1));

            var startChunk = (minFrame / chunkFrames) * chunkFrames;
            var endChunk = (maxFrame / chunkFrames) * chunkFrames;

            var inputCursor = 0;
            var hashCursor = 0;
            var snapshotCursor = 0;

            for (var chunkStart = startChunk; chunkStart <= endChunk; chunkStart += chunkFrames)
            {
                var chunkEnd = chunkStart + chunkFrames - 1;

                var inputStart = inputCursor;
                if (hasInputs)
                {
                    while (inputCursor < inputs.Count && inputs[inputCursor].Frame <= chunkEnd)
                    {
                        if (inputs[inputCursor].Frame < chunkStart)
                        {
                            inputCursor++;
                            inputStart = inputCursor;
                            continue;
                        }
                        inputCursor++;
                    }
                }
                var inputEnd = inputCursor;

                var hashStart = hashCursor;
                if (hasHashes)
                {
                    while (hashCursor < hashes.Count && hashes[hashCursor].Frame <= chunkEnd)
                    {
                        if (hashes[hashCursor].Frame < chunkStart)
                        {
                            hashCursor++;
                            hashStart = hashCursor;
                            continue;
                        }
                        hashCursor++;
                    }
                }
                var hashEnd = hashCursor;

                var snapshotStart = snapshotCursor;
                if (hasSnapshots)
                {
                    while (snapshotCursor < snapshots.Count && snapshots[snapshotCursor].Frame <= chunkEnd)
                    {
                        if (snapshots[snapshotCursor].Frame < chunkStart)
                        {
                            snapshotCursor++;
                            snapshotStart = snapshotCursor;
                            continue;
                        }
                        snapshotCursor++;
                    }
                }
                var snapshotEnd = snapshotCursor;

                if (inputStart == inputEnd && hashStart == hashEnd && snapshotStart == snapshotEnd) continue;

                list.Add(new LockstepRecordChunkIndex
                {
                    StartFrame = chunkStart,
                    EndFrame = chunkEnd,
                    InputStart = inputStart,
                    InputEnd = inputEnd,
                    StateHashStart = hashStart,
                    StateHashEnd = hashEnd,
                    SnapshotStart = snapshotStart,
                    SnapshotEnd = snapshotEnd,
                });
            }

            return list.Count > 0 ? list : null;
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) path = "battle_record.json";

            if (Path.IsPathRooted(path)) return path;

            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            return Path.Combine(baseDir, path);
        }
    }
}
