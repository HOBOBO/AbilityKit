# MOBA 玩法资源

`minion_routes.json` 是地图小兵路线和初始波次调度的编辑源。它与静态 `battle_maps` Luban 表分开：路线和调度描述玩法行为，`battle_maps` 描述地图几何及出生点。

通过 Unity 的 `Tools/AbilityKit/Demos/MOBA/Edit Minion Routes` 窗口编辑源文件。`Validate And Export` 验证地图身份、路线几何、默认路线和波次调度，再将完全相同的运行时副本发布到：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Resources/moba/minion_routes.json`
- `src/AbilityKit.Demo.Moba.Console/Configs/moba/minion_routes.json`

不要直接编辑发布副本。资源版本 4 要求显式提供 `MapId`、`DefaultRouteId`、`WaveSchedule`、`MaxActiveMinionsPerTeam` 及至少一条路线。每队容量同时统计活动和排队中的小兵，只有两队都有空间时才生成完整波次。版本 1 至 3 仍可读取，用于迁移兼容；新内容必须使用版本 4。

`map_structures.json` 是配套的地图建筑源，将建筑放置、属性及模型模板绑定到地图，不把战斗状态混入碰撞几何。仅在启用玩法功能 `structures` 时加载；基地和防御塔随后作为普通可受伤角色生成。
