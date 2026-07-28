# Docs/design 飞书一键同步工具

本工具链以 `Docs/design` 为唯一文档源，一次执行完成 Mermaid 校验、飞书友好格式导出、内容摘要比对和增量发布。默认使用 Board 兼容模式，把 Mermaid 语义转换成可编辑画板节点；需要保留 Mermaid fenced source 时可显式切换到 `native` 模式。默认通过用户 OAuth 以浏览器中登录的个人身份写入个人云空间，应用只承担 OAuth 客户端角色。发布时默认在目标根文件夹下镜像 `Docs/design` 的相对目录层级，避免全部文档扁平堆放。

## 1. 最少配置

首次执行安全预览：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1 -Preview
```

脚本会自动从 `tools/feishu-design-sync.template.json` 创建被 Git 忽略的本地配置：

```text
tools/feishu-design-sync.local.json
```

只需填写 OAuth 应用凭据和个人云空间目标文件夹：

```json
{
  "authMode": "user-oauth",
  "appId": "cli_xxxxxxxxxxxxxxxx",
  "appSecret": "your-app-secret",
  "target": {
    "rootToken": "your-personal-folder-token"
  },
  "changedDocumentMode": "block"
}
```

`rootToken` 是个人云空间文件夹 URL 中 `/folder/` 后面的部分，不是完整 URL。也可以不把凭据写入文件，改用环境变量：

```powershell
$env:FEISHU_APP_ID = "cli_xxxxxxxxxxxxxxxx"
$env:FEISHU_APP_SECRET = "your-app-secret"
$env:FEISHU_ROOT_TOKEN = "your-writable-feishu-folder-token"
```

环境变量优先于本地配置。`tools/feishu-design-sync.local.json` 已被 `.gitignore` 忽略，不要提交真实密钥或目录 token。

在飞书开放平台为该应用开通用户身份权限 `drive:drive`（云空间）、`docs:document.media:upload`（上传导入源）、`docx:document`（读取文档块）、`board:whiteboard:node:create`（创建画板节点）和 `board:whiteboard:node:read`（回读画板节点）。这些是默认 Board 模式所需权限；显式使用 `-MermaidMode native` 时只需要前三项基础权限。Board 模式使用 `docx:document` 创建 Board 块、删除 Mermaid 占位段落及枚举 Board 块。错误码 `99991679` 表示当前用户令牌缺少接口权限；错误码 `1061004` 表示当前调用身份对请求中的目标资源没有编辑权限。后台新增权限并发布配置后，必须使用 `-Login` 重新授权，旧令牌不会自动继承新增权限。

导入源上传遵循飞书 `ccm_import_open` 协议：不发送 `parent_node`，并通过 `extra` 声明目标文档类型和源文件扩展名。目标文件夹 token 仅用于后续导入任务的 `mount_key`。同步脚本会从 manifest 的 `source` 字段取得父目录：根级源文件直接挂载到配置的根文件夹，嵌套源文件逐级创建或复用同名飞书文件夹，再把最终父文件夹 token 作为该文档的 `mount_key`。

把以下地址登记为 OAuth 重定向 URL：

```text
http://127.0.0.1:8765/feishu/oauth/callback
```

应用不需要公开上架，但 OAuth 应用配置和用户身份权限必须在当前可用版本中生效。实际文件访问权限来自浏览器中完成授权的个人账号，因此目标文件夹应位于该账号自己的云空间中。

## 2. 一键同步

配置完成后，无参数执行即可同步：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1
```

Windows 下也可以直接运行：

```text
tools\sync_design_docs_to_feishu.cmd
```

首次正式运行会打开默认浏览器。默认 Board 模式要求目标个人飞书账号同意 `drive:drive`、`docs:document.media:upload`、`docx:document` 以及 Board 节点读写权限。显式使用 `-MermaidMode native` 时不请求 Board 权限。浏览器回调本机端口后，脚本保存用户访问令牌和刷新令牌并继续执行。后续运行会复用或自动刷新令牌；若缓存缺少当前模式所需权限，脚本会自动要求重新登录授权。

每次执行会自动完成：

