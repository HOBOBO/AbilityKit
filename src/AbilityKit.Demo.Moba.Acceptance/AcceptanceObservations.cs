using System.Globalization;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>Runtime values exposed by a carrier to the platform-neutral verifier.</summary>
public sealed record AcceptanceObservation(
    string Alias,
    string? ActorId,
    string? Kind,
    string Property,
    object? Value);

public sealed class AcceptanceObservations
{
    public IReadOnlyList<AcceptanceObservation> States { get; init; } = Array.Empty<AcceptanceObservation>();
    public IReadOnlyList<AcceptanceObservation> Contexts { get; init; } = Array.Empty<AcceptanceObservation>();
}

public interface IAcceptanceObservationSource
{
    AcceptanceObservations Capture(TestScenario scenario);
}

internal static class AcceptanceObservationMatcher
{
    public static (int matched, string missing) Match(
        MobaAcceptanceStateExpectation[]? expectations,
        IReadOnlyList<AcceptanceObservation> observations)
    {
        if (expectations is null || expectations.Length == 0) return (0, string.Empty);
        var missing = new List<string>();
        var matched = 0;
        foreach (var expected in expectations)
        {
            var found = observations.Any(actual =>
                Same(actual.Alias, expected.alias) &&
                (string.IsNullOrEmpty(expected.actorId) || Same(actual.ActorId, expected.actorId)) &&
                Same(actual.Property, expected.property) &&
                Compare(actual.Value, expected.comparator, ExpectedValue(expected), expected.tolerance));
            if (found) matched++;
            else missing.Add(Format(expected.alias, expected.property, expected.comparator, ExpectedValue(expected)));
        }
        return (matched, string.Join(",", missing));
    }

    public static (int matched, string missing) Match(
        MobaAcceptanceContextExpectation[]? expectations,
        IReadOnlyList<AcceptanceObservation> observations)
    {
        if (expectations is null || expectations.Length == 0) return (0, string.Empty);
        var missing = new List<string>();
        var matched = 0;
        foreach (var expected in expectations)
        {
            var found = observations.Any(actual =>
                Same(actual.Alias, expected.alias) &&
                (string.IsNullOrEmpty(expected.actorId) || Same(actual.ActorId, expected.actorId)) &&
                (string.IsNullOrEmpty(expected.kind) || Same(actual.Kind, expected.kind)) &&
                Same(actual.Property, expected.property) &&
                Compare(actual.Value, expected.comparator, ExpectedValue(expected)));
            if (found) matched++;
            else missing.Add(Format(expected.alias, expected.property, expected.comparator, ExpectedValue(expected)));
        }
        return (matched, string.Join(",", missing));
    }

    private static object? ExpectedValue(MobaAcceptanceStateExpectation e)
        => !string.IsNullOrEmpty(e.expectedValue) ? e.expectedValue
         : e.expectedVector is not null ? $"{e.expectedVector.x.ToString(CultureInfo.InvariantCulture)},{e.expectedVector.y.ToString(CultureInfo.InvariantCulture)},{e.expectedVector.z.ToString(CultureInfo.InvariantCulture)}"
         : e.property is "hasBuff" or "buff" ? e.expectedBool
         : e.expectedFloat != 0 ? e.expectedFloat
         : e.expectedInt != 0 ? e.expectedInt
         : e.expectedBool;

    private static object? ExpectedValue(MobaAcceptanceContextExpectation e)
        => !string.IsNullOrEmpty(e.expectedValue) ? e.expectedValue
         : e.expectedFloat != 0 ? e.expectedFloat
         : e.expectedInt != 0 ? e.expectedInt
         : e.expectedBool;

    private static bool Compare(object? actual, string? comparator, object? expected, MobaAcceptanceVector3Expectation? tolerance = null)
    {
        comparator = string.IsNullOrEmpty(comparator) ? "eq" : comparator!.ToLowerInvariant();
        if (actual is TestVector3 av && expected is string vectorText && TryVector(vectorText, out var ev))
        {
            var tx = tolerance?.x ?? 0.001f;
            var ty = tolerance?.y ?? 0.001f;
            var tz = tolerance?.z ?? 0.001f;
            return comparator is "eq" or "equals"
                && Math.Abs(av.X - ev.X) <= tx && Math.Abs(av.Y - ev.Y) <= ty && Math.Abs(av.Z - ev.Z) <= tz;
        }
        if (comparator is "contains") return actual?.ToString()?.Contains(expected?.ToString() ?? string.Empty, StringComparison.Ordinal) == true;
        if (!TryNumber(actual, out var a) || !TryNumber(expected, out var e))
        {
            var text = string.Equals(actual?.ToString(), expected?.ToString(), StringComparison.OrdinalIgnoreCase);
            return comparator switch
            {
                "eq" or "equals" => text,
                "ne" or "notEqual" => !text,
                _ => false,
            };
        }
        return comparator switch
        {
            "eq" or "equals" => Math.Abs(a - e) < 0.0001,
            "ne" or "notEqual" => Math.Abs(a - e) >= 0.0001,
            "gt" => a > e,
            "gte" or "ge" => a >= e,
            "lt" => a < e,
            "lte" or "le" => a <= e,
            _ => false,
        };
    }

    private static bool TryNumber(object? value, out double result)
    {
        if (value is bool) { result = 0; return false; }
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryVector(string text, out TestVector3 result)
    {
        var parts = text.Split(',');
        if (parts.Length == 3
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            result = new TestVector3(x, y, z);
            return true;
        }
        result = default;
        return false;
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string Format(string? alias, string? property, string? comparator, object? value) =>
        $"{alias}.{property}{comparator ?? "eq"}{value}";
}
