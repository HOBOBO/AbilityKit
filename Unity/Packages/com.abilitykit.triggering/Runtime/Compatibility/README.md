# 运行时兼容目录

本目录记录运行时兼容边界，不包含兼容实现占位代码。

权威的机器可读目录是 `RootRuntimeCompatibilityCatalog.cs`，便于阅读的状态记录在 `Runtime/Compatibility.md` 中。

Any compatibility entry addition, migration, or removal must update the catalog, the human-readable document, and the 相关测试 in `RuntimeCompatibilityCatalogTests.cs` in the same change.
