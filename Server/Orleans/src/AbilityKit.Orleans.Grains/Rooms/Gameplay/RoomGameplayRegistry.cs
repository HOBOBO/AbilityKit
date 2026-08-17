using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Gameplay;

namespace AbilityKit.Orleans.Grains.Rooms.Gameplay;

internal sealed class RoomGameplayRegistry
{
    private readonly Dictionary<string, IRoomGameplayAdapter> _adapters;
    private readonly IRoomGameplayAdapter _defaultAdapter;

    public RoomGameplayRegistry()
        : this(ServerGameplayModuleCatalog.Default.CreateRoomAdapters(), ServerGameplayCatalog.Default)
    {
    }

    public RoomGameplayRegistry(IEnumerable<IRoomGameplayAdapter> adapters, ServerGameplayCatalog catalog)
    {
        if (adapters is null)
        {
            throw new ArgumentNullException(nameof(adapters));
        }

        if (catalog is null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        _adapters = adapters.ToDictionary(a => a.RoomType, StringComparer.OrdinalIgnoreCase);
        foreach (var roomType in _adapters.Keys)
        {
            catalog.EnsureRegistered(roomType);
        }

        var defaultRoomType = catalog.DefaultDescriptor.RoomType;
        if (!_adapters.TryGetValue(defaultRoomType, out _defaultAdapter!))
        {
            throw new InvalidOperationException($"Default room gameplay adapter is not registered. RoomType={defaultRoomType}");
        }
    }

    public IRoomGameplayAdapter Resolve(string? roomType)
    {
        if (!string.IsNullOrWhiteSpace(roomType) &&
            _adapters.TryGetValue(GameplayRoomTypes.Normalize(roomType), out var adapter))
        {
            return adapter;
        }

        return _defaultAdapter;
    }
}
