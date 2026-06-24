# CutLab 开发进度

## 2026-06-24

### 完成

- [x] 产品规划文档 `docs/PROJECT_PLAN.md`
- [x] DDD 架构设计 `docs/ARCHITECTURE.md`
- [x] 解决方案骨架搭建（C# + Avalonia + DDD 四层）
- [x] 领域层：4 个核心聚合占位
  - `AnimationProject` / `CutRegistry` / `ScanSession` / `OperationBatch`
- [x] 应用层：3 个 Handler
  - `CreateProject` / `ScanFolder` / `GetMissingCuts`
- [x] 基础设施层
  - `JsonProjectRepository`（项目 JSON 持久化）
  - `RegexRecognitionService`（`1卡原画` / `EP01_S02_C001_原画` 识别）
  - `TemplateNamingService`（命名模板渲染）
  - `LocalFileSystemGateway`（文件扫描，重命名/撤销待实现）
- [x] Avalonia 桌面壳 + DI 配置
- [x] 默认模板 `templates/tv-anime-standard.json` / `simple-cut.json`
- [x] 单元测试 2 个（缺卡检测、命名模板）
- [x] `dotnet build` / `dotnet test` 通过

### 明天继续

- [ ] `ExecuteRenameCommand` — 批量重命名 + dry-run + undo
- [ ] `ExecuteArchiveCommand` — 自动建目录 / 移动文件
- [ ] UI 工作流 — 项目设置 → 扫描预览 → 执行
- [ ] 补测试 — `RegexRecognitionService` 多模式识别
- [ ] `NAMING_RULE_SPEC.md` — 模板语法说明

### 验证命令

```bash
cd G:\3.Projects_Indpnd\CutLab
dotnet build
dotnet test
dotnet run --project src/CutLab.App
```
