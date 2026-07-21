# 本目录已弃用 — Skill 已统一迁移至 .claude/skills/

本目录（`.windsurf/skills/`）下的 skill 内容已合并并迁移到项目根的 `.claude/skills/`，供 Claude Code / Claude Desktop 使用。

## 迁移映射

| 原目录 | 新位置 | 说明 |
|--------|--------|------|
| `.windsurf/skills/ability-kit/` | `.claude/skills/ability-kit/` | 与原 `.cursor/skills/abilitykit-development/` 合并，新增 `console_demo_runtime.md` 章节 |
| `.windsurf/skills/framesync-prediction-rollback/` | `.claude/skills/framesync-prediction-rollback/` | 原样迁移，规范 frontmatter |
| `.windsurf/skills/state-handles-controllers/` | `.claude/skills/state-handles-controllers/` | 原样迁移，规范 frontmatter |

## 后续维护

- **新增/修改 skill 请直接编辑 `.claude/skills/` 下的文件**，不要再维护本目录。
- 本目录仅作历史保留，不再与上游同步。
- 若使用 Windsurf IDE，可在 `.windsurf/wsrules` 等位置指向 `.claude/skills/`，或考虑后续完全移除本目录。

> 迁移日期：2026-07-20
