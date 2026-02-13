using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AbilityKit.Ability.Share.Common.Config
{
    public static class UnityJsonSettingsBootstrap
    {
        public const string DefaultFileName = "abilitykit.settings.json";

        public static IEnumerator EnsurePersistentCopy(string fileName = null)
        {
            fileName ??= DefaultFileName;

            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) yield break;

            var persistentPath = Path.Combine(persistentBase, fileName);
            if (File.Exists(persistentPath)) yield break;

            var streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
            var url = streamingPath;
            if (!string.IsNullOrEmpty(url) && !url.Contains("://"))
            {
                url = "file://" + url;
            }

            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                yield break;
            }

            var json = req.downloadHandler != null ? req.downloadHandler.text : null;
            if (string.IsNullOrEmpty(json)) yield break;

            try
            {
                var dir = Path.GetDirectoryName(persistentPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(persistentPath, json);
            }
            catch
            {
                yield break;
            }
        }

        public static IEnumerator LoadPersistentInto(LayeredJsonSettingsStore store, Func<string, Dictionary<string, object>> deserialize, string fileName = null)
        {
            if (store == null) yield break;

            fileName ??= DefaultFileName;

            yield return EnsurePersistentCopy(fileName);

            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) yield break;
            var persistentPath = Path.Combine(persistentBase, fileName);
            var settings = JsonSettingsFiles.LoadFlatOrEmpty(persistentPath, deserialize);
            store.ReplacePersistent(settings);
        }

        public static bool TrySaveOverridesToPersistent(IReadOnlyDictionary<string, object> overrides, Func<IReadOnlyDictionary<string, object>, string> serialize, string fileName = null)
        {
            if (serialize == null) return false;
            fileName ??= DefaultFileName;
            var persistentBase = Application.persistentDataPath;
            if (string.IsNullOrEmpty(persistentBase)) return false;
            var persistentPath = Path.Combine(persistentBase, fileName);
            return JsonSettingsFiles.TrySaveOverrides(persistentPath, overrides, serialize);
        }
    }
}
