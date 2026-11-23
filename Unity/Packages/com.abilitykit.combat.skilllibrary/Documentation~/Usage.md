# SkillLibrary 使用契约与限制

从[包入口](../README.md)查看安装条件与最小示例。

- `SkillLibrary<TKey, TData>` 按主键保有数据。`Add` 遇到重复键返回 `false`；用 `TryGet` 查询可能缺失的键。
- 单分类用 `CreateDerivedKeyedIndex`，一条记录可属于多个分类时用 `CreateDerivedMultiKeyIndex`。添加索引会遍历已存在的主数据；查询得到的是主键集合，不是数据副本，也不是固定排序结果。
- 替换数据使用 `Update`；原地修改可能影响索引的字段后，需调用 `NotifyUpdated`，否则索引不会自动跟随。对于自定义索引，明确其如何处理原地更新（回调收到的旧、新数据可能是同一个对象）。
- 索引回调与主存储更新不是事务。不要把可能抛异常或带外部副作用的工作放进 selector；发生异常后不能假设主存储和全部索引仍一致。不保证并发读写或配置热切换。
