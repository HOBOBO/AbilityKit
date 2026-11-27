# AbilityKit Combat Navigation

`com.abilitykit.combat.navigation` 基于宿主已准备的 XZ 网格执行路径查询，不负责地图烘焙、角色移动或时钟。

本次核校范围为 0.1.0，Unity 最低版本声明为 2022.3，直接依赖 Core、World DI，均以 [package.json](package.json) 为准。项目可从已配置的包源安装，也可选择本目录的 `package.json`；本批未验证干净 UPM 安装。

准备网格后构造 `NavigationWorld` 或通过 `NavigationService.Rebuild` 安装世界。根据 `PathStatus` 决定是否采用路径，保留旧路径或重试由调用方负责。首次接入见[包内文档索引](Documentation~/README.md)和[完整示例](Documentation~/Usage.md#完整示例与预期结果)。
