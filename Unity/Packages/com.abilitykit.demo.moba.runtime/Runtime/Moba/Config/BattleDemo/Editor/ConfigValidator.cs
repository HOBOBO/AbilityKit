using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;
using MO = AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using BattleDemo = AbilityKit.Demo.Moba.Config.BattleDemo;

namespace AbilityKit.Demo.Moba.Config.Editor
{
    /// <summary>
    /// 閰嶇疆楠岃瘉宸ュ叿锛岀敤浜庨獙璇侀厤缃姞杞藉拰瀹屾暣鎬?
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// 楠岃瘉缁撴灉
        /// </summary>
        public class ValidationResult
        {
            public bool IsSuccess { get; internal set; }
            public List<ValidationError> Errors { get; } = new List<ValidationError>();
            public List<ValidationWarning> Warnings { get; } = new List<ValidationWarning>();
            public List<string> LoadedTables { get; } = new List<string>();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine(IsSuccess ? "鉁?Validation Passed" : "鉁?Validation Failed");

                foreach (var e in Errors)
                    sb.AppendLine($"  [ERROR] {e.TableName}: {e.Message}");
                foreach (var w in Warnings)
                    sb.AppendLine($"  [WARN] {w.TableName}: {w.Message}");

                sb.AppendLine($"Loaded {LoadedTables.Count} tables");
                return sb.ToString();
            }
        }

        public class ValidationError
        {
            public string TableName { get; }
            public string Message { get; }
            public Exception Exception { get; }

            public ValidationError(string tableName, string message, Exception ex = null)
            {
                TableName = tableName;
                Message = message;
                Exception = ex;
            }
        }

        public class ValidationWarning
        {
            public string TableName { get; }
            public string Message { get; }

            public ValidationWarning(string tableName, string message)
            {
                TableName = tableName;
                Message = message;
            }
        }

        /// <summary>
        /// 楠岃瘉妯″紡
        /// </summary>
        [Flags]
        public enum ValidationMode
        {
            None = 0,
            LoadCheck = 1 << 0,           // 鍔犺浇妫€鏌?
            FormatCheck = 1 << 1,         // 鏍煎紡妫€鏌?
            RequiredCheck = 1 << 2,       // 蹇呭～妫€鏌?
            ReferenceCheck = 1 << 3,       // 寮曠敤妫€鏌?
            All = LoadCheck | FormatCheck | RequiredCheck | ReferenceCheck
        }

        /// <summary>
        /// 浠?Resources 鐩綍楠岃瘉閰嶇疆
        /// </summary>
        public static ValidationResult ValidateFromResources(string resourcesDir, ValidationMode mode = ValidationMode.All)
        {
            var result = new ValidationResult { IsSuccess = true };

            var db = new MobaConfigDatabase();
            try
            {
                if (mode.HasFlag(ValidationMode.LoadCheck))
                {
                    db.LoadFromResources(resourcesDir);
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Errors.Add(new ValidationError("ALL", $"Failed to load configs: {ex.Message}", ex));
                return result;
            }

            // 妫€鏌ユ瘡涓〃
            CheckTableLoaded(db, result, MobaConfigPaths.CharactersFile, "Characters");
            CheckTableLoaded(db, result, MobaConfigPaths.SkillsFile, "Skills");
            CheckTableLoaded(db, result, MobaConfigPaths.BuffsFile, "Buffs");
            CheckTableLoaded(db, result, MobaConfigPaths.AttributeTemplatesFile, "AttributeTemplates");

            return result;
        }

        /// <summary>
        /// 浠?TextSink 楠岃瘉閰嶇疆
        /// </summary>
        public static ValidationResult ValidateFromTextSink(IConfigTextSink sink, string resourcesDir = null, ValidationMode mode = ValidationMode.All)
        {
            var result = new ValidationResult { IsSuccess = true };

            var db = new MobaConfigDatabase();
            try
            {
                db.LoadFromTextSink(sink, resourcesDir);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Errors.Add(new ValidationError("ALL", $"Failed to load configs: {ex.Message}", ex));
                return result;
            }

            return result;
        }

        /// <summary>
        /// 浠庡瓧鑺傛暟鎹獙璇侀厤缃?
        /// </summary>
        public static ValidationResult ValidateFromBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string resourcesDir = null, ValidationMode mode = ValidationMode.All)
        {
            var result = new ValidationResult { IsSuccess = true };

            var db = new MobaConfigDatabase();
            try
            {
                db.LoadFromBytes(bytesByKey, resourcesDir);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Errors.Add(new ValidationError("ALL", $"Failed to load configs: {ex.Message}", ex));
                return result;
            }

            return result;
        }

        /// <summary>
        /// 浠庢贩鍚堟暟鎹簮楠岃瘉閰嶇疆
        /// </summary>
        public static ValidationResult ValidateFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> jsonByKey,
            string bytesResourcesDir = null,
            string jsonResourcesDir = null,
            ValidationMode mode = ValidationMode.All)
        {
            var result = new ValidationResult { IsSuccess = true };

            var db = new MobaConfigDatabase();
            try
            {
                db.LoadFromMixed(bytesByKey, jsonByKey, bytesResourcesDir, jsonResourcesDir);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Errors.Add(new ValidationError("ALL", $"Failed to load configs: {ex.Message}", ex));
                return result;
            }

            return result;
        }

        /// <summary>
        /// 浠庨厤缃粍楠岃瘉閰嶇疆
        /// </summary>
        public static ValidationResult ValidateFromGroups(IReadOnlyList<IConfigGroup> groups, ValidationMode mode = ValidationMode.All)
        {
            var result = new ValidationResult { IsSuccess = true };

            var db = new MobaConfigDatabase();
            try
            {
                db.LoadFromGroups(groups);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Errors.Add(new ValidationError("ALL", $"Failed to load configs from groups: {ex.Message}", ex));
                return result;
            }

            return result;
        }

        private static void CheckTableLoaded(MobaConfigDatabase db, ValidationResult result, string pathKey, string displayName)
        {
            try
            {
                switch (displayName)
                {
                    case "Characters":
                        var chars = db.GetTable<MO.CharacterMO>();
                        if (chars != null)
                        {
                            result.LoadedTables.Add($"{displayName}: {chars.Count} entries");
                        }
                        else
                        {
                            result.Warnings.Add(new ValidationWarning(displayName, "Table is null"));
                        }
                        break;

                    case "Skills":
                        var skills = db.GetTable<MO.SkillMO>();
                        if (skills != null)
                        {
                            result.LoadedTables.Add($"{displayName}: {skills.Count} entries");
                        }
                        break;

                    case "Buffs":
                        var buffs = db.GetTable<MO.BuffMO>();
                        if (buffs != null)
                        {
                            result.LoadedTables.Add($"{displayName}: {buffs.Count} entries");
                        }
                        break;

                    case "AttributeTemplates":
                        var attrs = db.GetTable<MO.BattleAttributeTemplateMO>();
                        if (attrs != null)
                        {
                            result.LoadedTables.Add($"{displayName}: {attrs.Count} entries");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ValidationError(displayName, $"Failed to access table: {ex.Message}", ex));
                result.IsSuccess = false;
            }
        }

        /// <summary>
        /// 鍒涘缓鍐呭瓨 TextSink 鐢ㄤ簬娴嬭瘯
        /// </summary>
        public static IConfigTextSink CreateTextSinkFromDictionary(IReadOnlyDictionary<string, string> texts)
        {
            return new DictionaryConfigTextSink(texts);
        }
    }
}
