# Triggering 兼容记录

本页记录历史目录；现存兼容入口、调用证据和下线条件见 [入口清单](../Documentation~/FormalApiBoundary.md)。

当前 Runtime 根目录已不再保留 .cs 兼容占位入口。

根目录扫描使用 [Runtime/Compatibility 下的机器可读清单](Compatibility/README.md)；它不扫描子目录中的保留 API。

- 已清理的旧主线运行壳包括 `Runtime/Dispatcher`、`Runtime/Scheduler`、`Runtime/Executable` 与 `Runtime/Legacy`。
- 旧路径已清理不代表所有同名能力已移除。例如 `Runtime/RuntimeServices/Dispatcher` 仍承载运行时驱动适配，`Runtime.Schedule` 仍有兼容实现。
- 不新增旧式兼容壳；保留入口按 [下线流程](../Documentation~/LegacyMigrationPolicy.md) 管理。
