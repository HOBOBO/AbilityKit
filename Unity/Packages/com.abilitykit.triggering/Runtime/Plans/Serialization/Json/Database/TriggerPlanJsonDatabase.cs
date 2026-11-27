using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        /// 直接加载后按输入顺序返回原始行，可能含重复 ID。
        /// 合库或成功按 ID 写入后，每个 ID 只保留生效记录，位置按 ID 首次出现的顺序排列。
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
        /// 按 ID 查询完整记录；直接加载的输入有重复 ID 时，返回最后加载的一条记录。
        /// </summary>
        /// <remarks>
        /// 运行态应从这条记录同时读取作用域、计划和执行根。执行根为空时，不回退到同 ID 的旧执行根。
        /// </remarks>
        public bool TryGetRecordByTriggerId(int triggerId, out Record record) => _store.TryGetRecord(triggerId, out record);

        /// <summary>
        /// 按 ID 查询索引记录对应的计划；需要作用域或执行根时，应查询完整记录。
        /// </summary>
        public bool TryGetPlanByTriggerId(int triggerId, out TriggerPlan<object> plan)
        {
            if (TryGetRecordByTriggerId(triggerId, out var record))
            {
                plan = record.Plan;
                return true;
            }

            plan = default;
            return false;
        }

        /// <summary>
        /// 查询历史执行根索引；原始输入重复时索引保留最后一个非空执行根。
        /// </summary>
        /// <remarks>
        /// 直接加载重复 ID 时，返回的执行根可能不是索引记录上的执行根，不要用本方法为计划配对。
        /// 合库或成功按 ID 写入后，索引只对应生效记录；无执行根时不再保留历史值。
        /// </remarks>
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
        /// 按 ID 写入整条记录；成功写入时先收敛目标库中的重复 ID，并同步执行根索引。
        /// </summary>
        public void AddRecord(in Record record, bool replaceExisting = true)
        {
            _store.AddRecord(in record, replaceExisting);
            ContentRevision = null;
            MutationVersion++;
        }

        /// <summary>
        /// 合并字符串、触发器计划和黑板初始化配置；计划按 ID 首次出现的顺序合并生效记录。
        /// </summary>
        /// <param name="source">待合并的源库，不会被修改；为空或为当前库时不执行合并。</param>
        /// <param name="replaceExisting">是否用源库的生效记录覆盖目标库已有的同 ID 记录，不改变单库内最后一条生效的规则。</param>
        /// <remarks>
        /// 此操作不发现文件、不校验配置，也不初始化黑板；由宿主决定输入、合并顺序及校验时机。
        /// </remarks>
        public void MergeFrom(TriggerPlanJsonDatabase source, bool replaceExisting = true)
        {
            if (source == null || ReferenceEquals(source, this)) return;
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