1. 检查 Mermaid 校验依赖，首次缺失时安装到被忽略的 `artifacts/mermaid-validation`。
2. 使用 Mermaid 官方解析器校验 `Docs/design` 下的全部图表，坏图会阻断发布并生成报告。
3. 重新生成 `artifacts/feishu-design-export`；默认 Board 模式把 Mermaid 替换为稳定占位符并提取独立 `.mmd` 资产。
4. 根据导出 Markdown、Mermaid 模式及相关渲染契约生成 SHA-256 内容指纹与增量计划。
5. 按 manifest 的源文件父路径规划远端目录；根级文件留在配置的根文件夹，其余文件逐级镜像目录。
6. 正式同步时先查找同一父目录下的同名文件夹，只在不存在时创建；每解析一级目录都立即保存路径与 token 映射。
7. 创建新增文档、跳过未变文档，并按配置处理已变文档。
8. 默认 Board 模式在导入成功后解析 Mermaid 语义，在占位符位置创建 Docx Board 块，以返回的 `board.token` 写入可编辑形状和连接线；显式传入 `-MermaidMode native` 才保留源码代码块。
9. Board 模式下只有节点全部写入成功才删除占位符；失败时回滚未完成的 Board 块，不降级为 PNG。
10. 每创建一个导入任务和每完成一篇文档都立即更新本地检查点，异常退出后可继续轮询既有 ticket，不重复创建页面。

凭据未配置完整时，脚本会自动退化为预览，不会打开登录页面或调用飞书写接口。

## 3. 安全预览

只生成完整计划、不写飞书也不改同步状态：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1 -Preview
```

需要同时在终端列出每篇文档时：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1 -Preview -ListDocuments
```

计划中的 `TargetFolder` 列会显示每篇文档预期挂载的相对目录。Preview 只计算目录路径，不枚举或创建远端文件夹，也不保存目录 token。

计划输出到：

```text
artifacts/feishu-design-export/feishu-sync-plan.md
```

## 4. 增量策略

| 动作 | 含义 | 默认行为 |
|------|------|----------|
| `create` | 本地文档从未发布 | 创建飞书文档并保存 token、URL 和摘要 |
| `skip-unchanged` | 文档与 Board 内容指纹未变化 | 不调用飞书 API |
| `changed-needs-update` | 已发布文档内容变化 | 默认阻断，不创建重复页面 |
| `duplicate-reimport` | 显式传入 `-AllowDuplicateReimport` | 创建替代页面并更新本地源文件到新页面的映射 |
| `force-reimport` | 同时传入 `-Force` 和 `-AllowDuplicateReimport` | 即使指纹未变化也创建替代页面 |
| `local-deleted` | 状态中存在但本地源已删除 | 只写入计划，不自动删除远端页面 |

默认 `changedDocumentMode` 为 `block`。飞书导入任务 API 只能创建文档，不能凭已有 document token 原位覆盖正文；因此已有映射的内容发生变化时，脚本默认报错并保留原页面。只有明确接受替代页面后才使用 `-AllowDuplicateReimport`。导入任务超时或进程中断时，本地检查点保留 ticket；下次执行先恢复轮询该任务，不会再次上传并创建同一页面。

目录层级只影响后续新建的导入任务。启用该能力前已经同步到根目录的文档不会被自动移动，也不会为了改变位置而重新导入，因为这会破坏现有映射或产生重复页面；它们继续保持原位置。若未来需要整理这些存量页面，应单独实现并显式执行基于 Drive 移动接口的迁移流程。

`native` 模式保留 Mermaid 源码，但真实探针确认 Markdown 导入器会将 Mermaid fenced source 导成 `block_type=14` 的 Docx 代码块。网页端可在该代码块内切换源码与预览，但它不是独立的飞书绘图组件。进一步开通 `docx:document.block:convert` 用户权限并调用官方 Markdown 块转换接口后，最小 Mermaid 输入仍只返回一个 `block_type=14` 块，源码位于 `code.elements`，响应不包含 `diagram`。Docx Open API 的块契约虽然包含 `diagram` 字段，但公开可写数据只有 `diagram_type`，没有 Mermaid 源码、绘图内容或可绑定的内容 token；块更新接口也只支持富文本更新。因此当前开放 API 无法把 Mermaid 源码写入独立绘图组件。`docx:document.block:convert` 不是日常同步的必要权限，脚本不会默认申请；需要保留源码时使用 `native` 代码块，需要 API 保证可编辑图形时使用 `board`。

