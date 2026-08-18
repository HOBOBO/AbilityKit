# AbilityKit 框架包发布与治理计划

## 1. 文档目的

本文档规定 AbilityKit 框架包对外发布的整体策略、注册流程、版本治理规则和后续更新管理流程。目标读者是框架维护者与需要接入 AbilityKit 的新项目方。

文档配套两份可执行产物：

- `tools/publish/`：发布工具链与白名单（操作手册见 `tools/publish/README.md`）。
- 本文档：策略、流程与治理规则（为何这样做、何时做、谁来决策）。

本文档不讨论框架内部的代码架构，仅讨论"包如何从本仓库走向外部消费者"。

## 2. 背景与现状

### 2.1 仓库形态

AbilityKit 是单一 git 仓库（monorepo），`Unity/Packages/com.abilitykit.*` 下共有 **88** 个 Unity 包，开发期全部以 **embedded package** 形式存在——Unity 直接从磁盘加载，无需 registry 往返，迭代速度不受发布流程影响。

### 2.2 包规模与分类（2026-08-13 复核）

| 分类 | 前缀 | 数量 | 是否发布 | 原因 |
| --- | --- | ---: | --- | --- |
| 框架包 | `com.abilitykit.*`（排除下两类） | 66 | **是** | 框架本体，是对外产物。 |
| 第三方包 | `com.abilitykit.thirdparty.*` | 7 | 否 | vendored 上游代码（entitas、svelto、rvo2、luban、desperatedevs、actioneditor、behaviortreeeditor），发布涉及版权与命名空间冲突。 |
| 示例包 | `com.abilitykit.demo.*` | 15 | 否 | 供阅读学习的示例，不是应被安装的依赖。 |

### 2.3 已落地的版本治理

发布前已完成一次全仓库版本治理（前提条件，与选用的 registry 无关）：

- 所有框架包声明版本统一锁定 cohort `0.1.0`；26 处"声明版本与被引用版本不一致"的脱节已归零；35 个 `package.json` 的 UTF-8 BOM 已清除。
- 治理前的典型问题：包自身 bump 到 `0.1.0`，但引用方仍写占位 `0.0.1`。本地 embedded 模式下 UPM 不校验版本号故未暴露；一旦走 registry 会直接解析失败。

## 3. 发布模型与选型理由

### 3.1 选型结论

框架包统一发布到 **OpenUPM**（`https://package.openupm.com`）scoped registry，所有框架包共享一个 **cohort 版本号**，按 **白名单批次** 逐步放量。

### 3.2 为何不采用"每包一个 GitHub 仓库 + git URL 引用"

UPM 的 git URL 引用方式 **不解析传递依赖**。外部项目安装 `com.abilitykit.ability` 时，UPM 不会自动拉取其内部依赖链（core、attributes、world.*、combat.* 等 8 个直接依赖，再叠传递层），消费者必须自行在 `manifest.json` 中铺平整条链的 git URL 并手工对齐版本。在 66 个框架包、依赖深度 5–7 层的规模下，这不可维护。

npm 协议的 scoped registry 按版本号自动解析传递依赖，是 Unity 生态里批量发包唯一能长期维护的形态。OpenUPM 是该协议在 Unity 社区的事实标准实现。

### 3.3 为何选择 cohort 版本而非独立 semver（现阶段）

| 维度 | cohort 统一版本（现阶段采用） | 独立 semver（未来演进） |
| --- | --- | --- |
| 一致性 | 全包同一版本，依赖图天然闭合，无需对齐 | 需逐包维护版本矩阵，易脱节 |
| 发版心智 | 一个号、一起进退 | 每包独立节奏 |
| 适用前提 | API 频繁变动、包间耦合紧 | 各包成熟度分化、变更独立 |
| 代价 | 没改动的包也会随周期出新版 | 版本碎片化，治理成本高 |

框架当前处于 API 高频调整期，包间契约耦合紧密（一个包改接口，依赖它的包即使代码不变，契约也已变），统一版本比逐包 semver 更安全。演进条件见 §5.3。

### 3.4 为何开发期仍用 embedded 而非 registry 引用

embedded 直接读磁盘，改代码即时生效，无需 `npm link` 或版本号往返。registry 只是对外发布的产出通道。两条路径互不干扰：本仓库开发永远走 embedded，外部消费者走 registry。

## 4. 包分类与发布范围

发布范围严格限定为框架包（§2.2）。两条硬规则由 `release.js` 强制校验：

1. `com.abilitykit.thirdparty.*` 与 `com.abilitykit.demo.*` 永不发布。
2. 任何框架包若依赖 thirdparty/demo 包，将被 `release.js` 拒绝——因为发布后消费者在 registry 找不到这些依赖，会断链。

