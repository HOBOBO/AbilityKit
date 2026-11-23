# 本地状态提交

在 Unity Package Manager 中导入 `Local State Commit`。示例用进程内类型化值模拟状态下发，展示宿主如何先校验候选，再导入隔离结果，成功后才一起发布状态和提交游标；无需启动场景或会话。

```csharp
using AbilityKit.Ability.StateSync.Samples;

var host = new LocalStateCommitExample();
var candidate = new LocalStateCommitExample.StateSample(frame: 0, revision: 1, value: 10);

bool failed = !host.TryCommit(candidate, RejectImport);
bool stillEmpty = !host.TryGetCommitted(out _);
bool retried = host.TryCommit(candidate, AcceptImport);
bool published = host.TryGetCommitted(out var committed);

static bool RejectImport(int candidateValue, out int importedValue)
{
    importedValue = candidateValue;
    return false;
}

static bool AcceptImport(int candidateValue, out int importedValue)
{
    importedValue = candidateValue;
    return true;
}
```

四个布尔值应均为 `true`，`committed` 的帧、修订和值分别是 `0`、`1`、`10`。宿主从无提交开始，不用帧零作为空值。帧不得倒退，修订号必须为正且严格递增；同帧可以接受更高修订。非法、重复或迟到的候选不调用导入。导入失败或抛异常不替换上次结果，失败的修订可以重试；异常由调用方处理，导入回调不得重入提交。

示例只处理单线程、单权威作用域的值类型结果，不提供真实世界导入的事务或自动回滚。项目宿主必须自行隔离候选和副作用，并管理作用域切换、固定基线重建和恢复。预测只消费已提交的结果；提交游标不等于输入终结证明，也不等于帧同步确认。需要保存快照元数据时，从成功提交的结果派生记录，参见[本地快照历史](../LocalSnapshotHistory/README.md)。

验收只使用本仓库和测试进程内的类型化对象：不启动网络服务、不抓包、不查询线上水位、不读写或解析网络载荷，也不检查项目外的进程或对象。
