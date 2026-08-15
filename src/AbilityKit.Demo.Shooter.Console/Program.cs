using AbilityKit.Demo.Host.Console;
using AbilityKit.Demo.Shooter.Runtime;

namespace AbilityKit.Demo.Shooter.Console;

internal static class Program
{
    private static int Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        var platform = new ConsolePlatform();
        var options = ShooterConsoleOptions.Parse(args);
        if (!options.IsValid || options.Mode == ShooterConsoleMode.Menu)
        {
            new ShooterConsoleMenu(platform.Output).Write(options);
            return options.IsValid ? 0 : 1;
        }

        if (options.Mode == ShooterConsoleMode.Local)
        {
            return RunLocal(platform);
        }

        if (options.Mode == ShooterConsoleMode.RemoteSmoke)
        {
            return new ShooterConsoleRemoteSmokeRunner(platform.Output).Run();
        }

        return new ShooterConsoleAcceptanceRunner(platform.Output).Run(options);
    }

    private static int RunLocal(ConsolePlatform platform)
    {
        var game = new ShooterConsoleApp(
            new KeyboardShooterConsoleInputSource(platform.Input),
            new ShooterConsoleRenderer(platform.Output),
            platform.Log);

        var host = new FixedStepConsoleHost(game, ConsoleHostOptions.Default);
        try
        {
            return host.Run();
        }
        finally
        {
            game.Dispose();
        }
    }
}
