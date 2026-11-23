# 稳定标识与单调时间契约

本文对应稳定标识的两个源码文件及单调时间的一个源码文件：

| 能力 | 公开命名空间 | 源码 |
|---|---|---|
| 版本化稳定哈希 | AbilityKit.Core.Identifiers | Runtime/Identifiers/StableHashV1.cs |
| 名称与标识注册表 | AbilityKit.Core.Identifiers | Runtime/Identifiers/StableStringIdRegistry.cs |
| 单调时钟接口、Stopwatch 实现和换算 | AbilityKit.Core.Timing | Runtime/Timing/MonotonicTime.cs |

这些原语不依赖玩法、调度器或 Unity 引擎。标识的领域归属、跨系统名称空间、持久化冲突策略和模拟时间推进由使用方负责。物理目录 Generic 是已有路径，本批不迁移源码或元数据；未来统一为能力目录时，应单独核对源码引用、工具链及 Unity GUID。

## 版本化哈希

StableHashV1 的三个入口具有不同编码契约，使用方必须固定具体方法，不能因为类型相同就互换。

| 方法 | 参与混合的值 | 结果 |
|---|---|---|
| Fnv1a32Utf16 | 按顺序混合每个 UTF-16 代码单元，不拆为字节 | 完整的有符号 32 位结果 |
| Fnv1a32Utf16NonNegative | 先计算上述 UTF-16 结果，再清除符号位 | 非负 31 位结果 |
| Fnv1a32Utf8 | 按 UTF-8 编码字节混合 | 完整的有符号 32 位结果 |

三者使用 FNV-1a 32 位初始值 2166136261 和乘数 16777619，乘法按无符号 32 位回绕。空引用抛 ArgumentNullException，空字符串合法；输入不进行大小写折叠或 Unicode 规范化，嵌入的零代码单元也参与计算。

UTF-16 入口对增补字符的两个代理代码单元分别混合，未配对代理项仍以原始代码单元参与。UTF-8 入口将合法代理对作为一个码点编码，每个未配对代理代码单元独立替换为 U+FFFD，并继续处理后续字符。它不分配中间编码数组；这不意味着注册表创建和新增条目没有分配。

以下是已有黄金向量的一部分，完整编码宽度及非法代理项边界见直接契约测试。

| 输入 | UTF-16 V1 | UTF-8 V1 |
|---|---:|---:|
| 空字符串 | -2128831035 | -2128831035 |
| hello | 1335831723 | 1335831723 |
| ability.cast | 1961249110 | 1961249110 |
| 能力 | 1868853449 | 932951673 |
| emoji-😀 | 802847573 | 765500573 |

