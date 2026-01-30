using System;
using System.Linq;
using UnityEditor;

public sealed class WorldDiCsprojPostprocessor : AssetPostprocessor
{
    private static string OnGeneratedCSProject(string path, string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

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
