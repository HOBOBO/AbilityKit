# Luban 二进制运行时使用指南

> 文档类型：使用指南 / 所有权契约
> 适用范围：本包 0.0.1 当前源码；环境要求见 [package.json](../package.json)。
> 核校状态：已核校本文命名空间、缓冲区与本地示例；不含平台或上游版本兼容验收。
> 事实基线：2026-11-25。

## 1. 先选择正确的 API

| 命名空间 | 提供的能力 | 来源 |
| --- | --- | --- |
| `Luban.Serialization` | 完整 ByteBuf 读写、BeanBase、序列化接口和缓冲池 | [LubanCore](../Runtime/LubanCore)；保留文件内上游许可 |
| `Luban` | 生成代码所需的精简读取外观：ByteBuf、BeanBase 等 | [LubanCompat](../Runtime/LubanCompat)；包装核心读缓冲 |

`Luban.ByteBuf` 与 `Luban.Serialization.ByteBuf` 是不同类型；前者没有核心缓冲区的完整 Write API，也不公开底层 Raw。两处 `BeanBase` 的继承契约同样不能按短名互换。生成的 `DR*`/`Tb*` 等业务类归调用项目，不在本包内。

## 2. 最小本地示例

以下完整类只用当前核心代码生成一组本地数据，再通过兼容层读取。将它放入引用本包程序集的调用方，执行 `LubanRuntimeQuickStart.Run()`，预期返回 `42`；这里没有读取网络或外部来源数据。

```csharp
using System;

public static class LubanRuntimeQuickStart
{
    public static int Run()
    {
        var writer = new Luban.Serialization.ByteBuf();
        writer.WriteInt(42);
        writer.WriteString("local-example");
        var bytes = writer.CopyData();

        var reader = new Luban.ByteBuf(bytes);
        var value = reader.ReadInt();
        var label = reader.ReadString();
        if (value != 42 || label != "local-example" || reader.NotEmpty)
            throw new InvalidOperationException("Unexpected local round-trip result");
        return value;
    }
}
```

实际业务按生成代码指定的结构、字段顺序和缓冲类型反序列化，再映射到项目 DTO；不能把本例的两字段字节布局套用到生成表。项目同时需要匹配的生成代码和同批表产物，单独更新 runtime 不会迁移旧数据。

## 3. 缓冲区生命周期与所有权

- `new ByteBuf(byte[])` 和 `Wrap(byte[])` 复用传入数组，不提供隔离拷贝；读取前后不要由其他线程修改该数组。兼容层拒绝 null；空或损坏数据不因此成为有效表。
- `ReaderIndex`/`WriterIndex` 是可变游标，`Size` 是二者差值。不要共享同一实例并行读写或手动设置越界游标。每个读取任务应拥有自己的缓冲实例及稳定输入。
- 核心 `CopyData()` 复制当前可读区间 `[ReaderIndex, WriterIndex)`；它不是整个底层容量的快照，读过的前缀不会自动包含在内。需要保留完整数据时在消费前复制或保存原始输入。
- `Replace(bytes)` 会改绑数组并重置游标；旧数组不由缓冲区释放。缓冲区不是文件流，也不负责文件或网络连接生命周期。
- 普通示例直接创建托管对象，无需 Dispose。核心 `Release()` 只调用构造时绑定的回收回调；普通 `new` 没有该回调，不等于自动回池。
- [ByteBufPool](../Runtime/LubanCore/Serialization/ByteBufPool.cs) 基于普通 Stack，无锁；按分配方的约定恰好归还一次。归还会清空游标，调用方此后不得使用或再次归还同一对象。
- [ThreadLocalTemporalByteBufPool](../Runtime/LubanCore/Serialization/ThreadLocalTemporalByteBufPool.cs) 的 `GuardAlloc` 返回可 Dispose 的临时 guard，使用完成即归还；不要跨线程或在归还后继续持有 Buffer。线程本地缓存不意味着某个借出的实例可跨线程安全共享。

## 4. 失败定位与兼容

| 症状 | 核查方向 |
| --- | --- |
| 找不到 Write 方法或类型无法转换 | 检查是否混用了两个 ByteBuf / BeanBase 命名空间，以生成代码签名为准 |
| 数据不足、长度或反序列化异常 | 检查生成代码与表的批次、字节是否完整、游标起点和解析字段顺序；不要用 catch 后的默认对象冒充加载成功 |
| 数据出现旧值或游标错乱 | 检查是否复用了正在读取的数组、已回池对象、跨线程缓冲区或旧生成物 |
| Unity 编译 unsafe 报错 | 本包 asmdef 已启用；自建 MSBuild 工程引用源码时也需 `AllowUnsafeBlocks`，不修改上游算法以绕过设置 |

底层类型可能抛序列化或范围等异常，包不统一返回 AbilityKit 的 ConfigReloadResult。外层数据库/加载器决定哪些异常转成失败结果；业务引用、ID 唯一性、规则和资源有效性需上层校验。没有自动格式协商、上游版本检测、schema 升级或发布回滚能力。

## 5. 核校范围

2026-11-25 已用 .NET 10 编译本包源码并运行本页核心写入 → 兼容读取示例，只产生本地受控测试数据；本地 round-trip 属局部 E3。实际 MOBA 表、Unity GUI、IL2CPP、不同上游版本及独立 UPM 安装未通过本示例验收。第三方来源与许可按源码现有声明保留，不为未记录的完整上游修订号补写推断。
