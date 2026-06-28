# CutLab 安装与打包

## 最终用户安装

### 方式 A：安装程序（推荐）

1. 获取 `dist/CutLab-Setup-1.0.0.exe`
2. 双击运行，按向导完成安装（安装向导默认**简体中文**）
3. 从开始菜单或桌面启动 **CutLab**

无需单独安装 .NET 运行时（自包含发布）。

### 方式 B：ZIP 便携版

1. 解压 `dist/CutLab-win-x64-1.0.0.zip` 到任意目录
2. 运行 `CutLab.exe`

---

## 开发者：构建安装包

### 前置条件

- .NET 10 SDK
- Windows 10/11 x64
- （可选）[Inno Setup 6](https://jrsoftware.org/isdl.php) — 用于生成 `.exe` 安装向导

### 一键构建

```powershell
cd G:\3.Projects_Indpnd\CutLab
.\scripts\build-installer.ps1
```

脚本会依次：

1. 运行 `dotnet test`
2. `dotnet publish` 自包含发布到 `publish/win-x64/`
3. 生成 ZIP 便携包到 `dist/CutLab-win-x64-1.0.0.zip`
4. 若检测到 Inno Setup，生成 `dist/CutLab-Setup-1.0.0.exe`

### 仅发布、不打包安装程序

```powershell
.\scripts\build-installer.ps1 -SkipInstaller
```

### 手动发布

```powershell
dotnet publish src/CutLab.App/CutLab.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -o publish/win-x64
```

---

## 可选依赖

| 功能 | 依赖 |
|------|------|
| 核心功能 | 无 |
| 生成预览视频 | FFmpeg（需在 PATH 中） |
| Excel 导出 | 无（内置 MiniExcel） |

---

## 卸载

通过 **设置 → 应用 → CutLab → 卸载**，或开始菜单中的「卸载 CutLab」。

用户数据保存在：

`%LOCALAPPDATA%\CutLab\projects\`

卸载程序不会自动删除项目 JSON，如需清理请手动删除上述目录。
