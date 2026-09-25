using AbilityKit.Demo.Tiny.Samples;

Console.WriteLine($"04 Frame replay passed: hash={TinyFrameExample.Run()}");
Console.WriteLine($"05 Hybrid prediction passed: hash={TinyHybridExample.Run()}");
Console.WriteLine($"06 Full recovery passed: hash={TinyRecoveryExample.Run()}");
