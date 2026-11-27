# AbilityKit Combat EntityManager

`com.abilitykit.combat.entitymanager` 保存实体 ID，并按调用方指定的键建立单键或多键索引。实体数据、分类规则和写入顺序由接入方管理。

## 定位与环境

本次核校版本为 0.1.0，Unity 最低声明为 2022.3；声明来源为 [package.json](package.json)，直接依赖 Core。对外命名空间为 `AbilityKit.Ability.Battle.EntityManager`，不与包名完全相同。适合单线程中的可重建查询索引，不提供 ECS 存储、事务或持久化。

## 安装与首次使用

在 Unity Package Manager 中从项目配置的来源安装；本地开发可选择本目录 `package.json`，并确保 Core 可解析。本批没有重做干净 UPM 安装。

先创建 `BattleEntityManager<TId>` 与所需索引，再注册 ID、显式调用 SetKey / AddKey。删除时走 Registry，让已挂载索引清理关系。可复制代码与预期结果见[完整使用示例](Documentation~/Usage.md#完整示例与预期结果)。

## 文档与样例

- [包内文档索引](Documentation~/README.md)。
- [使用指南](Documentation~/Usage.md)：显式改键、后加索引、结果视图和失败处理。
- [包内基础样例](Runtime/EntityManager/BattleEntityManagerExample.cs)。
- [历史开发说明](Document/实体管理模块开发设计文档.md)：保留旧路径与背景。

## 生命周期与限制

内建索引不消费 `EntityUpdate` 自动改键；NotifyUpdated 主要用于自定义索引。返回结果是无排序保证的内部集合视图。回调失败没有自动回滚；容器无 Dispose，由宿主释放引用及自己拥有的实体资源。

> 核校状态：2026-11-25 对照当前 Runtime 与 .NET 镜像核对上述限定契约；验证范围见使用指南。
