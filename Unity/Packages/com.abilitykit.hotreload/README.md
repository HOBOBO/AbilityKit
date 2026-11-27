# AbilityKit HotReload

`AbilityKit.HotReload` 为 Entitas 世界提供可选的系统热替换。宿主选择 `IHotfixEntry`，在世界线程的安全点调用 `HotReloadRuntime.Apply`。替换前准备并初始化候选系统；候选未提交时不会替换当前执行目标，但不回滚外部副作用。世界销毁时清理当前项，也可由宿主显式调用 `ReleaseWorld`。

- `Runtime/`：系统替换、服务覆盖、静态重置与释放；不引用 Unity 引擎。它依赖 Entitas 协议，不是通用程序集装载器。
- `Editor/`：只在 Unity Editor 的 Play Mode 启动时注册配置日志监听；它不装载程序集，也不参与系统替换。包装配仍声明该日志旁路使用的 Ability 与 Core 依赖。
- 版本与依赖见 [package.json](package.json)。失败边界与实际宿主装配见[运行时设计](Document/HotReload热更新运行时模块开发设计文档.md)。

不承诺程序集卸载、生产灰度、联机战斗中途切换或跨 world 的原子更新。
