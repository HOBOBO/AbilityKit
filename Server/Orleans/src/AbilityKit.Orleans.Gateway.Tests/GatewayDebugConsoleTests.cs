using System;
using System.IO;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class GatewayDebugConsoleTests
{
    [Fact]
    public void Debug_console_should_default_to_shooter_web_flow()
    {
        var html = File.ReadAllText(GetDebugConsolePath());

        Assert.Contains("selectedRoomType: 'shooter'", html);
        Assert.Contains("roomType: 'shooter'", html);
        Assert.Contains("gameplayId: 2", html);
        Assert.Contains("worldType: 'shooter_battle'", html);
        Assert.Contains("syncTemplateId: 'state-sync-authority'", html);
        Assert.Contains("maxPlayers: 4", html);
    }

    [Fact]
    public void Debug_console_should_use_gameplay_manifest_sync_templates()
    {
        var html = File.ReadAllText(GetDebugConsolePath());

        Assert.Contains("selectedGameplay.supportedSyncTemplateIds", html);
        Assert.Contains("battle.gameplayId = Number(gameplay.gameplayId", html);
        Assert.Contains("this.battle.syncTemplateId = gameplay.defaultSyncTemplateId || ''", html);
    }

    [Fact]
    public void Debug_console_should_expose_admin_room_actions()
    {
        var html = File.ReadAllText(GetDebugConsolePath());

        Assert.Contains("/api/rooms/mark-offline", html);
        Assert.Contains("/api/rooms/close", html);
        Assert.Contains("startShooterRoomQuick", html);
        Assert.Contains("Shooter 快速开战", html);
    }

    private static string GetDebugConsolePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "Server",
                "Orleans",
                "src",
                "AbilityKit.Orleans.Gateway",
                "wwwroot",
                "debug",
                "index.html");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Gateway debug console index.html from test output directory.");
    }
}
