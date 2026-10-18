using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbilityKit.Entitas.CodeGen.Configuration;

/// <summary>
/// 组件索引锁定文件：固定每个 context 内组件的 lookup 索引顺序。
/// 生成物字节级复现依赖此顺序（现状 = 字母序 0-38 + 手工追加 39-42，无规律可推导，只能锁定）。
/// </summary>
public sealed class PinFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonSerializerOptions JsonOptionsWrite = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static PinFile LoadOrEmpty(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<PinFile>(File.ReadAllText(path), JsonOptions) ?? new PinFile()
            : new PinFile();

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptionsWrite) + "\n");

    public Dictionary<string, PinContext> Contexts { get; set; } = new(StringComparer.Ordinal);

    public sealed class PinContext
    {
        public List<PinComponent> Components { get; set; } = new();
    }

    public sealed class PinComponent
    {
        /// <summary>组件名（不含 Component 后缀，与 lookup 常量一致）。</summary>
        public string Name { get; set; } = "";

        /// <summary>false = 定义存在但从未生成（如 Shield 双组件，Jenny 死后新增的历史漂移）。
        /// 保留在 pin 里是为了让漂移显式化，而不是静默跳过。</summary>
        public bool Generate { get; set; } = true;

        /// <summary>true = 生成物为手工维护（含 Dispose 等自定义生命周期逻辑），生成器跳过、只做存在性校验。</summary>
        public bool Custom { get; set; }

        /// <summary>
        /// true = 该组件参与生成的逐字段序列化（{Ctx}ComponentSerialization.cs 的 Write/Restore 覆盖集）。
        /// 只应标记"平值可序列化"的组件；blob 组件（List&lt;运行时对象&gt;/管线对象）走专属回滚 provider，不标。
        /// </summary>
        public bool Serialize { get; set; }

        /// <summary>
        /// 回滚覆盖归类（审计强制声明，缺失即报错）：serialize:true 隐含 serialized，无需重复声明。
        /// 可选值：serialized | provider:&lt;ProviderName&gt;（专属回滚 provider 恢复）|
        /// immutable（仅出生时赋值，实体重建路径恢复）| projection（每帧从服务真相源重投影/reconcile）|
        /// transient（每帧被重写后才消费，快照无需恢复）。
        /// </summary>
        public string? Coverage { get; set; }
    }
}
