using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbilityKit.GameplayTags.Editor
{
    internal static class GameplayTagJsonExporter
    {
        [Serializable]
        private struct JsonConfig
        {
            public string version;
            public List<JsonTagEntry> tags;
        }

        [Serializable]
        private struct JsonTagEntry
        {
            public string name;
            public string description;
            public string category;
        }

        public static string ExportToJson(GameplayTagDatabase db)
        {
            if (db == null) return "{}";

            var entries = db.Entries ?? Array.Empty<GameplayTagDatabase.Entry>();
            var tagEntries = new List<JsonTagEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (GameplayTagValidator.TryNormalize(e.Name, out _))
                {
                    tagEntries.Add(new JsonTagEntry
                    {
                        name = e.Name,
                        description = e.Comment ?? string.Empty,
                        category = e.Category ?? string.Empty
                    });
                }
            }

            tagEntries.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            var config = new JsonConfig
            {
                version = "1.0",
                tags = tagEntries
            };

            return JsonUtility.ToJson(config, true);
        }

        public static List<GameplayTagDatabase.Entry> ParseFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<GameplayTagDatabase.Entry>();

            try
            {
                var config = JsonUtility.FromJson<JsonConfig>(json);
                if (config.tags == null) return new List<GameplayTagDatabase.Entry>();

                var entries = new List<GameplayTagDatabase.Entry>();
                for (int i = 0; i < config.tags.Count; i++)
                {
                    var t = config.tags[i];
                    if (string.IsNullOrWhiteSpace(t.name)) continue;
                    if (GameplayTagValidator.TryNormalize(t.name, out _))
                    {
                        entries.Add(new GameplayTagDatabase.Entry(
                            t.name,
                            t.description ?? string.Empty,
                            t.category ?? string.Empty
                        ));
                    }
                }

                return entries;
            }
            catch (Exception)
            {
                return new List<GameplayTagDatabase.Entry>();
            }
        }

        public static void ExportToFile(GameplayTagDatabase db, string path)
        {
            if (db == null) return;
            var json = ExportToJson(db);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        public static void ImportFromFile(GameplayTagDatabase db, string path)
        {
            if (db == null || !File.Exists(path)) return;
            var json = File.ReadAllText(path, Encoding.UTF8);
            var entries = ParseFromJson(json);

            foreach (var entry in entries)
            {
                db.GetOrCreate(entry.Name);
                if (db.TryGetEntry(entry.Name, out var e))
                {
                    e.Comment = entry.Comment;
                    e.Category = entry.Category;
                }
            }
            db.SortAndDedup();
        }
    }
}
