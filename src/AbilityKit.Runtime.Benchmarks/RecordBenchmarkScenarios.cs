using AbilityKit.Benchmarking;
using AbilityKit.Core.Recording.Core;

namespace AbilityKit.Runtime.Benchmarks;

public sealed class RecordIdHashScenario : BenchmarkScenarioBase
{
    private readonly int _nameLength;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private string[] _names = Array.Empty<string>();
    private long _checksum;
    private long _expectedChecksum;
    private long _operationCount;

    public RecordIdHashScenario(int nameLength, int batchSize = 100)
    {
        if (nameLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(nameLength));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _nameLength = nameLength;
        _batchSize = batchSize;
        _descriptor = new BenchmarkDescriptor(
            $"record.id-hash.utf8.length-{nameLength}",
            "record",
            "name-hash",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Package,
                ["algorithm"] = "fnv1a-32",
                ["encoding"] = "utf8",
                ["nameLength"] = nameLength.ToString(),
                ["distinctNames"] = batchSize.ToString(),
                ["batchSize"] = batchSize.ToString()
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        _names = Enumerable.Range(0, _batchSize)
            .Select(index => CreateName(index, _nameLength))
            .ToArray();
        _expectedChecksum = CalculateChecksum(_names);
    }

    public override void IterationSetup()
    {
        _checksum = 0;
        _operationCount = 0;
    }

    public override long Execute(int invocationCount)
    {
        long checksum = 0;
        for (var invocation = 0; invocation < invocationCount; invocation++)
        {
            for (var i = 0; i < _names.Length; i++)
                checksum = unchecked((checksum * 397) ^ RecordIdHash.Fnv1a32(_names[i]));
        }

        _checksum = checksum;
        _operationCount = checked((long)invocationCount * _names.Length);
        return _operationCount;
    }

    public override void Validate()
    {
        var expected = RepeatChecksum(_expectedChecksum, _names, _operationCount / _names.Length);
        if (_operationCount > 0 && _checksum != expected)
            throw new InvalidOperationException("Record identifier hash checksum is inconsistent.");
    }

    public override string GetDeterminismDigest() =>
        $"operations={_operationCount};checksum={_checksum}";

    private static long CalculateChecksum(IReadOnlyList<string> names)
    {
        long checksum = 0;
        for (var i = 0; i < names.Count; i++)
            checksum = unchecked((checksum * 397) ^ RecordIdHash.Fnv1a32(names[i]));
        return checksum;
    }

    private static long RepeatChecksum(long firstPassChecksum, IReadOnlyList<string> names, long passCount)
    {
        if (passCount == 0)
            return 0;
        if (passCount == 1)
            return firstPassChecksum;

        long checksum = firstPassChecksum;
        for (var pass = 1L; pass < passCount; pass++)
        {
            for (var i = 0; i < names.Count; i++)
                checksum = unchecked((checksum * 397) ^ RecordIdHash.Fnv1a32(names[i]));
        }
        return checksum;
    }

    private static string CreateName(int index, int length)
    {
        var prefix = $"record-{index:D6}-";
        if (prefix.Length >= length)
            return prefix[..length];
        return prefix + new string((char)('a' + index % 26), length - prefix.Length);
    }
}
