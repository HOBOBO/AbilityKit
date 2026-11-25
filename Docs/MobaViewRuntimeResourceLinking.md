# moba.view.runtime 资源接入

`game-res` 是资源的权威来源，AbilityKit 不复制模型、材质、贴图、动画或特效文件。Windows 开发机使用 Junction 将资源挂载到 `com.abilitykit.demo.moba.view.runtime` 包：

```text
Unity/Packages/com.abilitykit.demo.moba.view.runtime/
├─ Resources/ChampionVfx/<hero>       -> game-res/Unity/Assets/ExtractedPrefabs/<hero>
└─ GameAssets/GameResDependencies/<dir> -> game-res/Unity/Assets/<dir>
```

英雄材质依赖的共享 Shader 通过 `GameAssets/GameResDependencies/Puppet` 挂载，
因此 `Puppet/Shaders` 及其 `.meta` 文件会随资源一起被 Unity 导入。不要把 Shader
文件复制到 AbilityKit；`game-res/Unity/Assets/Puppet` 仍是唯一权威来源。

从 AbilityKit 仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\link_game_res_resources.ps1 `
  -GameResRoot C:\Workspace\gitProject\game-res
```

脚本只创建 Junction，重复执行保持幂等；目标已存在但不是同一个 Junction 时会直接失败，避免误删除或覆盖本地文件。脚本也会检查源目录和对应的 `.meta` 文件是否存在。

英雄 Prefab 通过包内 `Resources` 路径读取，例如：

```csharp
Resources.Load<GameObject>(
    "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R");
```

当前烬（模型 ID `3001`）的模型、Q/W/E 与 R 相关特效均从
`13.Jhin_Battlecast` 读取。生产 VFX 表中的资源 ID 为 `90013001`–`90013008`，
动作表使用模型控制器中的 `Spell_Q`、`Spell_W`、`Spell_E`、
`Spell_R_Deploy` 和 `Spell_R_Idle` 状态；动作帧按资源控制器的 24 fps 配置。

动作运行时读取 `Resources/moba/character_view_actions.json`，模型配置读取
`Resources/luban/moba/models.json`。前者是表现动作覆盖目录，后者是 Luban 导出的权威模型目录；
两处都必须包含模型 ID `3001`，否则逻辑模型可以加载但表现层无法创建烬的 Prefab。

依赖目录不直接作为 `Resources.Load` 路径使用；Prefab 内的 GUID 引用由 Unity AssetDatabase 解析。建立 Junction 后在 Unity 中执行 `AssetDatabase.Refresh`，或重启 Unity，再运行 `MobaPresentationResourceTests`。Junction 本身不应提交到 Git，只提交建链脚本和这份说明。

## Action frame-rate conversion

`character_view_actions` stores model/source animation frames in `frameCount`. Jhin's model actions are authored at 24 FPS, while the battle logic may run at 30 or 60 ticks. The presentation runtime therefore converts the authored span before sequence progression and Animator sampling:

```text
durationSeconds = frameCount / authoredFrameRate
logicalFrameCount = ceil(durationSeconds * logicTickRate)
```

An action-level `frameRate` may override the catalog default; when omitted, the default is 24 FPS. `CharacterPlaybackIntent.AuthoredFrameCount` keeps the source span for diagnostics, while `FrameCount` is the logical span for the current tick rate. This keeps the real action duration stable when the logic tick rate changes.

## 正式动画信息同步

`Window/AbilityKit/MOBA 角色动画同步` 从权威模型表定位模型，并读取实际导入的 Controller/Clip，
将动画状态名、帧率和时长帧数同步回 `M-模型表.xlsx` 的角色动画动作子表。
HFSM 编辑器也提供该窗口的入口。改名使用编辑器侧稳定资源绑定追踪，不按文件名猜测状态名。
具体操作、检查和批处理入口见 [状态机编辑与动画同步](MobaStateMachineAuthoring.md#正式角色动画同步)。

当前挂载的正式拆分资源仍可能保留演示等待或旧 Controller 时序；同步实际导入数据不表示已经
裁好有效角色片段。时序告警需要人工处理，技能提交点和状态退出时间仍由 Flow/HFSM 配置决定。
