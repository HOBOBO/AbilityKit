using System.Diagnostics;
using System.Text.Json;
using AbilityKit.Core.Recording.FrameRecord;
using Xunit;

namespace AbilityKit.Record.Tests;

public sealed class FrameRecordDiffAnalyzerTests
{
    [Fact]
    public void CompareReturnsIdenticalForMatchingHashTracks()
    {
        var left = CreateRecord("world-1", Hash(10, 1, 100u), Hash(10, 2, 200u));
        var right = CreateRecord("world-1", Hash(10, 1, 100u), Hash(10, 2, 200u));

        var report = new FrameRecordDiffAnalyzer().Compare(left, right);

        Assert.Equal(FrameRecordDiffStatus.Identical, report.Status);
        Assert.True(report.Matched);
        Assert.Null(report.FirstDivergence);
        Assert.Null(report.Context);
    }

    [Fact]
    public void CompareFindsSecondHashAtSameFrameAndBuildsBoundedContext()
    {
        var payload = new byte[] { 1, 2, 3 };
        var left = CreateRecord("world-1", Hash(10, 1, 100u), Hash(10, 2, 200u));
        left.Inputs.Add(new FrameRecordInputFrame
        {
            Frame = 9,
            PlayerId = "player-1",
            OpCode = 7,
            PayloadBase64 = Convert.ToBase64String(payload),
        });
        left.Snapshots.Add(new FrameRecordSnapshotFrame
        {
            Frame = 11,
            OpCode = 8,
            PayloadBase64 = Convert.ToBase64String(payload),
        });
        var right = CreateRecord("world-1", Hash(10, 1, 100u), Hash(10, 2, 201u));

        var report = new FrameRecordDiffAnalyzer().Compare(
            left,
            right,
            new FrameRecordDiffOptions { ContextFrames = 1 });

        Assert.Equal(FrameRecordDiffStatus.Diverged, report.Status);
        Assert.False(report.Matched);
        Assert.NotNull(report.FirstDivergence);
        Assert.NotNull(report.FirstDivergence.Left);
        Assert.NotNull(report.FirstDivergence.Right);
        Assert.Equal(10, report.FirstDivergence.Frame);
        Assert.Equal(1, report.FirstDivergence.Ordinal);
        Assert.Equal(200u, report.FirstDivergence.Left.Hash);
        Assert.Equal(201u, report.FirstDivergence.Right.Hash);
        Assert.NotNull(report.Context);
        Assert.Equal(9, report.Context.StartFrame);
        Assert.Equal(11, report.Context.EndFrame);
        var input = Assert.Single(report.Context.Left.Inputs);
        Assert.Equal(3, input.PayloadBytes);
        Assert.True(input.PayloadValid);
        Assert.Equal("039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81", input.PayloadSha256);
        Assert.Single(report.Context.Left.Snapshots);
        Assert.Equal(2, report.Context.Left.StateHashes.Count);
    }

    [Fact]
    public void CompareReportsMissingHashOnOneSide()
    {
        var left = CreateRecord("world-1", Hash(10, 1, 100u), Hash(20, 1, 200u));
        var right = CreateRecord("world-1", Hash(10, 1, 100u));

        var report = new FrameRecordDiffAnalyzer().Compare(left, right);

        Assert.Equal(FrameRecordDiffStatus.Diverged, report.Status);
        Assert.NotNull(report.FirstDivergence);
        Assert.Equal(20, report.FirstDivergence.Frame);
        Assert.NotNull(report.FirstDivergence.Left);
        Assert.Null(report.FirstDivergence.Right);
    }

    [Fact]
    public void CompareRejectsDifferentWorldsBeforeHashComparison()
    {
        var left = CreateRecord("world-left", Hash(1, 1, 10u));
        var right = CreateRecord("world-right", Hash(1, 1, 10u));

        var report = new FrameRecordDiffAnalyzer().Compare(left, right);

        Assert.Equal(FrameRecordDiffStatus.WorldMismatch, report.Status);
        Assert.False(report.Matched);
        Assert.Null(report.FirstDivergence);
    }

    [Fact]
    public void CompareReportsNoComparableHashesForEmptyTrack()
    {
        var left = CreateRecord("world-1");
        var right = CreateRecord("world-1", Hash(1, 1, 10u));

        var report = new FrameRecordDiffAnalyzer().Compare(left, right);

        Assert.Equal(FrameRecordDiffStatus.NoComparableStateHashes, report.Status);
        Assert.Equal(FrameRecordDiffReason.LeftStateHashesMissing, report.Reason);
        Assert.False(report.Matched);
    }

