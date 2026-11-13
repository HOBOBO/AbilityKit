using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Explicit content selections passed to the existing model animation importer.</summary>
    public static class MobaCharacterAnimationManifestSync
    {
        public static void Apply()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-mobaAnimationSelectionManifest");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Provide -mobaAnimationSelectionManifest.");
            var path = Path.GetFullPath(args[index + 1]);
            var content = File.ReadAllText(path);
            var manifest = JObject.Parse(content);
            if (manifest.Value<int>("schemaVersion") != 1 || !(manifest["animations"] is JArray entries))
                throw new InvalidDataException("Unsupported animation selection manifest.");
            if (entries.Count == 0 || entries.OfType<JObject>().Select(e =>
                e.Value<int>("modelId") + "/" + e.Value<string>("animationKey")).Distinct().Count() != entries.Count)
                throw new InvalidDataException("Empty or duplicate animation selections.");
            var acceptWarnings = args.Contains("-mobaAnimationSyncIncludeTimingWarnings");
            foreach (var group in entries.OfType<JObject>().GroupBy(e => e.Value<int>("modelId")))
            {
                var scan = MobaCharacterAnimationSync.ScanModel(group.Key);
                var selected = group.Select(entry => scan.Rows.Single(row =>
                    row.Target.Value<string>("animationKey") == entry.Value<string>("animationKey"))).ToArray();
                if (scan.Errors.Count > 0 || selected.Any(row => row.Error != null || (!acceptWarnings && row.Warnings.Count > 0)))
                    throw new InvalidDataException("Resolve animation scan errors/warnings before importing model " + group.Key);
                if (File.ReadAllText(path) != content)
                    throw new InvalidDataException("Animation selections changed during import; review and retry.");
                MobaCharacterAnimationSync.Apply(scan, selected, false);
            }
            MobaCharacterAnimationSync.Check();
            Debug.Log("[MobaCharacterAnimationManifestSync] Imported and checked " + entries.Count + " explicit model animation selections.");
        }
    }
}
