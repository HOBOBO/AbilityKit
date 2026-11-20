# AbilityKit 框架包发布工具

仓库内的 Unity 嵌入包用于开发；面向外部消费者的约定渠道为 OpenUPM
（`https://package.openupm.com`）。发布清单与本地计划检查不代表包已经发布、注册表已收录或稳定性签核完成。

## 工具与版本约定

| 文件 | 职责 |
|---|---|
| `align-versions.js` | 预览统一框架包版本、内部引用与 BOM 修正；显式 `--apply` 才写入。 |
| `audit-versions.js` | 检查版本引用一致性、BOM 与 cohort，发现问题时非零退出。 |
| `release.js` | 校验清单、包、精确内部依赖和本地 Git 标签；默认 dry-run，`--verify-inputs` 校验提交内容，`--tag` 自动严格校验后创建 ready 包的本地标签。 |
| `release-inputs.js` | 只读核对固定 HEAD、完整包子树、执行工具与标签依赖闭包，输出 Git 对象证据。 |
| `release-manifest.json` | 维护 cohort、标签格式、禁止发布的前缀与批次状态；工具不会自动改写。 |
| `release.test.js` | 用 Node 内置测试运行器，在系统临时目录的隔离 Git 仓库验证 CLI，测试后清理夹具。 |

所有待发布框架包采用同一 cohort（当前为 `0.1.0`），内部 AbilityKit 依赖必须声明
**精确 SemVer 版本**，包括合法的预发布/构建标识；范围、通配符、git/file 地址不属于此工具支持的发布依赖形式。
若请求版本已有本地标签，可与仓库中该依赖的当前源码版本不同；工具不重新发布旧依赖。

`com.abilitykit.thirdparty.*` 与 `com.abilitykit.demo.*` 不发布，也不能成为已选发布包的内部依赖。
规则前缀由清单维护；不应为通过校验而删除这些禁止项。仅外部包依赖不在该工具的解析范围内。

## 计划选择与依赖规则

| 批次状态 | 默认计划 | `--include-candidate` | 可由 `--tag` 创建标签 |
|---|---|---|---|
| `ready` | 包含 | 包含 | 是，已有同名标签时跳过且不移动。 |
| `candidate` | 不包含 | 包含并显示 `cand` | 否。 |
| `shipped` | 不包含 | 不包含 | 否；状态记录不能代替真实标签。 |

`--batch <id>` 在上述状态筛选基础上限制为一个批次，不自动提升状态或扩展依赖批次。
未知批次、未知/重复参数、缺少批次 ID、`--tag` 与 `--dry-run` 同用均以非零退出。
没有符合条件的包时显示空计划并成功退出；候选批次需要显式加入预览。

对于每个已选包的内部依赖，必须在仓库中找到该包，且满足下列条件之一：

1. 已存在请求精确版本的本地 Git 标签，例如 `com.abilitykit.deterministic/0.1.0`。
2. 依赖在**本次所选计划**中，声明版本等于请求版本；ready 包要求依赖也为 ready，candidate 预览允许依赖 ready 或 candidate。

因此，ready 依赖未打标的 candidate 会被拒绝，即使使用 `--include-candidate`；
未选择的 ready 批次和没有标签的 shipped 批次也不能满足依赖。
不同 ready 批次可以在同次未筛选计划中共同满足闭包。候选闭包通过只代表可预览。

清单中的所有批次 ID 必须唯一、状态合法，包在批内/跨批只能归属一次；筛选不隐藏这些结构错误。
扫描到的 AbilityKit package.json 包名必须唯一且与目录一致。已选包还要检查存在性、cohort、BOM、禁止前缀和依赖。
未选择包的版本与完整依赖闭包不由本次计划验收，需另运行版本审计和相应批次检查。
标签格式须各包含一次 `{name}` 与 `{version}`；所有计划/请求标签先经 `git check-ref-format` 检查。

## 本地使用

```powershell
# 当前版本一致性检查
node tools/publish/audit-versions.js

# 默认只预览 ready；当前全部为 candidate 时显示空计划
node tools/publish/release.js

# 显式 dry-run 预览指定候选批次，不改变其状态
node tools/publish/release.js --dry-run --batch batch-1-leaves --include-candidate

# 在只读模式进一步检查工作区、HEAD 与已有标签内容；脏输入会拒绝
node tools/publish/release.js --dry-run --verify-inputs --batch batch-1-leaves --include-candidate

# 仅在完成发布签核并按流程设置 ready 后，由操作者创建本地标签
node tools/publish/release.js --tag
```

新包先核对定位、稳定性与依赖，再加入唯一批次。按依赖顺序组织批次；
筛选单个批次时，未同时选中的依赖须已具备对应版本标签。
cohort 升级需按发布流程评审，再显式对齐版本，例如
`node tools/publish/align-versions.js --apply -V 0.2.0`，并重新执行全部相关验收。

## 提交内容一致性与证据