第 2 条是后续批次的真实约束：`world.entitas`、`world.svelto`、`combat.navigation`（依赖 rvo2）等包若要发布，必须先解决 thirdparty 依赖的对外供给方式（见 §12.1）。

## 5. 版本号策略

### 5.1 当前 cohort

`0.1.0`。所有已发布框架包的声明版本与内部引用均为 `0.1.0`。

### 5.2 cohort bump 流程（常规周期发版）

cohort 模式下，发版以 **周期或里程碑** 为单位触发，不是每个 commit。两次发版之间积累的所有改动，在发版日统一放出。流程：

```text
1. 决定新 cohort 版本（如 0.1.0 -> 0.2.0）
2. node tools/publish/align-versions.js --apply -V 0.2.0   # 全包声明+引用对齐到 0.2.0，顺带去 BOM
3. node tools/publish/audit-versions.js -V 0.2.0            # 体检，必须全绿
4. 把 release-manifest.json 中待发包的 batch status 设为 ready
5. node tools/publish/release.js                            # 预览将要打的 tag
6. node tools/publish/release.js --tag                      # 本地创建 tag
7. git push origin --tags                                   # 推送，触发 OpenUPM 构建（不可逆）
```

cohort bump 时，**所有已纳入发布的框架包统一打新版本 tag**，无论本次是否有代码改动。这是 cohort 一致性的代价与约定：版本号标记的是发版节奏，未改动的包出新版内容不变，消费者升级无副作用。

### 5.3 演进到独立 semver 的触发条件

当同时满足以下条件时，可让个别包"毕业"到独立 semver：

- 该包连续 2 个 cohort 周期无 API 变动，仅 bugfix。
- 该包被至少 2 个外部项目稳定消费。
- 其下游包已不随它频繁变动。

毕业后该包脱离 cohort，自行维护版本；`align-versions.js` 与 `release.js` 需相应支持"混合模式"（cohort 包 + 独立包）。在首个 cohort 稳定前不实现该能力。

### 5.4 hotfix（紧急单包修复）

不容忍等下个周期的紧急修复，允许 **单包偏离 cohort**：

```text
1. 仅修改目标包代码（如 core 的一个崩溃 bug）
2. 给该包单独打 patch tag：core 当前 0.1.0 -> hotfix 0.1.1
   - 手工改该包 package.json version 为 0.1.1
   - git tag com.abilitykit.core/0.1.1 && git push origin --tags
3. 下一次正常 cohort bump（如 0.2.0）时，该包跟随 cohort 回到统一版本（0.1.1 -> 0.2.0）
```

hotfix 只动出问题的包，其余包保持原 cohort 版本。`audit-versions.js` 在 hotfix 期间会对该包报 off-cohort 警告，属预期，cohort bump 后自动消除。

## 6. 发布工具链

工具集中在 `tools/publish/`，命令速查见 `tools/publish/README.md`。职责划分：

| 工具 | 职责 | 何时用 |
| --- | --- | --- |
| `align-versions.js` | 全包声明版本与内部引用对齐到 cohort、去 BOM；幂等 | 每次 cohort bump |
| `audit-versions.js` | 一致性体检（脱节 / BOM / 离群），exit 码可接 CI | 每次 PR、每次发版前 |
| `release.js` | 按 `release-manifest.json` 打 OpenUPM tag；带依赖闭环 gate；默认 dry-run，绝不自动 push | 每次发版 |
| `release-manifest.json` | 发布白名单，分批次、带 status | 维护发布范围 |

`release.js` 的依赖闭环 gate 会拒绝两类错误：依赖了未发布的包（断链）、依赖了 thirdparty/demo 包（§4 第 2 条）。该 gate 已通过负向测试验证。

## 7. OpenUPM 注册流程（一次性）

首次发布前需完成一次性注册。此后只需打 tag，无需再交互 OpenUPM 后台。

### 7.1 前置

1. 仓库已推送到 **公开** GitHub 远程（OpenUPM 仅索引公开 GitHub 仓库）。
2. 确认 `audit-versions.js` 全绿、cohort 一致。

### 7.2 注册步骤

1. 访问 <https://openupm.com/packages/submit/>，提交本仓库的 GitHub URL。
2. OpenUPM 会扫描仓库，自动发现 `Unity/Packages/com.abilitykit.*` 下的所有 `package.json`——**子目录路径无需声明**，OpenUPM 原生支持 `Packages/<name>` 布局。
3. 对每一个打算发布的包，配置其 `gitTagPrefix` 为 `<包名>/`（例如 `com.abilitykit.core/`）。本仓库 tag 格式为 `<包名>/<版本>`（如 `com.abilitykit.core/0.1.0`），与此 prefix 匹配。
4. 等待 OpenUPM 审核与首次构建（通常数小时到一天，人工审核）。

