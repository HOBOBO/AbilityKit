using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime.Config.Plans;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 保留现有 JSON 加载入口，将解析转换和计划存储委托给独立组件。
    /// </summary>
    public sealed partial class TriggerPlanJsonDatabase
    {
        /// <summary>
        /// Cue 工厂接口。
        /// 负责将 JSON 中的通用 Cue 描述解析为具体的 ITriggerCue 实例。
        /// </summary>
        public interface ICueFactory
        {
            ITriggerCue Create(in TriggerCueDescriptor descriptor);
        }

        /// <summary>
        /// 默认 Cue 工厂：始终返回 NullTriggerCue
        /// 业务项目可注册自定义实现
        /// </summary>
        public sealed class DefaultCueFactory : ICueFactory
        {
            public static readonly DefaultCueFactory Instance = new DefaultCueFactory();

            private DefaultCueFactory() { }

            public ITriggerCue Create(in TriggerCueDescriptor descriptor)
            {
                return NullTriggerCue.Instance;
            }
        }

        public interface ITextLoader
        {
            bool TryLoad(string id, out string text);
        }

        public readonly struct Record
        {
            public readonly int TriggerId;
            public readonly string EventName;
            public readonly int EventId;
            public readonly TriggerPlanScope Scope;
            public readonly TriggerPlan<object> Plan;
            public readonly ITriggerPlanExecutable ExecutionRoot;

            public Record(int triggerId, string eventName, int eventId, TriggerPlanScope scope, in TriggerPlan<object> plan, ITriggerPlanExecutable executionRoot = null)
            {
                TriggerId = triggerId;
                EventName = eventName;
                EventId = eventId;
                Scope = scope;
                Plan = plan;
                ExecutionRoot = executionRoot;
            }
        }

        private TriggerPlanRecordStore<Record> _store = new(record => (record.TriggerId, record.ExecutionRoot));
        private ICueFactory _cueFactory = DefaultCueFactory.Instance;

        /// <summary>
        /// 表示已加载的计划、字符串和黑板声明的内容修订；运行时注入的记录没有可验证的数据来源时为 null。
        /// </summary>
        public string ContentRevision { get; private set; } = "trigger-plan:empty:1";

        /// <summary>
        /// 表示当前数据库实例的修改代次，用于发现绑定以后发生的替换；不是内容标识。
        /// </summary>
        public long MutationVersion { get; private set; }

        /// <summary>
        /// 按加载或写入顺序返回记录；原始输入可能含重复 ID。
        /// </summary>
        public IReadOnlyList<Record> Records => _store.Records;

        /// <summary>
        /// 返回已加载但尚未由此入口应用到宿主的黑板初始化配置。
        /// </summary>
        public IReadOnlyList<BlackboardInitializationPlan> Blackboards => _store.Blackboards;

        /// <summary>
        /// 获取或设置加载时使用的 Cue 工厂；赋空值恢复默认工厂。
        /// </summary>
        public ICueFactory CueFactory
        {
            get => _cueFactory ?? DefaultCueFactory.Instance;
            set => _cueFactory = value ?? DefaultCueFactory.Instance;
        }

        /// <summary>
        /// 查询非零 ID 对应的字符串。
        /// </summary>
        public bool TryGetString(int id, out string value) => _store.TryGetString(id, out value!);

        /// <summary>
        /// 按 ID 查询索引记录；原始输入重复时返回最后加载的记录。
        /// </summary>
        public bool TryGetRecordByTriggerId(int triggerId, out Record record) => _store.TryGetRecord(triggerId, out record);

        /// <summary>
        /// 按 ID 查询索引记录对应的计划。
        /// </summary>
        public bool TryGetPlanByTriggerId(int triggerId, out TriggerPlan<object> plan)
        {
            if (_store.TryGetRecord(triggerId, out var record))
            {
                plan = record.Plan;
                return true;
            }

            plan = default;
            return false;
        }

        /// <summary>
        /// 查询执行根索引；原始输入重复时索引保留最后一个非空执行根。
        /// 覆盖写入不使用此规则，新记录没有执行根时会清除已有索引。
        /// </summary>
        public bool TryGetExecutionRootByTriggerId(int triggerId, out ITriggerPlanExecutable root) =>
            _store.TryGetRoot(triggerId, out root!);

        /// <summary>
        /// 写入非零 ID 的字符串，按参数决定是否覆盖已有值。
        /// </summary>
        public void AddString(int id, string value, bool replaceExisting = true)
        {
            _store.AddString(id, value, replaceExisting);
            ContentRevision = ContentRevision == null ? null : HashRevision(new JArray(ContentRevision, id, value, replaceExisting));
            MutationVersion++;
        }

        /// <summary>
        /// 写入计划记录；覆盖时替换整条记录并同步更新执行根索引。
        /// </summary>
        public void AddRecord(in Record record, bool replaceExisting = true)
        {
            _store.AddRecord(in record, replaceExisting);
            ContentRevision = null;
            MutationVersion++;
        }

        /// <summary>
        /// 按源库的记录顺序合并字符串、触发器计划和黑板初始化配置。
        /// </summary>
        /// <param name="source">待合并的源库；为空时不修改目标库。</param>
        /// <param name="replaceExisting">为真时同 ID 的后续定义覆盖已有定义；否则保留最先加入的定义。</param>
        /// <remarks>
        /// 此操作不发现文件、不校验配置，也不初始化黑板；由宿主决定输入、合并顺序及校验时机。
        /// </remarks>
        public void MergeFrom(TriggerPlanJsonDatabase source, bool replaceExisting = true)
        {
            if (source == null) return;
            _store.MergeFrom(source._store, replaceExisting);
            ContentRevision = ContentRevision == null || source.ContentRevision == null ? null :
                HashRevision(new JArray(ContentRevision, source.ContentRevision, replaceExisting));
            MutationVersion++;
        }

        /// <summary>
        /// 将已加载的全局黑板初始化配置应用到可写解析器。
        /// </summary>
        public void InitializeBlackboards(IBlackboardResolver resolver, bool replaceExisting = true)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (!(resolver is IMutableBlackboardResolver mutable))
            {
                throw new InvalidOperationException(
                    $"Blackboard resolver must implement {nameof(IMutableBlackboardResolver)} to apply Runtime Plan initialization.");
            }

            BlackboardInitialization.Apply(
                _store.Blackboards,
                mutable,
                BlackboardInitializationScopes.Global,
                replaceExisting);
        }

        /// <summary>
        /// 将已加载的初始化配置交给宿主的所有者黑板存储。
        /// </summary>
        public void ConfigureOwnerBlackboards(IOwnerBlackboardStore store, bool releaseExisting = true)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            store.Configure(_store.Blackboards, releaseExisting);
        }

        /// <summary>
        /// 从指定加载器读取必需的 JSON 文本，并替换当前计划数据。
        /// </summary>
        public void Load(ITextLoader loader, string id)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException(nameof(id));

            if (!loader.TryLoad(id, out var json) || string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Trigger plan json not found or empty: {id}");
            }

            LoadFromJson(json, id);
        }

        /// <summary>
        /// 读取 JSON 并替换记录与索引；解析或转换失败不替换已有存储。
        /// </summary>
        public void LoadFromJson(string json, string sourceName = null)
        {
            var store = TriggerPlanJsonReader.Read(json, sourceName, CreateCue, out var dto);
            Publish(store, dto);
        }

        /// <summary>
        /// 将全局作用域的已加载计划注册到指定运行器。
        /// </summary>
        public void RegisterAll<TCtx>(TriggerRunner<TCtx> runner)
        {
            RegisterAll(runner, TriggerPlanScope.Global);
        }

        /// <summary>
        /// 按记录顺序注册指定作用域的计划，跳过无事件 ID 的记录。
        /// </summary>
        public void RegisterAll<TCtx>(TriggerRunner<TCtx> runner, TriggerPlanScope scope)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            for (int i = 0; i < _store.Records.Count; i++)
            {
                var r = _store.Records[i];
                if (r.EventId == 0) continue;
                if (r.Scope != scope) continue;
                var key = new EventKey<object>(r.EventId);
                runner.RegisterPlan<object, TCtx>(key, r.Plan);
            }
        }

        internal void LoadFromDto(TriggerPlanDatabaseDto dto) => Publish(TriggerPlanJsonReader.Read(dto, CreateCue), dto);

        private void Publish(TriggerPlanRecordStore<Record> store, TriggerPlanDatabaseDto dto)
        {
            var revision = HashRevision(dto == null ? JValue.CreateNull() :
                JToken.FromObject(dto, new JsonSerializer()));
            _store = store;
            ContentRevision = revision;
            MutationVersion++;
        }

        private static string HashRevision(JToken value)
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

        private ITriggerCue CreateCue(TriggerCueDescriptor descriptor) => CueFactory.Create(in descriptor);
    }
}
