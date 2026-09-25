using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Orleans.Contracts.Rooms;

namespace AbilityKit.Orleans.Grains.Rooms;

public static class RoomLaunchManifestBuilder
{
    public const int CurrentManifestVersion = 1;

    /// <summary>
    /// 基于排序后的资源引用集合计算稳定 SHA256 哈希（小写十六进制）。
    /// </summary>
    public static string ComputeHash(IEnumerable<string> assetReferences, IReadOnlyDictionary<string, string>? metadata = null)
    {
        using var sha = SHA256.Create();
        var builder = new StringBuilder();

        foreach (var reference in assetReferences.OrderBy(item => item, StringComparer.Ordinal))
        {
            builder.Append("ref:").Append(reference).Append('\n');
        }

        if (metadata is { Count: > 0 })
        {
            foreach (var kv in metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append("meta:").Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            }
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static RoomLaunchManifest Build(int manifestVersion, IEnumerable<string> assetReferences, IReadOnlyDictionary<string, string>? metadata = null)
    {
        var references = assetReferences.OrderBy(item => item, StringComparer.Ordinal).ToList();
        var meta = metadata is null || metadata.Count == 0 ? null : new Dictionary<string, string>(metadata);
        var hash = ComputeHash(references, meta);
        return new RoomLaunchManifest(manifestVersion, hash, references, meta);
    }
}
