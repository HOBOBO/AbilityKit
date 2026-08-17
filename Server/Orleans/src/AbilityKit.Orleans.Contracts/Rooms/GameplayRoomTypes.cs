namespace AbilityKit.Orleans.Contracts.Rooms;

/// <summary>
/// 网关、房间 Grain 和战斗运行时选择共享的玩法房间类型标识。
/// </summary>
public static class GameplayRoomTypes
{
    public const string Moba = "battle";

    public const string LegacyMoba = "moba";

    public const string Default = Moba;

    public static bool IsMoba(string? roomType)
    {
        return string.Equals(roomType, Moba, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roomType, LegacyMoba, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string roomType)
    {
        if (string.IsNullOrWhiteSpace(roomType))
        {
            throw new ArgumentException("RoomType is required", nameof(roomType));
        }

        return IsMoba(roomType) ? Moba : roomType;
    }
}
