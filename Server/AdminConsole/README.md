# AbilityKit Admin Console

独立的 AbilityKit Orleans Gateway 后台前端工程，使用 Vite + Vue 3 + TypeScript。

## 开发

```bash
npm install
npm run dev
```

开发服务器默认使用 Vite proxy 将 `/api` 与 `/debug` 转发到 `http://localhost:5000`。可通过环境变量 `ABILITYKIT_GATEWAY_URL` 指向实际 Gateway。

## 构建

```bash
npm run build
```

构建产物会输出到 `../Orleans/src/AbilityKit.Orleans.Gateway/wwwroot/admin`，因此 Gateway 仍可通过 `/admin` 托管正式后台，同时前端源码与后端 API 工程保持独立。

## 英雄技能批次排查

用现有 .NET Battle Scenario Runner 批跑并留档，参见[运行示例](../../Docs/design/09-ImplementationExamples/MOBA/31-MobaBattleScenarioSkillAcceptanceWorkflow.md#console-批跑与离线排查)。在 Admin 的「技能分析 → 英雄技能批次排查」选择 `--output-dir` 指定的目录，或单个已发布批次。支持目录选择的浏览器（如 Edge 或 Chrome）会在本机读取索引；需要查看更多诊断时再打开单场证据。不上传文件、不需要战斗会话，不在网页启动 Unity 或运行场景。

可按用例和验收状态筛选批次，在单场证据中查看技能链、按帧排布的战斗事件、实体关系及已保存帧的属性。没有证据时仍能看验收判定；属性快照不是整场逐帧历史。刷新页面后需重新选择目录。网页暂不提供共享目录、写入、删除或跨批对比。

先点批次概览的判定卡筛选用例。打开单场证据后，事件轨道按模拟帧聚合伤害、技能、对象、告警等已记录事件；点柱子可按类别和帧区间收窄明细，再从具体事件跳到同场 Trace 或实体。柱高只用于同一轨道内比较，不表示伤害量，也不改变验收判定。
