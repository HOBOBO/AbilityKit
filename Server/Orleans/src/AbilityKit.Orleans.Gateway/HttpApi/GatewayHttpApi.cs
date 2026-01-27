using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Rooms;
using Orleans;

namespace AbilityKit.Orleans.Gateway.HttpApi;

public static class GatewayHttpApi
{
    public static void MapGatewayHttpApi(this WebApplication app)
    {
        app.MapPost("/api/guest/login", async (IClusterClient client) =>
        {
            var session = client.GetGrain<ISessionGrain>("global");
            var resp = await session.CreateGuestAsync();
            return Results.Ok(resp);
        });

        app.MapPost("/api/rooms/create", async (CreateRoomHttpRequest wire, IClusterClient client) =>
        {
            if (wire is null || string.IsNullOrWhiteSpace(wire.SessionToken))
            {
                return Results.BadRequest("SessionToken is required");
            }

            var session = client.GetGrain<ISessionGrain>("global");
            var v = await session.ValidateAsync(new ValidateSessionRequest(wire.SessionToken));
            if (!v.IsValid || string.IsNullOrWhiteSpace(v.AccountId))
            {
                return Results.BadRequest("Invalid session");
            }

            if (string.IsNullOrWhiteSpace(wire.Region) || string.IsNullOrWhiteSpace(wire.ServerId) || string.IsNullOrWhiteSpace(wire.RoomType))
            {
                return Results.BadRequest("Region/ServerId/RoomType are required");
            }

            var directoryKey = $"{wire.Region}:{wire.ServerId}";
            var directory = client.GetGrain<IRoomDirectoryGrain>(directoryKey);

            var req = new CreateRoomRequest(
                v.AccountId,
                wire.Region,
                wire.ServerId,
                wire.RoomType,
                wire.Title ?? string.Empty,
                wire.IsPublic,
                wire.MaxPlayers,
                wire.Tags == null ? null : new Dictionary<string, string>(wire.Tags));

            var resp = await directory.CreateRoomAsync(req);
            return Results.Ok(resp);
        });

        app.MapPost("/api/rooms/join", async (JoinRoomHttpRequest wire, IClusterClient client) =>
        {
            if (wire is null || string.IsNullOrWhiteSpace(wire.SessionToken) || string.IsNullOrWhiteSpace(wire.RoomId))
            {
                return Results.BadRequest("SessionToken and RoomId are required");
            }

            var session = client.GetGrain<ISessionGrain>("global");
            var v = await session.ValidateAsync(new ValidateSessionRequest(wire.SessionToken));
            if (!v.IsValid || string.IsNullOrWhiteSpace(v.AccountId))
            {
                return Results.BadRequest("Invalid session");
            }

            var room = client.GetGrain<IRoomGrain>(wire.RoomId);
            await room.JoinAsync(v.AccountId);
            var snapshot = await room.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });
    }

    public sealed record CreateRoomHttpRequest(
        string SessionToken,
        string Region,
        string ServerId,
        string RoomType,
        string? Title,
        bool IsPublic,
        int MaxPlayers,
        IReadOnlyDictionary<string, string>? Tags);

    public sealed record JoinRoomHttpRequest(
        string SessionToken,
        string RoomId);
}
