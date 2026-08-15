using AbilityKit.Benchmarking;
using AbilityKit.Core.Collections;

namespace AbilityKit.Runtime.Benchmarks;

public sealed class LegacyFrameIndexScenario : BenchmarkScenarioBase
{
    private readonly int _frameCount;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly Dictionary<int, int> _values;
    private readonly List<int> _frames;
    private long _cycleCount;
    private long _checksum;

    public LegacyFrameIndexScenario(int frameCount, int batchSize = 10)
    {
        if (frameCount < 2 || (frameCount & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _frameCount = frameCount;
        _batchSize = batchSize;
        _values = new Dictionary<int, int>(frameCount);
        _frames = new List<int>(frameCount);
        _descriptor = CreateDescriptor("legacy-list-sort", frameCount, batchSize);
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void IterationSetup()
    {
        _values.Clear();
        _frames.Clear();
        _cycleCount = 0;
        _checksum = 0;
    }

    public override long Execute(int invocationCount)
    {
        var cycleCount = checked(invocationCount * _batchSize);
        long checksum = 0;
        for (var cycle = 0; cycle < cycleCount; cycle++)
        {
            _values.Clear();
            _frames.Clear();
            for (var index = 0; index < _frameCount; index++)
            {
                var frame = Permute(index, _frameCount);
                _values[frame] = frame;
                if (!_frames.Contains(frame))
                {
                    _frames.Add(frame);
                    _frames.Sort();
                }
            }

            var removals = new List<int>();
            for (var index = 0; index < _frames.Count; index++)
            {
                if (_frames[index] < _frameCount / 2) removals.Add(_frames[index]);
            }

            for (var index = 0; index < removals.Count; index++)
            {
                _values.Remove(removals[index]);
                _frames.Remove(removals[index]);
            }

            for (var index = 0; index < _frames.Count; index++) checksum += _frames[index];
        }

        _cycleCount = cycleCount;
        _checksum = checksum;
        return checked((long)cycleCount * (_frameCount + (_frameCount / 2)));
    }

    public override void Validate() => ValidateResult(_frames, _frameCount, _cycleCount, _checksum);

    public override string GetDeterminismDigest() =>
        $"cycles={_cycleCount};retained={_frames.Count};checksum={_checksum}";

    internal static BenchmarkDescriptor CreateDescriptor(string implementation, int frameCount, int batchSize) =>
        new(
            $"core-collections.frame-index.{implementation}.frames-{frameCount}",
            "core-collections",
            "index-mutation",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Package,
                ["implementation"] = implementation,
                ["frameCount"] = frameCount.ToString(),
                ["insertionOrder"] = "deterministic-out-of-order",
                ["trim"] = "lower-half",
                ["batchSize"] = batchSize.ToString()
            });

    internal static int Permute(int index, int frameCount) =>
        (int)(((long)index * (frameCount - 1)) % frameCount);

    internal static void ValidateResult(
        IReadOnlyList<int> frames,
        int frameCount,
        long cycleCount,
        long checksum)
    {
        if (cycleCount == 0)
        {
            if (frames.Count != 0 || checksum != 0)
                throw new InvalidOperationException("Frame index benchmark initial state is inconsistent.");
            return;
        }

        var retained = frameCount / 2;
        var expectedPerCycle = (long)(retained + frameCount - 1) * retained / 2;
        if (frames.Count != retained || checksum != expectedPerCycle * cycleCount)
            throw new InvalidOperationException("Frame index benchmark result is inconsistent.");

        for (var index = 0; index < frames.Count; index++)
        {
            if (frames[index] != retained + index)
                throw new InvalidOperationException("Frame index order is inconsistent.");
        }
    }
}

public sealed class SortedIntSetFrameIndexScenario : BenchmarkScenarioBase
{
    private readonly int _frameCount;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly Dictionary<int, int> _values;
    private readonly SortedIntSet _frames;
    private long _cycleCount;
    private long _checksum;

    public SortedIntSetFrameIndexScenario(int frameCount, int batchSize = 10)
    {
        if (frameCount < 2 || (frameCount & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _frameCount = frameCount;
        _batchSize = batchSize;
        _values = new Dictionary<int, int>(frameCount);
        _frames = new SortedIntSet(frameCount);
        _descriptor = LegacyFrameIndexScenario.CreateDescriptor("sorted-int-set", frameCount, batchSize);
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void IterationSetup()
    {
        _values.Clear();
        _frames.Clear();
        _cycleCount = 0;
        _checksum = 0;
    }

    public override long Execute(int invocationCount)
    {
        var cycleCount = checked(invocationCount * _batchSize);
        long checksum = 0;
        for (var cycle = 0; cycle < cycleCount; cycle++)
        {
            _values.Clear();
            _frames.Clear();
            for (var index = 0; index < _frameCount; index++)
            {
                var frame = LegacyFrameIndexScenario.Permute(index, _frameCount);
                _values[frame] = frame;
                _frames.Add(frame);
            }

            var removeCount = _frames.LowerBound(_frameCount / 2);
            for (var index = 0; index < removeCount; index++)
                _values.Remove(_frames[index]);
            _frames.RemoveRange(0, removeCount);

            for (var index = 0; index < _frames.Count; index++) checksum += _frames[index];
        }

        _cycleCount = cycleCount;
        _checksum = checksum;
        return checked((long)cycleCount * (_frameCount + (_frameCount / 2)));
    }

    public override void Validate()
    {
        var snapshot = new int[_frames.Count];
        for (var index = 0; index < snapshot.Length; index++) snapshot[index] = _frames[index];
        LegacyFrameIndexScenario.ValidateResult(snapshot, _frameCount, _cycleCount, _checksum);
    }

    public override string GetDeterminismDigest() =>
        $"cycles={_cycleCount};retained={_frames.Count};checksum={_checksum}";
}
