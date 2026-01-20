# Hotfix.Ability.Moba
 
 这个目录是一个 **热更源码工程（Hotfix Source Project）**，用于 Unity Editor-only 的热重载 PoC。
 
 - 源码放在 `Assets/` 外部，用于避免触发 Unity 的自动编译。
 - 编译产物输出到 `Unity/Library/HotUpdate/`。
 - Unity Editor 在运行时加载新的 DLL，并替换热更 systems/services，**不需要重建 world/entities**。
 
 ## 编译（Build）
 
 在 Unity 菜单执行：
 
 - `Tools/AbilityKit/Hot Reload/Compile Hotfix`
 
 其内部等价于执行：
 
 - `dotnet build Hotfix.Ability.Moba.csproj -c Debug -o <UnityProject>/Library/HotUpdate`
 
 ## 重载（Reload）
 
 在 Unity 菜单执行：
 
 - `Tools/AbilityKit/Hot Reload/Reload Hotfix`
 
 前置条件：
 
 - 游戏在运行中（Play Mode）
 - 已存在 battle session/world（即 `BattleLogicSessionHost.Current` 不为空）
 
 重载行为：
 
 - 从 `Library/HotUpdate` 加载最新的 `Hotfix.Ability.Moba*.dll`
 - 在 DLL 内查找实现了 `AbilityKit.Ability.HotReload.IHotfixEntry` 的类型
 - 执行：
   - `Uninstall(oldEntry)`
   - 对一个新的 Entitas `Feature` 执行 `Install(newEntry)`
   - 将该 feature swap 到一个代理系统中（从而立刻切换热更逻辑）
 
 ## 引用 / 依赖（References / Dependencies）
 
 该工程会引用 Unity 编译产物：
 
 - `Unity/Library/ScriptAssemblies/AbilityKit.Ability.Runtime.dll`
 
 同时还需要 Entitas：
 
 - `Unity/Assets/ThirdParty/Entitas/Entitas.dll`
 
 如果你看到类似编译错误：
 
 - `CS0400: 未能在全局命名空间中找到类型或命名空间名“Entitas”`
 - `CS0012: 类型“Systems”在未引用的程序集中定义。必须添加对程序集“Entitas ...”的引用。`
 
 请检查 `Hotfix.Ability.Moba.csproj` 中的 `Entitas` 引用是否正确。
 
 ## Static 规则（Hot Reload）
 
 由于旧的热更程序集不会卸载，**static 可变状态存在较大风险**（旧状态会跨 reload 持续存在）。
 
 - 避免在热更代码中使用 `static` 可变字段。
 - 如果必须使用 static cache/singleton，则必须在每次 reload 时重置。
 
 框架支持：
 
 - 用 `[AbilityKit.Ability.HotReload.HotReloadStatic]` 标记
 - 注册重置回调：
   - `AbilityKit.Ability.HotReload.HotReloadStaticRegistry.Register("id", ResetMethod);`
 
 每次调用 `HotReloadRuntime.Apply(...)` 前都会自动执行 `ResetAll()`.
