using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Editor
{
    /// <summary>
    /// 配置验证工具，用于验证配置加载和完整性
    /// </summary>
    public static class ConfigValidator
    {
        /// <summary>
        /// 验证结果
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
                sb.AppendLine(IsSuccess ? "✓ Validation Passed" : "✗ Validation Failed");

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
        /// 验证模式
        /// </summary>
        [Flags]
        public enum ValidationMode
        {
            None = 0,
            LoadCheck = 1 << 0,           // 加载检查
            FormatCheck = 1 << 1,         // 格式检查
            RequiredCheck = 1 << 2,       // 必填检查
            ReferenceCheck = 1 << 3,       // 引用检查
            All = LoadCheck | FormatCheck | RequiredCheck | ReferenceCheck
        }

        /// <summary>
        /// 从 Resources 目录验证配置
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

            // 检查每个表
            CheckTableLoaded(db, result, MobaConfigPaths.CharactersFile, "Characters");
            CheckTableLoaded(db, result, MobaConfigPaths.SkillsFile, "Skills");
            CheckTableLoaded(db, result, MobaConfigPaths.BuffsFile, "Buffs");
            CheckTableLoaded(db, result, MobaConfigPaths.AttributeTemplatesFile, "AttributeTemplates");

            return result;
        }

        /// <summary>
        /// 从 TextSink 验证配置
        /// </summary>
        public static ValidationResult ValidateFromTextSink(IMobaConfigTextSink sink, string resourcesDir = null, ValidationMode mode = ValidationMode.All)
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
        /// 从字节数据验证配置
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
        /// 从混合数据源验证配置
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
        /// 从配置组验证配置
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
        /// 创建内存 TextSink 用于测试
        /// </summary>
        public static IMobaConfigTextSink CreateTextSinkFromDictionary(IReadOnlyDictionary<string, string> texts)
        {
            return new DictionaryMobaConfigTextSink(texts);
        }
    }
}
