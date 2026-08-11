using AbilityKit.Attributes.Core;
using AbilityKit.Benchmarking;
using AbilityKit.Modifiers;

namespace AbilityKit.Runtime.Benchmarks;

public sealed class AttributeRecomputeScenario : BenchmarkScenarioBase
{
    private static int _scenarioSequence;

    private readonly int _modifierCount;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private AttributeContext _context = null!;
    private AttributeId _attribute;
    private double _checksum;
    private long _operationCount;

    public AttributeRecomputeScenario(int modifierCount, int batchSize = 100)
    {
        if (modifierCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(modifierCount));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _modifierCount = modifierCount;
        _batchSize = batchSize;
        _descriptor = new BenchmarkDescriptor(
            $"attributes.recompute.modifiers-{modifierCount}",
            "attributes",
            "dirty-recompute",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Package,
                ["formula"] = "default",
                ["modifierCount"] = modifierCount.ToString(),
                ["batchSize"] = batchSize.ToString()
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        var sequence = Interlocked.Increment(ref _scenarioSequence);
        var definition = new AttributeDef(
            $"benchmark_attribute_{sequence}",
            defaultBaseValue: 100f);
        _attribute = AttributeRegistry.DefaultRegistry.Register(definition);
        _context = new AttributeContext();

        for (var index = 0; index < _modifierCount; index++)
            _context.AddModifier(_attribute, ModifierOp.Add, index + 1f);

        _ = _context.GetValue(_attribute);
    }

    public override void IterationSetup()
    {
        _checksum = 0;
        _operationCount = 0;
    }

    public override long Execute(int invocationCount)
    {
        double checksum = 0;
        for (var invocation = 0; invocation < invocationCount; invocation++)
        {
            for (var index = 0; index < _batchSize; index++)
            {
                var baseValue = ((invocation * _batchSize + index) & 1) == 0 ? 100f : 101f;
                _context.SetBase(_attribute, baseValue);
                checksum += _context.GetValue(_attribute);
            }
        }

        _checksum = checksum;
        _operationCount = checked((long)invocationCount * _batchSize);
        return _operationCount;
    }

    public override void Validate()
    {
        var modifierSum = _modifierCount * (_modifierCount + 1) / 2f;
        var oddCount = _operationCount / 2;
        var evenCount = _operationCount - oddCount;
        var expected = evenCount * (100f + modifierSum) + oddCount * (101f + modifierSum);
        if (Math.Abs(_checksum - expected) > 0.001d)
            throw new InvalidOperationException("Attribute recompute checksum is inconsistent.");
    }

    public override string GetDeterminismDigest() =>
        $"recomputes={_operationCount};checksum={_checksum:F3}";
}

public sealed class ModifierComposeSortedScenario : BenchmarkScenarioBase
{
    private readonly int _modifierCount;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private ModifierData[] _modifiers = Array.Empty<ModifierData>();
    private double _checksum;
    private double _expectedChecksum;
    private long _operationCount;

    public ModifierComposeSortedScenario(int modifierCount, int batchSize = 100)
    {
        if (modifierCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(modifierCount));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        _modifierCount = modifierCount;
        _batchSize = batchSize;
        _descriptor = new BenchmarkDescriptor(
            $"modifiers.compose-sorted.count-{modifierCount}",
            "modifiers",
            "modifier-composition",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Package,
                ["ordering"] = "pre-sorted",
                ["magnitude"] = "fixed",
                ["modifierCount"] = modifierCount.ToString(),
                ["batchSize"] = batchSize.ToString()
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        _modifiers = new ModifierData[_modifierCount];
        for (var index = 0; index < _modifiers.Length; index++)
        {
            _modifiers[index] = (index % 3) switch
            {
                0 => ModifierData.Add(ModifierKey.None, 1f + index * 0.01f),
                1 => ModifierData.PercentAdd(ModifierKey.None, 0.01f),
                _ => ModifierData.Mul(ModifierKey.None, 1.001f)
            };
        }

        OperatorComposer.SortByPriority(_modifiers);
        _expectedChecksum = CalculateExpectedChecksum(_batchSize);
    }

    public override void IterationSetup()
    {
        _checksum = 0;
        _operationCount = 0;
    }

    public override long Execute(int invocationCount)
    {
        double checksum = 0;
        for (var invocation = 0; invocation < invocationCount; invocation++)
        {
            for (var index = 0; index < _batchSize; index++)
            {
                var baseValue = (index & 1) == 0 ? 100f : 101f;
                checksum += OperatorComposer.ComposeSorted(_modifiers, baseValue, 1f, null!).FinalValue;
            }
        }

        _checksum = checksum;
        _operationCount = checked((long)invocationCount * _batchSize);
        return _operationCount;
    }

    public override void Validate()
    {
        var passCount = _operationCount / _batchSize;
        var expected = _expectedChecksum * passCount;
        if (Math.Abs(_checksum - expected) > Math.Max(0.001d, expected * 0.000001d))
            throw new InvalidOperationException("Modifier composition checksum is inconsistent.");
    }

    public override string GetDeterminismDigest() =>
        $"compositions={_operationCount};checksum={_checksum:F3}";

    private double CalculateExpectedChecksum(int operationCount)
    {
        double checksum = 0;
        for (var index = 0; index < operationCount; index++)
        {
            var baseValue = (index & 1) == 0 ? 100f : 101f;
            checksum += OperatorComposer.ComposeSorted(_modifiers, baseValue, 1f, null!).FinalValue;
        }
        return checksum;
    }
}
