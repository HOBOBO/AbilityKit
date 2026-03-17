# Unity/Tools

## AbilityKit.NewUnityProject.ps1

用于：
- 创建一个新的 Unity 工程目录（复制本仓库 `Unity/ProjectSettings` + `Unity/Packages/manifest.json` 作为基础骨架）
- 按 profile 将本仓库 `Unity/Packages/` 下的 packages 通过 Junction（目录联接）软链接到新工程的 `Packages/` 下

### 前置条件
- Windows
- PowerShell 5+（或 PowerShell 7+）

### 使用方式

1. 进入本仓库任意位置，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Unity\Tools\AbilityKit.NewUnityProject.ps1 `
  -TargetDirectory "D:\UnityProjects" `
  -ProjectName "AbilityKit.Sandbox" `
  -Profile "foundation"
```

2. 可用的 profiles 在 `Unity/Tools/Profiles/`：
- `foundation`
- `ability-runtime`
- `demo-moba`

### 常用参数
- `-Force`：如果目标工程目录已存在，则先删除再创建
- `-LinkOnly`：不创建工程骨架，仅对现有工程执行“按 profile 建立 Packages 软链接”

### 注意事项
- 脚本使用 Junction（`New-Item -ItemType Junction`），通常不需要管理员权限。
- 若你链接了 `com.abilitykit.demo.moba.entitas.generated`，它属于 generated 包：如果你改了 Entitas 组件/系统定义，需要先在 Unity 里运行 code generation 再继续后续接线。
