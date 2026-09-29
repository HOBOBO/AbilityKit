using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Grains.Rooms;

public static class RoomLaunchManifestBuilder
{
    public const int CurrentManifestVersion = 1;

    /// <summary>
    /// 基于排序后的资源引用集合计算稳定 SHA256 哈希（小写十六进制）。
    /// </summary>
    public static string ComputeHash(IEnumerable<string> assetReferences, IReadOnlyDictionary<string, string>? metadata = null)
        => RoomLaunchManifestHash.Compute(assetReferences, metadata);

    public static RoomLaunchManifest Build(int manifestVersion, IEnumerable<string> assetReferences, IReadOnlyDictionary<string, string>? metadata = null)
    {
        var references = assetReferences.OrderBy(item => item, StringComparer.Ordinal).ToList();
        var meta = metadata is null || metadata.Count == 0 ? null : new Dictionary<string, string>(metadata);
        var hash = ComputeHash(references, meta);
        return new RoomLaunchManifest(manifestVersion, hash, references, meta);
    }
}
