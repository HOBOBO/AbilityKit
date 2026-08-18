---
name: publishing
description: AbilityKit 框架包发布流程——OpenUPM scoped registry + monorepo cohort 版本(0.1.0) + 白名单批次；tools/publish/ 工具链(align-versions.js/audit-versions.js/release.js/release-manifest.json)；thirdparty/demo 永不发布；dry-run 打 tag 流程与依赖闭环 gate。触发场景：发布包、改 package.json 版本/keywords/author、对齐版本、清 BOM、打 tag、查发布批次、新增包进白名单、发布审计。
---

# publishing skill

基于源码核校（2026-08-17）。AbilityKit 发布走 **OpenUPM scoped registry + monorepo cohort 版本 + 白名单批次**。开发期包仍是 Unity embedded（`Unity/Packages/com.abilitykit.*`），发布只是对外产出，两者不冲突。

## 为什么不是 git URL

UPM git URL 不解析传递依赖，88 个包没人能手铺依赖链；只有 registry 按版本自动解析。`com.abilitykit.thirdparty.*`（版权）和 `com.abilitykit.demo.*`（示例）**永不发布**（release-manifest.json 的 `neverReleased.prefixes` 白名单控制）。

## 工具链（tools/publish/）

| 文件 | 作用 |
|------|------|
| `align-versions.js` | 把所有框架包锁到 cohort 版本（0.1.0）+ 清 UTF-8 BOM，幂等 |
| `audit-versions.js` | 一致性校验（版本脱节 / BOM / off-cohort），exit 码可接 CI |
| `release.js` | 按 `release-manifest.json` 打 `<name>/<ver>` 格式 OpenUPM tag；**默认 dry-run，绝不自动 push**；依赖闭环 gate 已验证能拦断链 |
| `release-manifest.json` | 白名单 + batch/status（candidate → ready → shipped） |
| `README.md` | SOP（含 OpenUPM 注册外部步骤） |

## 版本与元数据约定

- 所有框架包锁 `0.1.0` cohort。
- package.json 要求：`displayName/description/unity/keywords/author/dependencies` 齐全；author 统一 `"AbilityKit"`；description 用 UTF-8（曾有 modifiers 包 GBK 乱码）。
- 改 package.json 后必跑 `node tools/publish/audit-versions.js`，期望 `version mismatches: 0 / BOM remaining: 0 / framework packages off cohort: 0`。

## 当前批次（batch-1-leaves，status: candidate）

7 个叶子包：`core`、`deterministic`、`gameplaytags`、`diagnostics`、`ai.abstractions`、`protocol`、`network.runtime`。

> deterministic 与 core 必须同批：core 的 `MathUtil.Sqrt` 已路由到定点内核、asmdef 依赖它。
> threading 已从 batch-1 移除（2026-08 删除死代码包）。

## 发布 SOP

1. 确认要发布的包稳定（对应测试/门禁绿）。
2. `node tools/publish/audit-versions.js` 清零。
3. `node tools/publish/release.js`（dry-run）确认 plan 里只有预期包、无断链。
4. 外部人工：repo 推公开 GitHub + openupm.com 注册（每包配 `gitTagPrefix: <name>/`，子路径自动识别）。
5. manifest 里对应 batch `status` 改 `ready` → `node tools/publish/release.js --tag` → `git push origin --tags`。

## 坑

- OpenUPM tag 约定 `<package-name>/<version>`，push 后 OpenUPM 自动构建索引。
- `align-versions.js`/`audit-versions.js` 会清 BOM；用 sed 清 BOM 时注意别破坏中文注释（.ps1 无 BOM 时 PowerShell 按 GBK 读会解析错——审计脚本只写 ASCII）。
- 完整发布治理文档：`Docs/AbilityKit框架包发布与治理计划.md`（注册流程、后续包管理、hotfix、回退、CI 治理、3 批 roadmap）。
- 依赖深度分批：batch-2-mid（timer/hfsm/trace/modifiers/world.di/...）→ batch-3-upper（ability/combat.*/host/...）。

## 相关 skill

- 测试门禁 → [test-artifacts](../test-artifacts/SKILL.md)
- 设计文档治理 → [design-docs](../design-docs/SKILL.md)
