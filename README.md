# CutLab

动画镜头卡号（Cut ID）生产管理工具 — C# + Avalonia + DDD

## 技术栈

- .NET 10
- Avalonia UI (MVVM)
- 领域驱动设计（四层架构）

## 解决方案结构

```
src/
  CutLab.Domain/           聚合、值对象、领域服务、仓储接口
  CutLab.Application/      Commands / Queries / Handlers
  CutLab.Infrastructure/   JSON 仓储、文件系统、命名与识别实现
  CutLab.App/              Avalonia 桌面应用
tests/
  CutLab.Domain.Tests/
  CutLab.Application.Tests/
docs/
  PROJECT_PLAN.md          产品规划
  ARCHITECTURE.md          DDD 架构设计
```

## 快速开始

```bash
# 还原依赖并编译
dotnet build

# 运行测试
dotnet test

# 启动桌面应用
dotnet run --project src/CutLab.App
```

## 当前进度（骨架）

- [x] DDD 四层项目结构
- [x] 核心聚合占位：`AnimationProject` / `CutRegistry` / `ScanSession` / `OperationBatch`
- [x] 应用层 Handler：`CreateProject` / `ScanFolder` / `GetMissingCuts`
- [x] 基础设施：`JsonProjectRepository` / `RegexRecognitionService` / `TemplateNamingService`
- [x] Avalonia 主窗口 + DI 配置
- [x] 项目设置 / Excel 导出 / 递归扫描
- [ ] 插卡重编号、版本标签

## 文档

- [项目规划](docs/PROJECT_PLAN.md)
- [架构设计](docs/ARCHITECTURE.md)
