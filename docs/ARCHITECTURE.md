# CutLab 架构设计（DDD）

> C# + Avalonia · 领域驱动设计  
> 版本：v0.1 · 2026-06-24

---

## 目录

1. [架构概览](#1-架构概览)
2. [限界上下文](#2-限界上下文)
3. [领域模型](#3-领域模型)
4. [应用层用例](#4-应用层用例)
5. [基础设施与防腐层](#5-基础设施与防腐层)
6. [表示层（Avalonia）](#6-表示层avalonia)
7. [项目结构](#7-项目结构)
8. [依赖规则](#8-依赖规则)
9. [MVP 实施顺序](#9-mvp-实施顺序)

---

## 1. 架构概览

CutLab 采用 **经典 DDD 四层架构**，核心逻辑全部收敛在领域层，UI 与文件 IO 均为可替换的外围适配器。

```
┌─────────────────────────────────────────────────────────┐
│  CutLab.App (Presentation)                              │
│  Avalonia Views · ViewModels · 导航 · 对话框             │
├─────────────────────────────────────────────────────────┤
│  CutLab.Application (Application)                       │
│  Commands / Queries · Handlers · DTOs · 用例编排         │
├─────────────────────────────────────────────────────────┤
│  CutLab.Domain (Domain)                                 │
│  聚合 · 实体 · 值对象 · 领域服务 · 仓储接口 · 领域事件    │
├─────────────────────────────────────────────────────────┤
│  CutLab.Infrastructure (Infrastructure)                 │
│  仓储实现 · 文件系统 · JSON 持久化 · Excel · FFmpeg     │
└─────────────────────────────────────────────────────────┘
```

### 1.1 为什么 CutLab 适合 DDD

| 特征 | CutLab 中的体现 |
|------|-----------------|
| **复杂业务规则** | 卡号模板渲染、多种野生命名识别、插卡重编号 |
| **明确领域语言** | 卡号、分镜、原画、缺卡、归档——与制片术语一致 |
| **状态与一致性** | 批量重命名 + 撤销、归档移动不可部分成功 |
| **可测试性** | 领域层零 IO，规则可单元测试 |
| **演进空间** | v2 进度追踪、FFmpeg 预览作为新用例接入，不污染领域 |

### 1.2 架构风格选择

- **MVP 阶段**：单一限界上下文 `Production`（生产管理），避免过度拆分
- **CQRS 轻量应用**：写操作用 Command，读操作用 Query；不引入 Event Sourcing
- **无分布式**：本地桌面，领域事件用于应用层编排（如扫描完成后触发缺卡检测）

---

## 2. 限界上下文

### 2.1 上下文地图（MVP）

```
                    ┌──────────────────┐
                    │   Production     │  ← MVP 唯一核心上下文
                    │  (镜头生产管理)   │
                    └────────┬─────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
        ┌──────────┐  ┌──────────┐  ┌──────────┐
        │ 文件系统  │  │ 本地配置  │  │ 报表导出  │
        │  (OS)    │  │  (JSON)  │  │ (Excel)  │
        └──────────┘  └──────────┘  └──────────┘
           防腐层        防腐层         防腐层
```

### 2.2 Production 上下文职责

| 职责 | 说明 |
|------|------|
| 项目管理 | 卡号规则、归档模板、工作根路径 |
| 镜头注册表 | 卡号序列、缺卡、插卡 |
| 资产发现 | 扫描文件夹、识别、提议新名 |
| 文件操作 | 重命名、移动、建目录、撤销 |
| 生产报表 | 卡号清单、缺卡清单、进度（v2） |

### 2.3 未来可拆分上下文（v2+）

| 上下文 | 触发条件 |
|--------|----------|
| `Preview` | FFmpeg 预览合成独立演进 |
| `TimeSheet` | 若接入帧级摄影表，与 Production 通过 `CutId` 关联 |

---

## 3. 领域模型

### 3.1 通用语言（Ubiquitous Language）

| 术语 | 领域类型 | 说明 |
|------|----------|------|
| 动画项目 | `AnimationProject` | 一部作品/一集的工作单元 |
| 卡号 | `CutNumber` | 值对象：EP + SC + CUT |
| 镜头 | `Cut` | 实体：一个卡号对应的制作单元 |
| 命名规范 | `NamingConvention` | 值对象/实体：模板与后缀规则 |
| 归档模板 | `ArchiveTemplate` | 值对象：目录结构模式 |
| 资产 | `ProductionAsset` | 实体：磁盘上的一个生产文件 |
| 扫描会话 | `ScanSession` | 聚合根：一次文件夹扫描的结果 |
| 操作批次 | `OperationBatch` | 聚合根：一批可撤销的文件操作 |
| 缺卡 | `MissingCut` | 值对象：范围内缺失的卡号 |

### 3.2 聚合设计

#### 聚合 1：`AnimationProject`（项目聚合）

**一致性边界**：项目的命名规则、归档模板、默认场集号不可与外部 Cut 状态混在同一事务。

```
AnimationProject (Aggregate Root)
├── ProjectId          : Guid
├── Name               : string
├── Episode            : EpisodeNumber        [VO]
├── NamingConvention   : NamingConvention     [VO]
├── ArchiveTemplate    : ArchiveTemplate      [VO]
├── RootPath           : WorkspacePath        [VO]
├── RecognitionRules   : IReadOnlyList<RecognitionPattern> [VO]
└── CreatedAt / UpdatedAt
```

**领域行为**：
- `UpdateNamingConvention(convention)` — 校验模板语法
- `UpdateArchiveTemplate(template)` — 校验路径模式
- `SetRootPath(path)` — 校验路径存在性（通过领域服务）

**不变量**：
- 命名模板必须包含 `{CUT}` 占位符
- 归档模板子目录名不可为空、不可含非法字符

---

#### 聚合 2：`CutRegistry`（镜头注册表聚合）

**一致性边界**：一个集/场范围内的卡号序列、插卡、缺卡逻辑。

```
CutRegistry (Aggregate Root)
├── RegistryId         : Guid
├── ProjectId          : ProjectId            [VO]
├── Scope              : CutScope             [VO]  // EP01 S02, C001-C030
├── Cuts               : IReadOnlyList<Cut>   [Entity]
└── ...

Cut (Entity)
├── CutNumber          : CutNumber            [VO]
├── Status             : CutProductionStatus  [VO/Enum]
├── Note               : string?
└── VersionTag         : VersionTag?          [VO]  // v2: v1, draft, s
```

**领域行为**：
- `RegisterCut(cutNumber)` — 注册已知镜头
- `InsertCut(after, newCut)` — 插卡（v1.5），生成 C003b
- `DetectGaps()` → `IReadOnlyList<MissingCut>` — 缺卡检测
- `MarkStatus(cutNumber, status)` — 更新制作状态（v2）

**不变量**：
- 同 Scope 内 `CutNumber` 唯一
- 插卡后缀规则：字母序 b, c, d…

---

#### 聚合 3：`ScanSession`（扫描会话聚合）

**一致性边界**：一次扫描的识别结果与提议命名，扫描完成前可反复添加。

```
ScanSession (Aggregate Root)
├── SessionId          : Guid
├── ProjectId          : ProjectId
├── SourcePath         : WorkspacePath
├── ScannedAt          : DateTimeOffset
├── Assets             : IReadOnlyList<ProductionAsset>
└── ...

ProductionAsset (Entity)
├── AssetId            : Guid
├── OriginalPath       : FilePath             [VO]
├── ParsedCut          : CutNumber?           [VO]  // null = 未识别
├── AssetType          : AssetType?           [VO]  // 原画/分镜/...
├── ProposedFileName   : FileName?            [VO]
├── RecognitionStatus  : RecognitionStatus    [Enum]
└── ConflictWith       : AssetId?             // 目标名冲突
```

**领域行为**：
- `AddDiscoveredFile(path, parsed, proposed)` — 加入扫描结果
- `MarkUnrecognized(path, reason)` — 标记无法识别
- `ResolveConflict(assetId, strategy)` — 冲突处理策略
- `GetRecognized()` / `GetUnrecognized()` / `GetConflicts()` — 分类查询

**不变量**：
- 同一会话内 `OriginalPath` 唯一
- `ProposedFileName` 若存在，须通过 `NamingConvention` 校验

---

#### 聚合 4：`OperationBatch`（操作批次聚合）

**一致性边界**：批量重命名/移动/建目录的可撤销单元。

```
OperationBatch (Aggregate Root)
├── BatchId            : Guid
├── ProjectId          : ProjectId
├── OperationType      : BatchOperationType   // Rename | Move | CreateDirs
├── Status             : BatchStatus          // Pending | Applied | Undone
├── Entries            : IReadOnlyList<FileOperationEntry>
├── AppliedAt          : DateTimeOffset?
└── ...

FileOperationEntry (Entity)
├── EntryId            : Guid
├── Kind               : OperationKind        // Rename | Move | MkDir
├── SourcePath         : FilePath
├── TargetPath         : FilePath
└── Success            : bool
```

**领域行为**：
- `Apply()` — 标记为已执行（实际 IO 在基础设施）
- `Undo()` — 逆序回滚，仅当 Status = Applied
- `CanUndo()` → bool

**不变量**：
- 仅最近一个 Applied 批次可撤销（MVP 策略）
- Undo 必须逆序执行 Entries

---

### 3.3 值对象

```csharp
// 示例签名，实现时放在 CutLab.Domain/ValueObjects/

public readonly record struct CutNumber(int Episode, int Scene, int Cut, string? InsertSuffix = null)
{
    public string ToDisplayString(NamingConvention convention) => ...;
    public bool IsInRange(CutScope scope) => ...;
}

public readonly record struct EpisodeNumber(int Value);

public readonly record struct NamingConvention(
    string Template,
    string Separator,
    IReadOnlyDictionary<AssetType, string> TypeSuffixes);

public readonly record struct ArchiveTemplate(
    string PathPattern,
    IReadOnlyList<string> FolderNames);

public readonly record struct CutScope(
    EpisodeNumber Episode,
    int Scene,
    CutNumber From,
    CutNumber To);

public readonly record struct WorkspacePath(string Value);
public readonly record struct FilePath(string Value);
public readonly record struct VersionTag(string Value);  // v1, draft, s
```

### 3.4 领域服务

| 服务 | 职责 | 原因 |
|------|------|------|
| `INamingService` | 模板渲染、提议文件名生成 | 跨 `NamingConvention` + `CutNumber` + `AssetType` |
| `IRecognitionService` | 从文件名解析卡号 | 多模式正则，不属于单一实体 |
| `IGapAnalysisService` | 范围缺卡 + 疑似未识别 | 跨 `CutRegistry` + `ScanSession` |
| `IArchivePathResolver` | 根据模板解析目标目录 | 跨 `ArchiveTemplate` + `CutNumber` |

```csharp
public interface INamingService
{
    Result<FileName> GenerateFileName(
        NamingConvention convention,
        CutNumber cut,
        AssetType type,
        string extension);
}

public interface IRecognitionService
{
    RecognitionResult TryParse(
        string fileName,
        IReadOnlyList<RecognitionPattern> patterns,
        NamingConvention defaultConvention);
}
```

### 3.5 仓储接口

```csharp
public interface IAnimationProjectRepository
{
    Task<AnimationProject?> GetByIdAsync(ProjectId id, CancellationToken ct = default);
    Task SaveAsync(AnimationProject project, CancellationToken ct = default);
    Task<IReadOnlyList<AnimationProject>> ListRecentAsync(int count, CancellationToken ct = default);
}

public interface ICutRegistryRepository
{
    Task<CutRegistry?> GetByProjectAsync(ProjectId projectId, CutScope scope, CancellationToken ct = default);
    Task SaveAsync(CutRegistry registry, CancellationToken ct = default);
}

public interface IOperationBatchRepository
{
    Task SaveAsync(OperationBatch batch, CancellationToken ct = default);
    Task<OperationBatch?> GetLastAppliedAsync(ProjectId projectId, CancellationToken ct = default);
}
```

`ScanSession` MVP 可内存持有，不必持久化；关闭应用前若需恢复可 v1.1 加入 `IScanSessionRepository`。

### 3.6 领域事件

| 事件 | 触发时机 | 订阅方（应用层） |
|------|----------|------------------|
| `ScanSessionCompleted` | 扫描结束 | 刷新 UI、可选自动缺卡检测 |
| `OperationBatchApplied` | 批量操作成功 | 更新 CutRegistry、写日志 |
| `OperationBatchUndone` | 撤销完成 | 刷新 UI |
| `MissingCutsDetected` | 缺卡分析完成 | 推送缺卡面板 |

```csharp
public sealed record ScanSessionCompleted(
    Guid SessionId,
    ProjectId ProjectId,
    int TotalFiles,
    int RecognizedCount,
    int UnrecognizedCount) : IDomainEvent;
```

---

## 4. 应用层用例

应用层 **不含业务规则**，只负责：加载聚合 → 调用领域行为 → 协调基础设施 → 持久化 → 发布领域事件。

### 4.1 Commands（写）

| Command | Handler 职责 |
|---------|-------------|
| `CreateProjectCommand` | 创建 `AnimationProject` 并持久化 |
| `UpdateProjectSettingsCommand` | 更新命名/归档规则 |
| `ScanFolderCommand` | 调文件网关扫描 → 构建 `ScanSession` → `RecognitionService` |
| `ExecuteRenameCommand` | 从 ScanSession 生成 `OperationBatch` → 文件网关执行 → 持久化 |
| `ExecuteArchiveCommand` | 建目录 / 移动文件 → `OperationBatch` |
| `UndoLastOperationCommand` | 加载最近 Batch → `Undo()` → 文件网关逆操作 |
| `InsertCutCommand` (v1.5) | `CutRegistry.InsertCut` + 级联重命名 |

### 4.2 Queries（读）

| Query | 返回 |
|-------|------|
| `GetProjectQuery` | `ProjectDto` |
| `GetScanPreviewQuery` | `ScanPreviewDto`（旧名→新名列表） |
| `GetMissingCutsQuery` | `MissingCutsReportDto` |
| `GetOperationHistoryQuery` | 最近操作批次摘要 |

### 4.3 应用层目录

```
CutLab.Application/
├── Projects/
│   ├── CreateProject/
│   │   ├── CreateProjectCommand.cs
│   │   └── CreateProjectHandler.cs
│   └── UpdateProjectSettings/
├── Scanning/
│   ├── ScanFolder/
│   └── GetScanPreview/
├── Operations/
│   ├── ExecuteRename/
│   ├── ExecuteArchive/
│   └── UndoLastOperation/
├── Reporting/
│   ├── GetMissingCuts/
│   └── ExportCutList/          # v1.1
├── Common/
│   ├── Interfaces/
│   │   ├── IFileSystemGateway.cs
│   │   └── IUnitOfWork.cs
│   └── Behaviors/
│       └── ValidationBehavior.cs
└── DependencyInjection.cs
```

### 4.4 文件系统网关（应用层端口）

领域层不直接访问磁盘；应用层定义端口，基础设施实现：

```csharp
public interface IFileSystemGateway
{
    IAsyncEnumerable<string> EnumerateFilesAsync(
        WorkspacePath root, bool recursive, CancellationToken ct);

    Task ApplyOperationsAsync(
        OperationBatch batch,
        IProgress<OperationProgress>? progress,
        CancellationToken ct);

    Task RevertOperationsAsync(
        OperationBatch batch,
        CancellationToken ct);
}
```

---

## 5. 基础设施与防腐层

### 5.1 职责

| 组件 | 实现 |
|------|------|
| `JsonProjectRepository` | `AnimationProject` → `projects/{id}.json` |
| `JsonCutRegistryRepository` | 镜头注册表持久化 |
| `JsonOperationBatchRepository` | 操作日志 / 撤销 |
| `LocalFileSystemGateway` | `System.IO` 封装，路径规范化 |
| `RegexRecognitionService` | `IRecognitionService` 实现 |
| `TemplateNamingService` | `INamingService` 实现 |
| `MiniExcelExportAdapter` | Excel 导出（v1.1） |
| `FfmpegPreviewAdapter` | FFmpeg 调用（v2） |

### 5.2 持久化映射（防腐）

JSON 文件是 **基础设施细节**，不泄漏到领域层：

```
Domain Model          →    Persistence DTO (Infrastructure)
─────────────────────────────────────────────────────────────
AnimationProject      →    AnimationProjectJson
CutRegistry           →    CutRegistryJson
OperationBatch          →    OperationBatchJson
```

使用 explicit mapper 类，禁止领域实体直接 `[JsonSerializable]`。

### 5.3 Unit of Work

MVP 本地单用户，UoW 可简化为：

```csharp
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
```

一次 Command 结束时 `CommitAsync` 写 JSON；失败则不提交。

---

## 6. 表示层（Avalonia）

### 6.1 MVVM + 应用层

ViewModel **只依赖 Application**，不引用 Infrastructure 或 Domain 聚合：

```
View → ViewModel → IMediator / ICommandHandler → Application → Domain
```

推荐使用 **CommunityToolkit.Mvvm** + 手动 Handler 注入（MVP 不强制 MediatR，可选）。

### 6.2 主要 ViewModel

| ViewModel | 对应用例 |
|-----------|----------|
| `MainWindowViewModel` | 导航、当前项目 |
| `ProjectSettingsViewModel` | `CreateProject` / `UpdateProjectSettings` |
| `ScanViewModel` | `ScanFolder` / `GetScanPreview` |
| `RenameViewModel` | `ExecuteRename` / `UndoLastOperation` |
| `GapDetectionViewModel` | `GetMissingCuts` |
| `ArchiveViewModel` | `ExecuteArchive` |

### 6.3 UI 模块

```
CutLab.App/
├── Views/
│   ├── MainWindow.axaml
│   ├── ProjectSettingsView.axaml
│   ├── ScanView.axaml
│   ├── GapDetectionView.axaml
│   └── ArchiveView.axaml
├── ViewModels/
├── Converters/
├── Services/
│   └── DialogService.cs          # 文件浏览对话框等 UI 专属
└── App.axaml.cs
```

---

## 7. 项目结构

```
CutLab/
├── docs/
│   ├── PROJECT_PLAN.md
│   └── ARCHITECTURE.md              # 本文档
├── src/
│   ├── CutLab.Domain/
│   │   ├── Projects/                # AnimationProject 聚合
│   │   ├── Cuts/                    # CutRegistry 聚合
│   │   ├── Scanning/                # ScanSession 聚合
│   │   ├── Operations/              # OperationBatch 聚合
│   │   ├── ValueObjects/
│   │   ├── Services/                # 领域服务接口
│   │   ├── Events/
│   │   └── Common/                  # Result, Entity, AggregateRoot
│   ├── CutLab.Application/
│   │   ├── Projects/
│   │   ├── Scanning/
│   │   ├── Operations/
│   │   ├── Reporting/
│   │   └── Common/
│   ├── CutLab.Infrastructure/
│   │   ├── Persistence/
│   │   ├── FileSystem/
│   │   ├── Recognition/
│   │   ├── Naming/
│   │   └── Export/
│   └── CutLab.App/
│       ├── Views/
│       ├── ViewModels/
│       └── Services/
├── tests/
│   ├── CutLab.Domain.Tests/
│   └── CutLab.Application.Tests/
├── templates/
├── CutLab.sln
└── README.md
```

---

## 8. 依赖规则

```
CutLab.App              → Application
CutLab.Application      → Domain
CutLab.Infrastructure   → Application, Domain
CutLab.Domain           → (无项目引用)
Tests                   → 被测项目
```

**禁止**：
- Domain 引用 Infrastructure / App
- ViewModel 直接 new 领域实体或访问 `System.IO`
- JSON DTO 出现在 Domain / Application 公共 API

---

## 9. MVP 实施顺序

按 **聚合 + 用例** 纵向切片，而非先写完整个 Domain 再写 UI。

| 步骤 | 交付物 | 验证方式 |
|------|--------|----------|
| 1 | `CutNumber`, `NamingConvention`, `INamingService` | 单元测试：模板渲染 |
| 2 | `IRecognitionService` + 3 种命名模式 | 单元测试：解析 |
| 3 | `AnimationProject` 聚合 + JSON 仓储 | 创建/加载项目 |
| 4 | `ScanSession` + `ScanFolderCommand` | CLI/测试：扫描文件夹 |
| 5 | `OperationBatch` + `ExecuteRename` + Undo | 测试：改名可回滚 |
| 6 | `CutRegistry` + `GetMissingCutsQuery` | 测试：缺卡列表 |
| 7 | `ExecuteArchiveCommand` | 测试：目录创建 |
| 8 | Avalonia 壳 + Scan/Rename 界面 | 端到端手动验证 |

---

## 附录：与原规划的映射

| 原 MODULE_PLAN 模块 | DDD 归属 |
|---------------------|----------|
| `CutNumberParser` | `IRecognitionService`（Infrastructure 实现） |
| `NamingRuleEngine` | `INamingService`（Infrastructure 实现） |
| `FileScanner` | `ScanFolderHandler` + `IFileSystemGateway` |
| `RenameExecutor` | `OperationBatch` 聚合 + `IFileSystemGateway` |
| `GapDetector` | `CutRegistry.DetectGaps()` + `IGapAnalysisService` |
| `ArchiveBuilder` | `IArchivePathResolver` + `ExecuteArchiveHandler` |
| `ProjectConfigStore` | `IAnimationProjectRepository` |
| `ExportService` | `ExportCutListHandler` + Infrastructure |

---

*CutLab — 领域模型与制片流程同频。*
