using System;
using System.Linq;
using UnityEditor;

public sealed class WorldDiCsprojPostprocessor : AssetPostprocessor
{
    private static string OnGeneratedCSProject(string path, string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        if (!string.IsNullOrEmpty(path) && path.EndsWith("AbilityKit.Host.Extensions.csproj", StringComparison.OrdinalIgnoreCase))
        {
            const string needle = "Packages\\com.abilitykit.host.extension\\Runtime\\FrameSync\\IClientPredictionTuningControl.cs";
            const string anchor = "Packages\\com.abilitykit.host.extension\\Runtime\\FrameSync\\IClientPredictionDriverStats.cs";

            var hasNeedle = lines.Any(l => l != null && l.Contains(needle));
            if (!hasNeedle)
            {
                var insertAt = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    var l = lines[i];
                    if (l != null && l.Contains(anchor) && l.Contains("<Compile"))
                    {
                        insertAt = i + 1;
                        break;
                    }
                }

                if (insertAt >= 0)
                {
                    var list = lines.ToList();
                    list.Insert(insertAt, "    <Compile Include=\"" + needle + "\" />");
                    lines = list.ToArray();
                }
            }
        }

        if (!string.IsNullOrEmpty(path) && path.EndsWith("AbilityKit.Protocol.Moba.csproj", StringComparison.OrdinalIgnoreCase))
        {
            const string anchor = "Packages\\com.abilitykit.protocol.moba\\Runtime\\Generated\\GatewayFrameSync\\WireCustomBinary.g.cs";

            var need1 = "Packages\\com.abilitykit.protocol.moba\\Runtime\\GatewayTimeSync\\OpCodes.cs";
            var need2 = "Packages\\com.abilitykit.protocol.moba\\Runtime\\GatewayTimeSync\\WireTimeSyncTypes.cs";
            var need3 = "Packages\\com.abilitykit.protocol.moba\\Runtime\\GatewayTimeSync\\WireTimeSyncBinary.cs";

            bool Has(string n) => lines.Any(l => l != null && l.Contains(n));

            if (!Has(need1) || !Has(need2) || !Has(need3))
            {
                var insertAt = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    var l = lines[i];
                    if (l != null && l.Contains(anchor) && l.Contains("<Compile"))
                    {
                        insertAt = i + 1;
                        break;
                    }
                }

                if (insertAt >= 0)
                {
                    var list = lines.ToList();
                    if (!Has(need1)) list.Insert(insertAt++, "    <Compile Include=\"" + need1 + "\" />");
                    if (!Has(need2)) list.Insert(insertAt++, "    <Compile Include=\"" + need2 + "\" />");
                    if (!Has(need3)) list.Insert(insertAt++, "    <Compile Include=\"" + need3 + "\" />");
                    lines = list.ToArray();
                }
            }
        }

        bool ShouldRemove(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (!line.Contains("IWorldServices.cs")) return false;
            if (!line.Contains("<Compile")) return false;
            return true;
        }

        var filtered = lines.Where(l => !ShouldRemove(l));
        return string.Join("\n", filtered);
    }
}
