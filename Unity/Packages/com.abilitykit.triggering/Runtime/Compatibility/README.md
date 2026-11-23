# 运行时兼容目录

本目录维护 Runtime 根目录兼容占位脚本的治理清单，不放兼容实现。

机器可读清单位于 [`RootRuntimeCompatibilityCatalog.cs`](RootRuntimeCompatibilityCatalog.cs)，现状见 [`Runtime/Compatibility.md`](../Compatibility.md)。清单为空表示没有已登记的根目录占位脚本，不代表包内不存在兼容 API。整个包的入口分级以[正式入口清单](../../Documentation~/FormalApiBoundary.md)为准。

不新增根目录占位脚本。若清单调整，在同一批更新机器可读清单、说明和 `RuntimeCompatibilityCatalogTests.cs` 中的相关测试。`Runtime/Validation/Compatibility/RuntimeCompatibilityCatalog.cs` 是保留的校验门面，不维护第二份清单。