    [Fact]
    public void CompareMarksInvalidPayloadWithoutTreatingItAsValidEmptyPayload()
    {
        var left = CreateRecord("world-1", Hash(10, 1, 100u));
        left.Inputs.Add(new FrameRecordInputFrame
        {
            Frame = 10,
            PlayerId = "player-1",
            OpCode = 7,
            PayloadBase64 = "not-base64",
        });
        var right = CreateRecord("world-1", Hash(10, 1, 101u));

        var report = new FrameRecordDiffAnalyzer().Compare(left, right);

        Assert.NotNull(report.Context);
        var input = Assert.Single(report.Context.Left.Inputs);
        Assert.False(input.PayloadValid);
        Assert.Equal(0, input.PayloadBytes);
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            input.PayloadSha256);
    }

    [Fact]
    public void CompareSaturatesContextBoundsForLargeWindow()
    {
        var left = CreateRecord("world-1", Hash(int.MaxValue, 1, 100u));
        var right = CreateRecord("world-1", Hash(int.MaxValue, 1, 101u));

        var report = new FrameRecordDiffAnalyzer().Compare(
            left,
            right,
            new FrameRecordDiffOptions { ContextFrames = int.MaxValue });

        Assert.NotNull(report.Context);
        Assert.Equal(0, report.Context.StartFrame);
        Assert.Equal(int.MaxValue, report.Context.EndFrame);
    }

    private static FrameRecordFile CreateRecord(
        string worldId,
        params FrameRecordStateHashFrame[] hashes)
    {
        return new FrameRecordFile
        {
            Meta = new FrameRecordMeta
            {
                WorldId = worldId,
                WorldType = "test",
                TickRate = 30,
                RandomSeed = 42,
                PlayerId = "player-1",
                StartedAtUnixMs = 1L,
            },
            Inputs = new List<FrameRecordInputFrame>(),
            StateHashes = hashes.ToList(),
            Snapshots = new List<FrameRecordSnapshotFrame>(),
            Index = new List<FrameRecordChunkIndex>(),
        };
    }

    private static FrameRecordStateHashFrame Hash(int frame, int version, uint hash)
    {
        return new FrameRecordStateHashFrame
        {
            Frame = frame,
            Version = version,
            Hash = hash,
        };
    }
}

public sealed class FrameRecordToolsEndToEndTests
{
    [Fact]
    public async Task DiffCommandLoadsOptimizedRecordsAndWritesMachineReadableReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-record-tools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var leftPath = Path.Combine(directory, "left.record.bin");
            var rightPath = Path.Combine(directory, "right.record.bin");
            WriteRecord(leftPath, 200u);
            WriteRecord(rightPath, 201u);

            var result = await RunToolAsync(
                $"diff \"{leftPath}\" \"{rightPath}\" --context 1");

            Assert.True(
                result.ExitCode == 1,
                $"Expected exit code 1, but received {result.ExitCode}.{Environment.NewLine}" +
                $"stdout: {result.StandardOutput}{Environment.NewLine}" +
                $"stderr: {result.StandardError}");
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var json = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("diverged", json.RootElement.GetProperty("status").GetString());
            Assert.False(json.RootElement.GetProperty("matched").GetBoolean());
            Assert.Equal(20, json.RootElement.GetProperty("firstDivergence").GetProperty("frame").GetInt32());
            Assert.Equal(19, json.RootElement.GetProperty("context").GetProperty("startFrame").GetInt32());
            Assert.Equal(21, json.RootElement.GetProperty("context").GetProperty("endFrame").GetInt32());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DiffCommandReturnsZeroForMatchingRecords()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-record-tools", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "record.bin");
            WriteRecord(path, 200u);

            var result = await RunToolAsync($"diff \"{path}\" \"{path}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var json = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("identical", json.RootElement.GetProperty("status").GetString());
            Assert.True(json.RootElement.GetProperty("matched").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DiffCommandReturnsTwoForInvalidUsage()
    {
        var result = await RunToolAsync("diff");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.Contains("Usage:", result.StandardError, StringComparison.Ordinal);
    }

    private static async Task<ToolResult> RunToolAsync(string arguments)
    {
        const int WindowsDllInitializationFailed = unchecked((int)0xC0000142);
        const int maxStartAttempts = 3;

        var toolPath = Path.Combine(AppContext.BaseDirectory, "AbilityKit.Record.Tools.dll");
        Assert.True(File.Exists(toolPath), $"Tool assembly was not copied to test output: {toolPath}");

        ToolResult? result = null;
        for (var attempt = 1; attempt <= maxStartAttempts; attempt++)
        {
            result = await RunToolOnceAsync(toolPath, arguments);
            if (result.ExitCode != WindowsDllInitializationFailed || attempt == maxStartAttempts)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
        }

        return result!;
    }

    private static async Task<ToolResult> RunToolOnceAsync(string toolPath, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{toolPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        Assert.NotNull(process);

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ToolResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static void WriteRecord(string path, uint finalHash)
    {
        var meta = new FrameRecordMeta
        {
            WorldId = "world-1",
            WorldType = "test",
            TickRate = 30,
            RandomSeed = 42,
            PlayerId = "player-1",
            StartedAtUnixMs = 1L,
        };
        using var writer = new FrameRecordOptimizedBinaryWriter(path, meta);
        writer.AppendStateHash(10, 1, 100u);
        writer.AppendStateHash(20, 1, finalHash);
        writer.AppendSnapshot(20, 7, new byte[] { 4, 5, 6 });
    }

    private sealed record ToolResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
