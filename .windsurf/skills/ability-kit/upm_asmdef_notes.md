---
name: ability-kit
section: upm-asmdef-notes
---

# UPM / asmdef notes (important)

- asmdef 引用不传递：缺类型时优先补 asmdef references，而不是只加 using。
- World DI 有两条注册路径：attribute 扫描（`[WorldService]`）与 module 显式注册（`WorldCreateOptions.Modules.Add(IWorldModule)`）。
  如果某个服务由 module 注册（例如 `IUnitResolver`），必须确保 module 被装载，否则运行时 DI 会报“dependencies are registered”。