### 7.3 验证注册成功

首个 tag 推送后，在 <https://package.openupm.com/packages/com.abilitykit.core> 能看到包页面与版本，即注册成功。

### 7.4 一次性配置的存放

本仓库 dev 项目的 `Unity/Packages/manifest.json` 已在 OpenUPM scoped registry 的 `scopes` 中加入 `com.abilitykit`，供开发期切换到 registry 引用时使用（§11.2）。OpenUPM 后台的 `gitTagPrefix` 配置不在仓库内，由维护者记录在本文档 §7.2。

## 8. 首次发布流程

首批发布用于打通"tag → OpenUPM 构建 → 消费者安装"的完整链路，范围刻意收窄到无内部依赖的叶子包。

### 8.1 首批范围（batch-1-leaves）

7 个无内部依赖的叶子框架包：

`com.abilitykit.core`、`com.abilitykit.deterministic`、`com.abilitykit.gameplaytags`、`com.abilitykit.diagnostics`、`com.abilitykit.ai.abstractions`、`com.abilitykit.protocol`、`com.abilitykit.network.runtime`。

已在 `release.js` 预演中全部通过校验。

### 8.2 操作

1. 完成 §7 一次性注册。
2. 维护者确认这 7 个包稳定（status: `candidate` → `ready`，在 `release-manifest.json` 改）。
3. `release.js --tag` → `git push origin --tags`。
4. 等 OpenUPM 构建完成，在一个干净的消费者项目里验证 `com.abilitykit.core` 可装、可编译。

首批验证通过前，不启动第二批。

## 9. 后续包更新管理

本章是日常运维的核心，覆盖代码变更后如何发版的全部场景。

### 9.1 常规周期发版（最常见）

积累一批改动后，按 §5.2 的 cohort bump 流程统一发版。所有已发布框架包一起进入新 cohort。要点：

- 发版频率建议绑定里程碑（如每 2–4 周或一个特性完成），而非每个 commit。
- 发版前 `audit-versions.js` 必须全绿。
- `release.js --tag` 只给 `release-manifest.json` 中 `status: ready` 的批次打 tag；新纳入的包需先把 status 从 `candidate` 改为 `ready`。

### 9.2 把新包纳入发布（批次递进）

新包不能跳过依赖发布。纳入流程：

1. 确认该包是框架包（非 thirdparty/demo）。
2. 确认其所有 `com.abilitykit.*` 依赖要么已发布（更早批次），要么在同一批次。`release.js` 会校验，不满足则拒绝。
3. 在 `release-manifest.json` 现有批次追加该包，或新建一个批次。
4. `release.js`（dry-run）确认依赖闭环可满足。
5. 首次纳入时 status 设 `candidate`，确认稳定后改 `ready` 随下次发版放出。

批次递进的默认顺序按依赖深度：叶子 → 仅依赖叶子的中层 → 上层。详见 §13 roadmap。

### 9.3 仅一个包改了代码，想尽快发

两种选择：

- **等下个周期**（默认推荐）：改动不紧急时随下次 cohort 一起发，避免版本号噪音。
- **单包 patch**：改动紧急但不到 hotfix 程度，可仿照 §5.4 给该包单独打 patch tag，偏离 cohort，下次 cohort bump 时回归。频繁使用会让版本碎片化，应节制。

### 9.4 hotfix

见 §5.4。原则：只动出问题的包，patch 版本号，下次 cohort 回归。

### 9.5 回退与不可撤回性

OpenUPM 上的已发布版本 **不可删除、不可覆盖**（semver 的基本约束，消费者可能已锁定该版本）。因此：

- 发版前务必 dry-run（`release.js` 默认）+ audit 全绿。
- tag 推送是公开动作，`release.js` 不自动 push，必须人工执行 `git push origin --tags`。
- 若发版后发现严重问题，只能发更高版本修复（hotfix 或紧急 cohort bump），不能撤回旧版本。

### 9.6 废弃包

某个包不再维护时：不删除已发布版本，在 `package.json` 的 `description` 标注 deprecated，从 `release-manifest.json` 移除（不再打新 tag），旧版本保留在 registry 供存量消费者。

## 10. 治理与 CI

### 10.1 PR 门禁

建议在 CI（`.github/workflows`，现有 `abilitykit-test-gates.yml` 可扩展）加入：

```text
node tools/publish/audit-versions.js
```

