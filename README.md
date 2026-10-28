# SURS - 妇科超声结构化报告系统

SURS（Structured Ultrasound Reporting System）是一个基于 **WPF + .NET 9** 的妇科超声结构化报告工具，提供结构化录入、O-RADS 分级辅助、实时预览与 PDF 导出。

## 功能概览

- 结构化报告录入：报告头、子宫、子宫内膜、附件、盆腔积液、结论等模块。
- 动态表单联动：按病变类型显示对应字段。
- O-RADS 分级辅助与结果卡片。
- 实时报告预览、显示/隐藏、缩放与重置。
- A4 PDF 导出与 Windows x64 单文件发布。
- 激活卡 + 账号密码登录；纯离线、首次使用起算的 7 天授权。

## 纯离线授权

软件不访问外网。首次启动时输入发卡方提供的激活卡号、账号和密码，授权从该电脑首次激活当刻起计算 7 天（精确 168 小时）。之后每次启动只需账号和密码。

- 卡号使用 RSA-SHA256 签名，软件只内置公钥。
- 密码以 PBKDF2-HMAC-SHA256 散列保存在签名卡内。
- 同一张卡可在不同电脑分别激活，但在同一台电脑只能消费一次。
- 续期必须签发新卡；旧卡在本机永久不可复用。
- 支持主状态、ProgramData 影子副本和 HKCU 注册表镜像，以及时钟回拨拦截。

详细协议、医院操作手册、60 家发卡台账和安全边界见：

- [登录与授权设计](docs/登录与授权设计.md)

## 技术栈

- 运行平台：.NET 9 / `net9.0-windows`
- 桌面框架：WPF
- 架构模式：MVVM
- 主要依赖：
  - `CommunityToolkit.Mvvm`
  - `QuestPDF`

## 目录结构

```text
SURS/
├── SURS.App/                    # WPF 应用（UI、ViewModel、授权与 PDF 服务）
│   ├── Services/License/        # 授权协议、机器指纹、授权服务、内置公钥
│   ├── ViewModels/              # 主流程与登录 ViewModel
│   ├── LoginWindow.xaml         # 激活/登录/续期页面
│   └── MainWindow.xaml          # 主报告界面
├── SURS.Core/                   # 核心抽象与领域定义
├── SURS.Infrastructure/         # 基础设施实现
├── tools/
│   ├── SURS.LicenseTool/        # 离线发卡、台账、验签、解析工具
│   └── SURS.LicenseSelfTest/    # 无 UI 授权协议/生命周期/防重置自检
├── docs/                        # 设计与操作文档
└── SURS.sln                     # 解决方案（含 App、发卡工具、自检）
```

## 环境要求

- Windows 10/11
- .NET 9 SDK
- 构建 WPF 工程需要恢复 NuGet 依赖

## 快速开始

### 1) 恢复依赖

```powershell
dotnet restore SURS.sln
```

### 2) 本地运行

首次运行前需要一张由发卡工具签发的授权卡：

```powershell
dotnet run --project tools/SURS.LicenseTool -- init
dotnet run --project tools/SURS.LicenseTool -- issue --note "测试医院"
dotnet run --project SURS.App/SURS.App.csproj
```

将 `issue` 输出的激活卡号、账号和密码填入首次激活页面。

### 3) 授权自检

```powershell
dotnet run --project tools/SURS.LicenseSelfTest -c Debug
```

### 4) 台账查询

```powershell
dotnet run --project tools/SURS.LicenseTool -- list
dotnet run --project tools/SURS.LicenseTool -- list --account hos01
dotnet run --project tools/SURS.LicenseTool -- list --note "测试医院"
```

## 构建与发布

### Debug 构建

```powershell
dotnet build SURS.sln -c Debug
```

### 发布 Windows x64 单文件（自包含）

```powershell
dotnet publish SURS.App/SURS.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true `
  -o ./Publish
```

发布后可执行文件默认为：

```text
./Publish/SURS.App.exe
```

## 发卡安全

- RSA 私钥默认保存在 `%LOCALAPPDATA%\SURS.LicenseTool\.keys\`，严禁提交仓库或随软件发布。
- 仓库中的 `.gitignore` 已排除 `tools/**/.keys/`。
- 台账默认保存在 `%LOCALAPPDATA%\SURS.LicenseTool\ledger.jsonl`，含明文密码，必须按敏感文件保护。
- 正式交付前应重新生成正式密钥对，并替换 `EmbeddedLicenseKey.cs` 中的演示公钥后重新发布。
- 纯离线方案无法提前停用已发出的有效卡；需要集中停用或院内限定时应升级为内网授权点。

## 开发入口

- 应用入口：`SURS.App/App.xaml`、`SURS.App/App.xaml.cs`
- 登录/激活：`SURS.App/LoginWindow.xaml`、`SURS.App/ViewModels/LoginViewModel.cs`
- 主流程：`SURS.App/ViewModels/MainViewModel.cs`
- PDF：`SURS.App/Services/PdfService.cs`
- O-RADS：`SURS.App/Services/ORadsCalculator.cs`
- 授权协议：`SURS.App/Services/License/LicenseCrypto.cs`
- 授权状态机：`SURS.App/Services/License/LicenseService.cs`

## 文档索引

- [登录与授权设计（含医院操作手册）](docs/登录与授权设计.md)
- 架构设计：`docs/架构设计文档.md`
- 架构实施：`docs/架构实施指南.md`
- 模块拆分：`docs/模块拆分详细方案.md`
- O-RADS 实现：`docs/O-RADS实现说明.md`
- 实施检查：`docs/实施检查清单.md`

## 许可

本项目采用 MIT License。