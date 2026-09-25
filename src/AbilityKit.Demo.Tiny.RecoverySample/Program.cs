using AbilityKit.Demo.Tiny.Client;
using AbilityKit.Demo.Tiny.Samples;

if (args.Length == 1 && args[0] is "history" or "overflow" or "mismatch")
{
    try
    {
        Console.WriteLine(RunLocal(args[0]));
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Tiny Recovery {args[0]} failed: {exception}");
        return 1;
    }
}

if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out var port) ||
    port is < 1 or > 65535)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/AbilityKit.Demo.Tiny.RecoverySample -- " +
        "<host> <gateway-port> [account-prefix] | history | overflow | mismatch");
    return 2;
}

var prefix = args.Length == 3 ? args[2] : $"tiny-recovery-{Guid.NewGuid():N}";
try
{
    foreach (var scenario in new[] { "history", "overflow", "mismatch" })
        Console.WriteLine(RunLocal(scenario));
    var result = await TinySessionSmoke.RunAsync(args[0], port, prefix, "frame");
    if (result != 0) return result;
    Console.WriteLine("06 disconnect passed: clients=2, mode=frame, reconnect=full");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny Recovery chapter failed: {exception}");
    return 1;
}

static string RunLocal(string scenario) => scenario switch
{
    "history" => $"06 history-exhaustion passed: capacity=2, requested=full, " +
        $"finalHash={TinyRecoveryExample.Run()}, resumed=true",
    "overflow" => TinyRecoveryEvidence.RunOverflow(),
    "mismatch" => TinyRecoveryEvidence.RunSnapshotMismatch(),
    _ => throw new ArgumentOutOfRangeException(nameof(scenario))
};
