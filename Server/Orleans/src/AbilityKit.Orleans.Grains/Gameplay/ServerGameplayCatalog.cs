using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;

namespace AbilityKit.Orleans.Grains.Gameplay;

internal static class ServerGameplayDescriptors
{
    public static GameplayRoomDescriptor Moba { get; } = new(
        GameplayRoomTypes.Moba,
        "MOBA Battle",
        DefaultMaxPlayers: 10,
        RequiresPlayerLoadout: true,
        DefaultWorldType: GameplayRoomTypes.Moba,
        DefaultTickRate: 30,
        DefaultSyncTemplateId: "frame-sync-authority");

    public static GameplayRoomDescriptor Shooter { get; } = new(
        ShooterGameplay.RoomType,
        "Shooter State Sync",
        ShooterGameplay.DefaultMaxPlayers,
        RequiresPlayerLoadout: false,
        DefaultWorldType: ShooterGameplay.WorldType,
        DefaultTickRate: ShooterGameplay.DefaultTickRate,
        DefaultSyncTemplateId: ShooterServerProtocol.StateSyncAuthorityTemplate);

}

public sealed class ServerGameplayCatalog
{
    public static ServerGameplayCatalog Default => ServerGameplayModuleCatalog.Default.GameplayCatalog;

    private readonly Dictionary<string, GameplayRoomDescriptor> _descriptors;
    private readonly string _defaultRoomType;

    public ServerGameplayCatalog(IEnumerable<GameplayRoomDescriptor> descriptors)
    {
        if (descriptors is null)
        {
            throw new ArgumentNullException(nameof(descriptors));
        }

        _descriptors = descriptors.ToDictionary(d => d.RoomType, StringComparer.OrdinalIgnoreCase);
        if (_descriptors.Count == 0)
            throw new ArgumentException("At least one gameplay descriptor must be registered.", nameof(descriptors));
        _defaultRoomType = _descriptors.ContainsKey(GameplayRoomTypes.Default)
            ? GameplayRoomTypes.Default
            : _descriptors.Keys.First();
    }

    public GameplayRoomDescriptor DefaultDescriptor => _descriptors[_defaultRoomType];

    public IReadOnlyCollection<GameplayRoomDescriptor> Descriptors => _descriptors.Values;

    public GameplayRoomDescriptor Resolve(string? roomType)
    {
        if (!string.IsNullOrWhiteSpace(roomType) && _descriptors.TryGetValue(roomType, out var descriptor))
        {
            return descriptor;
        }

        return DefaultDescriptor;
    }

    public void EnsureRegistered(string roomType)
    {
        if (string.IsNullOrWhiteSpace(roomType) || !_descriptors.ContainsKey(roomType))
        {
            throw new InvalidOperationException($"Gameplay room type is not registered. RoomType={roomType}");
        }
    }
}