普通 dry-run 是开发期声明预览，可以检查尚未提交的计划。`--verify-inputs` 开启严格只读检查；
`--tag` 自动执行同样的检查，不能用省略此参数绕过。检查覆盖所有已选包，包括显式加入的候选。
空计划不校验源码，也不输出输入证据。严格校验不替操作者提交、暂存或清理任何文件。
Git 查询禁用可选锁，避免只读状态查询刷新索引；标签创建仍使用 Git 必要的引用锁。

- 固定当前 HEAD 提交，要求 `release.js`、`release-inputs.js` 和发布清单已提交且内容/暂存状态一致。
- 要求已选包的完整目录与 HEAD 子树一致，包含 Runtime、Editor、文档和元数据。
  新增/删除/暂存修改、未跟踪文件、忽略文件也会拒绝；不跟随符号链接、目录联接或子模块。
- 除 Git 状态外，批量计算实际文件的 Git 内容身份，识别 `assume-unchanged` 等标记隐藏的内容变化。
  内容按照当前 Git 过滤/换行规则规范化，不把等价 CRLF/LF 写成不同源码；非 Windows 同时核对可执行权限。
- 已选包已有同版本标签时，标签须指向提交，包内名称/版本和完整包子树须与本次输入一致。
  允许不同提交中相同的包子树，禁止复用同版本不同内容标签；即使没有新标签待建也会检查。
- 已发布依赖从其请求版本标签读取 package.json 和子树，递归核对内部依赖的精确版本与闭包。
  未选择的历史依赖不要求等于工作区当前版本或子树；它的实际发布输入是记录的标签对象/提交。
  缺失的传递依赖只有在同次计划存在同版本、状态允许的提供者时才能满足。

成功输出一行 `input-evidence: {...}` JSON，包含 schemaVersion、固定 head、三个工具/清单 blob、
所选包名称/版本/tree，以及已有依赖的 tag/object/commit/tree。可将命令输出存为本地验证证据；
Git 对象 ID 绑定仓库中的规范化内容，不是 nupkg/tarball 的 SHA256 或注册表安装证明。
已有标签在检查结束时复核其对象身份，HEAD 也复核未变化。新标签显式指向已验证的提交 ID，
不会随创建过程中的 HEAD 移动而改用其他提交。

范围之外的项目文件与未选择包的工作区修改不阻止该计划；镜像 .NET 工程、平台构建、外部 Git 配置/
过滤器和发布制品仍须各自固定并验收。工具不提供并发工作区/标签锁；检查时应停止并行修改，
通过后如输入变化须重跑，不能将此证据当作随后任意时刻的文件快照或远端授权。

## 失败与操作边界

参数、清单、包和计划校验在创建任何标签前完成；Git 标签枚举、引用检查、状态读取失败均返回非零。
Git 使用参数数组调用，不经 shell 拼接。真正创建标签时如有命令失败，立即非零退出并停止后续项，
报告已成功数量；此前成功的本地标签保留，**不自动回滚**。应人工复核该次部分结果。

`--tag` 给严格校验固定的提交打标签，不提交工作区内容。原 package.json 脏改警告已升级为
所选输入的拒绝条件；不能把普通声明预览中的 `EXISTS` 当作标签内容已经核对。
本地标签内容验证不证明远端存在性或注册表收录。
本工具不执行 Core 测试、注释检查、Unity/Player 矩阵、干净消费、制品哈希复核或发布签核。
正式发布还须固定实际制品、构建输入和相应验收，不能仅凭 dry-run 或输入校验通过推进候选。

## 回归门禁

需要 Node.js 22 或更新版本与 Git，无 npm 安装步骤：

```powershell
node --test --test-reporter=tap tools/publish/release.test.js
& tools/test_release_plan.ps1
& tools/run_test_gate.ps1 -Gate core-stability -StepName 'Release plan dependency and failure tests'
```

测试使用复制后的真实 CLI、最小清单与包、真实 Git 标签和故障夹具。
成功/拒绝、版本不满足、状态混合、筛选、重复归属、缺包、非法参数、Git 读取失败、部分打标失败均有覆盖；
严格输入场景另覆盖 Runtime/暂存/未跟踪/忽略文件、隐藏修改、标签内容、历史依赖闭包与换行规范化。
PowerShell 入口保存 TAP 与验证输入哈希；单步骤通过不代表整个 `core-stability` 通过。
输入哈希在运行前后核对，测试期间验证器变化则拒收该次证据；TAP 中的
`fixture-input-evidence` 仅为隔离成功样例，不是真实工作仓库的发布证明。

## 注册表与消费者

正式发布仍需操作者单独完成：将公共 GitHub 仓库/包提交到 OpenUPM，按每包的
`<package-name>/` 配置标签前缀并核对仓库内包路径，确认索引后再按发布流程推送选定标签。
脚本从不推送；本轮未验证 OpenUPM 注册、构建或收录。

注册表具备所需版本后，消费工程的 `Packages/manifest.json` 可声明：

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

上例是接入形式，不声明当前版本已可安装。注册表用于按版本解析传递依赖；
直接使用 UPM git URL 不会自动补齐整个 git 依赖链，不能替代注册表闭包验收。
