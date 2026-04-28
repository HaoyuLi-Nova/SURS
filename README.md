# SURS - 超声结构化报告系统

SURS（Structured Ultrasound Reporting System）是一个基于 **WPF + .NET 9** 的妇科超声结构化报告工具。系统面向临床医生，提供结构化录入、O-RADS 分级辅助、实时预览与 PDF 导出能力，帮助提升报告标准化与出具效率。

## 功能概览

- **结构化录入**：按“报告头、子宫、子宫内膜、附件区、盆腔积液、结论”等模块分区填写，减少漏项。
- **动态表单交互**：根据病灶类型/选项变化，联动显示对应字段，降低无效输入。
- **O-RADS 分级辅助**：根据病灶特征进行分级计算并展示结果卡片。
- **实时报告预览**：录入内容变更后自动刷新预览，支持显示/隐藏预览、缩放与重置。
- **一键导出 PDF**：生成 A4 医疗报告，支持发布为单文件可执行程序。

## 技术栈

- **运行平台**：.NET 9（`net9.0-windows`）
- **桌面框架**：WPF
- **架构模式**：MVVM
- **主要依赖**：
  - `CommunityToolkit.Mvvm`（命令/属性通知）
  - `QuestPDF`（PDF 生成）

## 目录结构

```text
SURS/
├── SURS.App/                  # WPF 主应用（UI + ViewModel + 应用服务）
│   ├── Controls/              # 业务表单与公共控件
│   ├── ViewModels/            # 主流程状态与命令（MainViewModel）
│   ├── Models/                # 报告领域模型
│   ├── Services/              # PDF、对话框等应用服务
│   ├── Converters/            # XAML 数据转换器
│   ├── Helpers/               # 事件订阅与控件辅助工具
│   └── Common/                # 日志与常量
├── SURS.Core/                 # 核心抽象与领域定义（接口、实体、公共结果）
├── SURS.Infrastructure/       # 基础设施实现（依赖 Core）
├── docs/                      # 架构、模块拆分与实施文档
└── SURS.sln                   # 解决方案文件
```

## 环境要求

- Windows 10/11
- .NET 9 SDK

> 说明：`SURS.App` 为 WPF 桌面应用，请在 Windows 环境构建与运行。

## 快速开始

### 1) 克隆仓库

```bash
git clone https://github.com/yourusername/SURS.git
cd SURS
```

### 2) 还原依赖

```bash
dotnet restore SURS.sln
```

### 3) 本地运行（调试）

```bash
dotnet run --project SURS.App/SURS.App.csproj
```

## 构建与发布

### 发布为 Windows x64 单文件（自包含）

```bash
dotnet publish SURS.App/SURS.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishReadyToRun=true \
  -o ./Publish
```

发布完成后，可执行文件默认位于：

```text
./Publish/SURS.App.exe
```

## 开发说明

- 主入口：`SURS.App/App.xaml`、`SURS.App/MainWindow.xaml`
- 主流程逻辑：`SURS.App/ViewModels/MainViewModel.cs`
- PDF 生成服务：`SURS.App/Services/PdfService.cs`
- O-RADS 计算服务：`SURS.App/Services/ORadsCalculator.cs`

## 文档索引

- 架构设计：`docs/架构设计文档.md`
- 架构实施：`docs/架构实施指南.md`
- 模块拆分：`docs/模块拆分详细方案.md`
- O-RADS 实现：`docs/O-RADS实现说明.md`
- 实施检查：`docs/实施检查清单.md`

## 许可证

本项目采用 MIT License。
