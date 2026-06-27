# CutLab 开发进度

## 2026-06-25（续 2）

### 完成

- [x] **项目设置界面** — `ProjectSettingsWindow` 编辑命名/归档规则
- [x] `GetProjectHandler` / `UpdateProjectSettingsHandler`
- [x] **卡号清单导出 Excel** — `ExportCutListHandler` + MiniExcel
- [x] **递归扫描** — 「含子目录」复选框
- [x] `IFileDialogService` — 文件夹选择 + Excel 保存对话框
- [x] `docs/NAMING_RULE_SPEC.md` — 模板语法说明
- [x] 单元测试 11 个（Application 层）

### 下一步

- [ ] 插卡重编号（C003b）
- [ ] 版本标签（v1 / draft / s）
- [ ] 批量预览合成（FFmpeg）

### 验证命令

```bash
cd G:\3.Projects_Indpnd\CutLab
dotnet build
dotnet test
dotnet run --project src/CutLab.App
```

---

## 2026-06-25（续）

- [x] 自动归档、缺卡检测 UI、文件夹浏览

## 2026-06-25

- [x] 批量重命名 + 撤销 + 扫描预览

## 2026-06-24

- [x] 项目骨架 + DDD 架构 + Git 首次提交
