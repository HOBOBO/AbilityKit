#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Logic.Ability.Core.Pipeline;

namespace AbilityKit.Samples.Logic.Ability.Samples.Pipeline
{
    /// <summary>
    /// 技能上下文，存储技能执行时的状态数据。
    /// </summary>
    public sealed class SkillContext : IPipelineContext
    {
        private readonly int _contextId;
        private readonly Dictionary<string, object> _data;

        public SkillContext(int contextId)
        {
            _contextId = contextId;
            _data = new Dictionary<string, object>();
        }

        public int ContextId => _contextId;

        public int SkillId { get; set; }

        public int CasterId { get; set; }

        public int? TargetId { get; set; }

        public T? GetData<T>(string key)
        {
            return _data.TryGetValue(key, out var value) ? (T?)value : default;
        }

        public void SetData<T>(string key, T value)
        {
            _data[key] = value!;
        }

        public bool HasData(string key)
        {
            return _data.ContainsKey(key);
        }

        public void RemoveData(string key)
        {
            _data.Remove(key);
        }
    }
}
