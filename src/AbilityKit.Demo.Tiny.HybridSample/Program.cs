using AbilityKit.Demo.Tiny.Client;
using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Demo.Tiny.View;

if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out var port) ||
    port is < 1 or > 65535)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/AbilityKit.Demo.Tiny.HybridSample -- <host> <gateway-port> [account-prefix]");
    return 2;
}

var prefix = args.Length == 3 ? args[2] : $"tiny-hybrid-{Guid.NewGuid():N}";
try
{
    var hash = TinyHybridExample.Run();
    return await TinySessionSmoke.RunChapterAsync(
        args[0], port, prefix, TinySyncMode.Hybrid, hash);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny Hybrid chapter failed: {exception}");
    return 1;
}
