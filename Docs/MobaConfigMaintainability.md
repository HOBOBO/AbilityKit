# MOBA 配置表维护约定

## 字段备注

`LubanConfig/Moba/Production/Datas/` 是当前生效的 Luban Excel 目录。11 个业务工作簿的每个子 sheet 都有字段备注：普通表使用第 3 行 `##`；嵌套 bean 表的第 3 行 `##var` 必须保留成员名，备注放在第 4 行 `##`。三个 `__*.xlsx` 控制工作簿沿用 Luban 自身的备注行。

新增或改名字段后，更新 `LubanConfig/Moba/tools/annotate_workbooks.py` 中的字段说明，再运行：

```powershell
python LubanConfig/Moba/tools/annotate_workbooks.py --apply
python LubanConfig/Moba/tools/annotate_workbooks.py
python LubanConfig/Moba/export_pipeline.py --apply
python LubanConfig/Moba/export_pipeline.py --check-published
```

字段备注参与 Luban C# 注释生成，所以改备注后也需要重新发布。不要在嵌套 bean 表的 `##var` 行直接写中文说明，那一行定义导出字段名。

## 配置治理顺序

1. **技能等级参数**：当前 `ParamNames` 与各级 `Params` 靠数组下标匹配，人工插入参数易错。导出器现已校验名称合法、名称不重复、每级数值数量一致。下一步可把参数拆成按技能、等级、参数名逐行配置的表，导出时组装回现有 DTO，先保持运行时契约稳定。
2. **剩余裸整数策略列**：召唤物 `OverflowPolicy`/`StatsMode`、召唤动作的各种 `*Mode`、区域 `AttachMode`、特效 `Kind`/`StackPolicy`/`StopPolicy` 等仍为 `int`。逐项核对运行时枚举和历史值后迁入 `strong_types.xml`，按一个工作簿一批发布，避免仅按名称猜测数值语义。
3. **迁移兼容列**：多数表仍有 `OmittedFields`/`NullFields`，用于还原旧 JSON 的缺失/null 差异。等消费者统一使用新配置契约，再统计非空行、移除确实无用的列和还原分支。
4. **旧资源镜像**：`旧属性模板` 和 `Ability节点` 仍保留迁移期的数据形状。要先确认运行时消费者与外部工具，再决定能否让角色属性表和编辑器产物成为唯一来源。

每批结构迁移都应先跑 `--check-published`，再跑六英雄 .NET 冒烟和对应 Unity EditMode 验收。