ASCII 输入在两个完整位宽入口中结果相同，非 ASCII 输入可能不同。清除符号位会进一步压缩值域；以上算法均不保证唯一性，也不是密码学摘要。UTF-8 替换还会让非法代理项与显式替换字符得到相同结果。跨版本变更和持久化迁移的准入要求见 [Core 边界](./CoreBoundary.md#稳定标识符哈希)。

## 注册表与失败输出

StableStringIdRegistry 使用按序号、区分大小写的字符串比较，直接采用完整的 UTF-16 V1 哈希。它不进行 Unicode 规范化，不保留特殊的无效标识值。

| 操作 | 行为 |
|---|---|
| GetOrRegister，名称已存在 | 返回已有标识 |
| GetOrRegister，新名称无冲突 | 注册正向及反向映射，返回哈希标识 |
| GetOrRegister，不同名称哈希相同 | 抛 InvalidOperationException，保留原条目，不登记冲突名称 |
| GetOrRegister，空引用 | 抛 ArgumentNullException |
| TryGetId，空引用或未注册名称 | 返回 false，输出零，不注册名称 |
| TryGetName，未知标识 | 返回 false，输出空字符串 |

零与负数均可作为成功注册的标识，空字符串也可作为成功注册的名称。查询是否成功必须以布尔返回值为准，不能只检查输出值。注册顺序不参与哈希计算；同一组无冲突名称在不同注册顺序下标识不变。

costarring 与 liquid 的 UTF-16 V1 哈希均为 1582148253。同一注册表中先注册者保留，另一个名称会被拒绝，反向查询仍返回先注册的名称；交换注册顺序也遵循同一规则。不同注册表可以分别接受这两个名称，因此本实例内的冲突检测不能证明全局唯一。

注册表非线程安全。调用方负责同步和实例的存活范围；没有注销、清空、全局注册或自动冲突消解机制。不能容忍冲突的协议应在其领域内保留原始名称或建立明确的冲突处理契约，不应通过静默分配其他整数改变 V1 标识。

## 时钟来源与单位

IMonotonicClock 提供 Timestamp 和每秒计数 Frequency。自定义实现应提供稳定的正频率；驱动方式与并发读取保证由实现负责。

StopwatchMonotonicClock.Instance 和 MonotonicTime 的读取入口使用同一运行时的 Stopwatch 时间源。前者是共享的无额外状态实例；TimestampFrequency 与实例 Frequency 均来自 Stopwatch.Frequency。

| 入口或参数 | 单位与含义 |
|---|---|
| Timestamp、GetTimestamp | 对应时钟的不透明计数 |
| Frequency、TimestampFrequency | 每秒时钟计数 |
| GetMilliseconds | 当前 Stopwatch 读数换算成整数毫秒 |
| TimeSpan.Ticks | 每个计数为 100 纳秒，与 Stopwatch 计数不一定相同 |
| DurationToTimestampTicks 的返回值 | 指定频率下的时钟计数 |

这些读数不表示 UTC、日期或 Unix 时间，也不能当作可持久化时刻。只能比较同一时钟来源的读数；不同进程、设备或自定义时钟即使频率相同，也不能因此直接比较时间戳。单调时间测量不等于确定性的战斗模拟时间；帧推进、回放和跨端同步由所属模块负责。

## 换算、取整与溢出

| 方法 | 正常结果 | 非法输入 | 范围溢出 |
|---|---|---|---|
| ToMilliseconds | 时间戳乘 1000 再除以频率，向零截断 | 频率非正时抛 ArgumentOutOfRangeException | 结果超出 long 范围时抛 OverflowException |
| DurationToTimestampTicks | 非负 TimeSpan 换算为计数，非整计数向上取整；零时长返回零 | 频率非正或时长为负时抛 ArgumentOutOfRangeException | 返回 long.MaxValue |

ToMilliseconds 接受负时间戳。例如频率为 10000000 时，15009999 换算为 1500 毫秒，-15009999 换算为 -1500 毫秒；负数不是向负无穷取整。实现先拆分整秒与余数，余数乘法可能溢出时使用十进制中间值，最终结果仍执行范围检查。

DurationToTimestampTicks 先校验频率，再校验时长，零时长也不能绕过非法频率校验。在输入 TimeSpan 的精度范围内，向上取整避免请求的正时长被缩短。例如频率为 3 时，1 个 TimeSpan 计数换算为 1 个时钟计数，1 秒换算为 3 个计数，1001 毫秒换算为 4 个计数。

饱和只针对时长换算结果。将结果加到当前时间戳、比较截止值或处理时钟寿命边界仍由调用方负责，不能依赖本方法自动保护后续加法。

## 验证范围

直接测试位于 src/AbilityKit.Core.Tests/StableHashTests.cs、MonotonicTimeTests.cs 和 IdentifiersAndTimingContractTests.cs。本批新增 28 项测试，包含编码宽度边界、非法代理项、真实冲突的两个注册顺序、零及负标识、查询默认输出、序号比较和注册顺序独立性。

时间边界使用任意精度整数公式作为参照：毫秒转换覆盖 63 组输入，时长转换覆盖 49 组输入，包含整数路径、十进制余数路径、向零截断、向上取整及两类饱和。当前 .NET 10 Core 全量 261 项通过；本批三个源码文件分别通过中文多行注释检查。

上述证据不代替 Unity、Mono、IL2CPP 或干净消费项目验证，也不代表全包注释债务清零或正式发布评审完成。
