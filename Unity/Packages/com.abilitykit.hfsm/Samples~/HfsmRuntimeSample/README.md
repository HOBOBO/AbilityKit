# HFSM Runtime Sample

演示「编辑器导出 Definition → 运行时加载执行 → 编辑器调试窗口观察」的完整链路。

## 导入

在 Package Manager 的 `AbilityKit HFSM` 包详情里，点击 **Samples** 区段的 **Import**，sample 会复制到 `Assets/Samples/AbilityKit HFSM/<version>/HfsmRuntimeSample/`。

## 运行验证

1. 打开任意场景，把 `Prefabs/HfsmRuntimeSample.prefab` 拖进 Hierarchy。
2. 进入 Play Mode（组件会自动加载 `Resources/hfsm_sample.json` 并启动状态机）。
3. 选中该 GameObject，右键组件名 → `Trigger: go` / `Trigger: back`，观察 Console 里的状态进入/退出日志。
4. 打开 `Window > AbilityKit > HFSM Runtime Monitor`，选中 `HfsmRuntimeSample` 实例，观察激活路径随触发变化。

（也可用菜单 `AbilityKit/HFSM/Samples/Runtime Sample/Create Runtime Verification Scene` 一键在当前场景创建同样的 GameObject。）

## 配置

`Resources/hfsm_sample.json` 是运行时 Definition（两状态 Idle/Active + 两个触发转移 go/back），可直接文本编辑；也可以改用编辑器里 `Window > AbilityKit > HFSM Graph Editor` 画图后 `Export Next Runtime Definition` 导出 `.hfsm`（内容格式相同），再把组件引用的文件替换成它。

## 删除

不需要时直接删除 `Assets/Samples/AbilityKit HFSM/` 整个目录即可，不影响主包。
