using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Pipeline;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 携带一次施法实际使用的触发前、施法阶段定义与回滚凭证；阶段实例不在各次施法之间共享。
    /// </summary>
    internal sealed class MobaSkillPipelineDefinition
    {
        internal MobaSkillPipelineDefinition(IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            MobaSkillPipelineRevision revision)
        {
            PreCastConfig = preCastConfig;
            PreCastPhases = preCastPhases;
            CastConfig = castConfig;
            CastPhases = castPhases;
            Revision = revision;
        }

        internal readonly IAbilityPipelineConfig PreCastConfig;
        internal readonly IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> PreCastPhases;
        internal readonly IAbilityPipelineConfig CastConfig;
        internal readonly IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> CastPhases;
        internal readonly MobaSkillPipelineRevision Revision;

        internal static bool TryResolve(IMobaSkillPipelineLibrary library, int skillId, out MobaSkillPipelineDefinition definition)
        {
            if (library is TableDrivenMobaSkillPipelineLibrary table)
                return table.TryGetDefinition(skillId, out definition);

            definition = null;
            if (!library.TryGet(skillId, out var preConfig, out var prePhases, out var castConfig, out var castPhases))
                return false;

            definition = new MobaSkillPipelineDefinition(preConfig, prePhases, castConfig, castPhases, null);
            return true;
        }
    }

    /// <summary>
    /// 保存定义内容修订，并检查运行期间只能原位使用的依赖是否已修改。
    /// </summary>
    internal sealed class MobaSkillPipelineRevision
    {
        private readonly Func<bool> _dependenciesAreCurrent;

        internal MobaSkillPipelineRevision(string value, Func<bool> dependenciesAreCurrent = null)
        {
            Value = value;
            _dependenciesAreCurrent = dependenciesAreCurrent;
        }

        internal readonly string Value;

        internal bool IsCurrent => _dependenciesAreCurrent == null || _dependenciesAreCurrent();

        internal static void RequireMatch(SkillRunnerRollbackEntry entry, MobaSkillPipelineDefinition definition)
        {
            var revision = definition.Revision;
            if (revision == null || !revision.IsCurrent || !string.Equals(entry.DefinitionRevision, revision.Value, StringComparison.Ordinal))
                throw new InvalidOperationException($"Skill rollback definition revision is missing or mismatched. skill={entry.SkillId}, stage={entry.Stage}.");
        }

        internal static JToken Describe(object value)
        {
            return value == null ? JValue.CreateNull() : JToken.FromObject(value, new JsonSerializer());
        }

        internal static string Digest(JToken value)
        {
            using var hash = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(Canonicalize(value).ToString(Formatting.None));
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static JToken Canonicalize(JToken value)
        {
            if (value is JObject obj)
                return new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(property.Name, Canonicalize(property.Value))));
            if (value is JArray array) return new JArray(array.Select(Canonicalize));
            return value;
        }

        internal static bool IsValid(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var digit in value)
                if (!(digit >= '0' && digit <= '9' || digit >= 'A' && digit <= 'F')) return false;
            return true;
        }
    }
}