exit 非零即阻断合入。这保证版本脱节、BOM、离群版本不会回流。

### 10.2 发版前检查清单

- [ ] `audit-versions.js` 全绿
- [ ] `release-manifest.json` 中待发批次 status 为 `ready`
- [ ] `release.js`（dry-run）无 error
- [ ] 工作区已 commit（`release.js` 会对未提交的 package.json 发警告）
- [ ] 确认本次发版范围与 changelog

### 10.3 权限

- OpenUPM 后台与 GitHub tag 推送权限仅限维护者。
- `release.js --tag` 任何贡献者可本地 dry-run，但 `git push --tags` 需推送权限。

## 11. 消费者侧

### 11.1 安装

在消费者项目的 `Packages/manifest.json` 配置 scoped registry 后按名安装：

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.abilitykit"]
    }
  ],
  "dependencies": {
    "com.abilitykit.core": "0.1.0"
  }
}
```

传递依赖自动从同一 registry 解析。消费者只需声明直接依赖。

### 11.2 开发期切换到 registry 引用（可选）

本仓库默认 embedded 开发。如需验证 registry 产物本身是否可装，可临时把某包从 `Packages/` 移出、在 `manifest.json` 改为版本号引用——但日常开发不建议，会丧失 embedded 的即时生效优势。

## 12. 边界与已知约束

### 12.1 依赖 thirdparty 的框架包

`world.entitas`、`world.svelto`、`combat.navigation` 等包依赖 thirdparty。它们发布前必须解决 thirdparty 的对外供给，候选方案：

- 将 thirdparty 以其 **原始包名** 发布到 OpenUPM（如直接发布 Entitas 的官方 Connectable 版本），框架包依赖原始包名而非 `com.abilitykit.thirdparty.*`。
- 或在框架包内 inline 所需 thirdparty 代码，去除该依赖。

该问题在第三批（上层包）才暴露，首批与第二批的叶子/中层包不涉及。需在推进到相关批次前决策。

### 12.2 OpenUPM 构建延迟

tag 推送后到 registry 可见，存在构建与索引延迟（首次含人工审核较久，后续通常数分钟）。发版后不要立即宣告，先在 OpenUPM 包页面确认版本出现。

### 12.3 私有消费者

若需对未公开稳定的外部项目供给，OpenUPM（公开 registry）不适用。可自建 Verdaccio 私有 registry，`release.js` 的 tag 机制不变，消费者侧换 registry URL 即可。当前不实施。

## 13. 分批 Roadmap

按依赖深度递进。每批通过 `release.js` 校验、稳定确认后放量。批次与候选包（候选，非承诺）：

| 批次 | 依赖深度 | 状态 | 候选包 |
| --- | --- | --- | --- |
| batch-1-leaves | 0（叶子） | candidate | core、deterministic、gameplaytags、diagnostics、ai.abstractions、protocol、network.runtime |
| batch-2-mid | 1（仅依赖叶子） | 待规划 | timer、hfsm、trace、modifiers、world.di、world.ecs、actionschema、flow、context、dataflow、behavior、ability.explain、combat.entitymanager、combat.targeting、combat.skilllibrary、network.sdk、network.transport.{inmemory,litenet,websocket}、gameframework.network、protocol.room、protocol.editor、pipeline、unity.pool、game.view.runtime、ai.mlagents.bridge |
| batch-3-upper | ≥2（上层） | 待规划 | ability、attributes、combat.{damage,motion,projectile,collision.abstractions,navigation}、host、host.extension、coordinator、game.battle.runtime、world.{framesync,snapshot,statesync,networkfragments}、network.{battle,battle.config,client,room}、record、triggering 等 |

第二批中的 `world.entitas`、`world.svelto`、`combat.navigation` 受 §12.1 约束，发布前需先解决 thirdparty 供给。

## 14. 待决事项

需维护者拍板后方可继续推进：

1. **首批 7 个叶子包是否增减**——是否有包尚不稳定应移出，或应有其他叶子包加入。
2. **§7.1 公开 GitHub 远程**——本仓库是否已具备公开远程，或需新建发布专用仓库。
3. **§12.1 thirdparty 供给方案**——影响第二批起的具体包能否发布，建议在推进第二批前定。
4. **cohort 发版节奏**——绑定里程碑的具周期（如每 2 周 / 每特性），影响 §9.1。
5. **本次基建改动是否提交**——版本治理（88 个 package.json + manifest.json）与 `tools/publish/` 工具链尚未 commit。

---

*维护者：如本文档策略与 `tools/publish/` 实现出现分歧，以本文档为准并据其修正工具；工具命令细节以 `tools/publish/README.md` 为准。*
