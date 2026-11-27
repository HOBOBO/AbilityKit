# MOBA 玩法资源

`minion_routes.json` 是地图小兵路线和初始波次调度的编辑源。它与静态 `battle_maps` Luban 表分开：路线和调度描述玩法行为，`battle_maps` 描述地图几何及出生点。

通过 Unity 的 `Tools/AbilityKit/Demos/Moba/Map/编辑兵线与波次` 编辑源文件。「校验并预览发布」验证地图身份、路线几何、默认路线和波次调度，列出源和发布路径，确认后才写入。完全相同的运行时副本发布到：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Resources/moba/minion_routes.json`
- `src/AbilityKit.Demo.Moba.Console/Configs/moba/minion_routes.json`

不要直接编辑发布副本。资源版本 4 要求显式提供 `MapId`、`DefaultRouteId`、`WaveSchedule`、`MaxActiveMinionsPerTeam` 及至少一条路线。每队容量同时统计活动和排队中的小兵，只有两队都有空间时才生成完整波次。版本 1 至 3 仍可读取，用于迁移兼容；新内容必须使用版本 4。

`map_structures.json` 是配套的地图建筑源，将建筑放置、属性及模型模板绑定到地图，不把战斗状态混入碰撞几何。仅在启用玩法功能 `structures` 时加载；基地和防御塔随后作为普通可受伤角色生成。

通过同一子菜单的「编辑地图建筑」打开建筑窗口。它单独校验建筑数据、地图 ID 和位置边界，确认后写源文件、Unity `Resources/moba/map_structures.json` 和 Console `Configs/moba/map_structures.json`。两个窗口都不刷新地图 Prefab；取消不写入，多份文件的发布不是事务，失败后先核对版本控制差异再重跑。