`board` 兼容模式通过 Docx Board 块与 Board 节点 API 写入可编辑绘图。转换器现已覆盖 `Docs/design` 中全部 575 张 Mermaid：383 张流程图、173 张时序图、14 张状态图、4 张类图和 1 张思维导图。流程图会保留 `subgraph` 分组框和标题；时序图支持 `alt`、`else`、`loop`、`opt`、`Note` 与 `autonumber`；状态图、类图和思维导图分别使用分层图或树形布局生成可编辑节点。离线审计不仅检查转换是否成功，还通过 Mermaid parser DB 核对分组、注释、自动编号、实体和关系数量，报告写入 `artifacts/feishu-board-audit/report.md`。

该结论表示当前仓库图表达到 575/575 结构与关键语义覆盖，不等同于支持 Mermaid 的全部语法。当前状态图均为扁平状态，时序图未使用激活条；未来出现复合状态、激活条或其他未映射 parser 记录时，转换器仍会显式失败，不会降级为图片或静默丢失语义。类图的继承、组合、聚合和依赖关系会保留为连接线、已验证箭头及关系标签；受 Board 公开连接线样式限制，组合/聚合的菱形端点以文字关系标签表达。

若团队要求 URL 永久稳定，需要后续实现基于飞书 Docx Block API 的正文替换与标题更新；这与当前基于 Markdown 导入任务的一键版本化同步是两种更新模型。

## 5. 常用参数

| 参数 | 用途 |
|------|------|
| `-Preview` | 校验、导出并生成计划，但不调用飞书写接口、不更新同步状态。旧参数 `-DryRun` 仍作为别名兼容。 |
| `-ListDocuments` | 在终端打印每篇文档及其计划动作。 |
| `-ConfigPath` | 指定另一份本地配置。 |
| `-ExportDir` | 临时覆盖导出目录。 |
| `-SyncStatePath` | 临时覆盖同步状态路径；适合将真实探针与正式映射隔离。 |
| `-MermaidMode` | Mermaid 发布方式。默认 `board`，转为可编辑画板节点；`native` 保留 fenced source。 |
| `-Source` | 按 manifest 的源路径精确筛选一篇文档，用于单文档预览或真实探针；零匹配或多匹配时立即失败。 |
| `-SkipMermaidValidation` | 跳过 Mermaid 校验，仅用于已由上游门禁完成校验的场景。 |
| `-SkipExport` | 复用已有导出包；若清单不存在则失败。 |
| `-Force` | 已有远端文档即使内容指纹未变化也执行版本化重导入。 |
| `-AllowDuplicateReimport` | 兼容旧调用，显式允许已变文档执行版本化重导入。 |
| `-Login` | 忽略已有用户令牌缓存，强制重新打开浏览器授权。 |
| `-VerifyBoard` | 与 `-Source` 配合执行只读服务端校验：枚举文档 Board 块，回读每块节点，并与本地 Mermaid 转换结果逐项比对；不执行远端写入。 |

`-RegenerateExport` 为旧命令兼容参数。当前默认行为已经是自动刷新导出；凭据完整且未传 `-Preview` 时即执行真实同步。

单文档 Board 探针先执行预览：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1 -Source 00-Prologue.md -Preview -ListDocuments
```

确认计划后移除 `-Preview` 才会写入飞书。若该源已有远端映射且指纹变化，默认 `block` 策略会停止同步，不创建替代页面；只有显式增加 `-AllowDuplicateReimport` 才允许创建新页面。若 Board 权限补充授权前已经创建导入 ticket，使用同一条命令并增加 `-Login`；脚本会恢复该 ticket 并继续替换占位符，不会再次创建页面：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1 -Source 00-Prologue.md -Login
```

