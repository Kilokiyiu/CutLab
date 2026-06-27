# CutLab

动画镜头卡号（Cut ID）生产管理工具 — C# + Avalonia + DDD

## 功能

- 批量规范化重命名（预览 / 撤销）
- 缺卡检测与插卡后缀缺漏检测
- 自动归档目录创建与文件移动
- 版本标签筛选、进度台账 Excel 导出
- FFmpeg 分镜预览视频合成
- 多项目工作区、卡号缩略图预览

## 安装（Windows）

见 [安装与打包指南](docs/INSTALL.md)。

构建安装包：

```powershell
.\scripts\build-installer.ps1
```

产物位于 `dist/` 目录。

## 开发

```bash
dotnet build
dotnet test
dotnet run --project src/CutLab.App
```

## 文档

- [项目规划](docs/PROJECT_PLAN.md)
- [架构设计](docs/ARCHITECTURE.md)
- [安装与打包](docs/INSTALL.md)
- [开发进度](docs/PROGRESS.md)