同步完成后，可执行严格的服务端只读回读。首次补充 `board:whiteboard:node:read` 权限后需保留 `-Login` 重新授权；后续校验可省略 `-Login`：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/sync_design_docs_to_feishu.ps1 -Source 00-Prologue.md -VerifyBoard -Login -SkipExport -SkipMermaidValidation
```

该命令不创建、修改或删除飞书内容。任何 API 错误、Board 数量不符或节点数量不符都会立即失败；只有成功读取全部节点后才打印计数。当前单文档探针的预期结果为 6 个 Board，节点数依次为 `22,15,23,11,14,11`，总计 96。

## 6. 输出与状态

默认输出结构：

```text
artifacts/feishu-design-export/
  README.md
  manifest.json
  manifest.md
  feishu-sync-plan.md
  feishu-sync-state.local.json
  markdown/
  mermaid/
  feishu-user-token.local.json
  html/
```

- `markdown/`：飞书导入友好的 Markdown；默认 Board 模式将 Mermaid 替换为占位符，`native` 模式保留 fenced source。
- `mermaid/`：Board 模式提取的 `.mmd` 源码，同步时转换为可编辑 Board 节点。
- `html/`：用于人工检查的静态预览。
- `manifest.json`：以 `source` 为稳定主键的机器可读清单。
- `manifest.md`：文档标题、顺序和结构统计。
- `feishu-sync-plan.md`：本次增量动作和远端映射。
- `feishu-sync-state.local.json`：逐文档检查点，保存内容摘要、远端 token、URL、时间和导入 ticket，并保存当前根目录下的相对文件夹路径到飞书 folder token 的映射；重新导出不会删除此文件。
- `feishu-user-token.local.json`：个人 OAuth 访问令牌与刷新令牌，仅保存在被 Git 忽略的本地输出目录。

预览模式不会发起 OAuth 登录，也不会写 `feishu-sync-state.local.json`，避免预览后把未发布内容错误标记为已同步。

## 7. 高级默认值

模板只暴露日常需要配置的字段。以下值由脚本提供默认值，通常无需写入本地配置：

- 认证模式：`user-oauth`
- OAuth 回调：`http://127.0.0.1:8765/feishu/oauth/callback`
- 用户令牌缓存：`artifacts/feishu-design-export/feishu-user-token.local.json`
- 源目录：`Docs/design`
- 导出目录：`artifacts/feishu-design-export`
- 飞书 API：`https://open.feishu.cn/open-apis`
- 目标挂载类型：文件夹 `1`
- 源目录层级：默认保留；可在本地配置的 `target.preserveSourceHierarchy` 中设为 `false` 以恢复扁平发布
- 目录恢复：优先复用本地检查点；映射缺失时枚举父目录并按名称恢复，确认不存在后才创建
- 导入格式：Markdown 到 Docx
- 请求超时：120 秒
- 导入轮询：每 3 秒一次，最长 180 秒；超时后保留 ticket 供下次恢复
- Mermaid 展示：默认 Board 模式语义转换后写入可编辑节点，失败时不使用图片兜底；`native` 模式保留原始源码并交给飞书 Markdown 导入器

租户接口路径确有差异时，仍可在本地配置中增加 `apiBaseUrl`、`sourceDir`、`exportDir`、`syncStatePath`、`oauth`、超时字段，以及 `import.endpointPaths` 覆盖默认值。旧的企业应用身份模式仍可通过 `"authMode": "tenant-app"` 启用。

## 8. PPT 深入阅读链接

公司内训 PPT 的文档跳转以 [`00-PresentationAndFeishuNavigation.md`](00-PresentationAndFeishuNavigation.md) 为总入口。PPT 中不要写入本地 Markdown 相对路径或源码路径；正式上传完成后，从 `feishu-sync-state.local.json` 中按源文件键读取 `feishuDocumentUrl`，再写入 PPT 超链接。

当前工具不会把 Markdown 中的跨文档相对链接自动改写成目标飞书页面 URL，并且版本化重导入可能生成新的页面 URL。因此每次批量更新文档后，应重新核对 PPT 使用的 URL。建议至少验收导航总页、MOBA 总览、Shooter 总览、网络同步能力地图、玩法能力地图和工程质量入口六类链接。
