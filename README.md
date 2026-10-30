# SURS - 妇科超声结构化报告系统

SURS（Structured Ultrasound Reporting System）是一个基于 **WPF + .NET 9** 的妇科超声结构化报告工具，提供结构化录入、O-RADS 分级辅助、实时预览与 PDF 导出。

## 功能概览

- 结构化报告录入：报告头、子宫、子宫内膜、附件、盆腔积液、结论等模块。
- 动态表单联动：按病变类型显示对应字段。
- O-RADS 分级辅助与结果卡片。
- 实时报告预览、显示/隐藏、缩放与重置。
- A4 PDF 导出与 Windows x64 单文件发布。
- 激活卡 + 账号密码登录；纯离线、自首次激活当刻起算的 7 天授权。

## 登录与离线授权

软件不访问外网。首次在某台电脑上运行时，登录页显示三个输入框：

1. 激活卡号（交付单中的 `SURS2...` 长字符串）
2. 账号（例如 `hos01`）
3. 密码

首次激活成功后才进入主界面。之后每次启动只显示账号和密码，每次都必须登录。授权窗口从该电脑首次激活当刻起精确计算 `7 × 24 = 168` 小时。

- 激活卡使用 RSA-SHA256 签名，软件只内置公钥。
- 账号密码只用于本机身份认证，密码以 PBKDF2-HMAC-SHA256 散列保存在签名卡内。
- 同一张卡可以在不同电脑分别激活，但同一张卡在同一台电脑只能消费一次。
- 续期必须签发新卡，旧卡在同一台电脑永久不可复用。
- 支持主状态、ProgramData 影子副本和 HKCU 注册表镜像，并拦截时钟回拨和常见删档重置。
- 纯离线无法提前停用已发出的有效卡；重装整机、全新机器和逆向破解不在 L1 防护范围内。

完整设计、操作手册和边界说明见 [登录与授权设计](docs/登录与授权设计.md)。

## 技术栈

- 运行平台：.NET 9 / `net9.0-windows`
- 桌面框架：WPF
- 架构模式：MVVM
- 主要依赖：`CommunityToolkit.Mvvm`、`QuestPDF`

## 目录结构

```text
SURS/
├── SURS.App/                    # WPF 应用（UI、登录、授权与 PDF）
│   ├── Services/License/        # 授权协议、机器指纹、授权服务、内置公钥
│   ├── ViewModels/              # 主流程与登录 ViewModel
│   └── LoginWindow.xaml         # 首次激活/登录/续期页面
├── SURS.Core/                   # 核心抽象与领域定义
├── SURS.Infrastructure/         # 基础设施实现
├── tools/
│   ├── SURS.LicenseTool/        # 离线发卡、批量交付、JSONL 台账、验签
│   └── SURS.LicenseSelfTest/    # 无 UI 的协议/生命周期/防重置自检
├── docs/                        # 设计与操作文档
└── SURS.sln                     # 解决方案（App、发卡工具、自检）
```

## 环境要求

- Windows 10/11
- .NET 9 SDK
- 首次恢复 WPF 依赖需要可访问 NuGet；依赖恢复后可完全离线构建运行

## 甲方发卡快速开始

### 1) 生成密钥对

```powershell
dotnet run --project tools/SURS.LicenseTool -- init
```

默认私钥和公钥位于 `%LOCALAPPDATA%\SURS.LicenseTool\.keys\`。生成后把公钥替换到 `SURS.App/Services/License/EmbeddedLicenseKey.cs` 并重新发布。正式交付不得使用仓库中的演示密钥。

### 2) 接一家、发一家

```powershell
dotnet run --project tools/SURS.LicenseTool -- issue `
  --note "某某医院" `
  --days 7 `
  --out ".\交付\某某医院-交付单.txt"
```

未指定 `--account` 时，工具按台账自动分配 `hos01`、`hos02`……；未指定密码时自动生成 16 位强随机密码。`--account` 和 `--password` 可用于覆盖。

每次签发自动追加台账，默认路径：

```text
%LOCALAPPDATA%\SURS.LicenseTool\ledger.jsonl
```

可用 `--ledger` 指定统一台账文件。台账是 append-only JSONL，包含机构、账号、天数、签发时间、卡指纹和明文密码，必须按敏感文件保管。

### 3) 查询台账

```powershell
dotnet run --project tools/SURS.LicenseTool -- ledger list
dotnet run --project tools/SURS.LicenseTool -- list --account hos01
dotnet run --project tools/SURS.LicenseTool -- list --note "某某医院" --full
```

`list` 可直接使用，`ledger list` 等价。`--full` 显示卡指纹，不显示密码以外的额外秘密。

### 4) 验签与排查

```powershell
dotnet run --project tools/SURS.LicenseTool -- verify "<完整激活卡号>"
dotnet run --project tools/SURS.LicenseTool -- inspect "<完整激活卡号>"
```

`verify` 同时验签和解析；`inspect` 只解析内容并明确显示“签名未校验”。

### 5) 60 家批量交付（可选）

```powershell
dotnet run --project tools/SURS.LicenseTool -- batch `
  --hospitals "医院名单.txt" `
  --days 7 `
  --out ".\医院交付包" `
  --app-exe ".\Publish\SURS.App.exe" `
  --ledger ".\台账\60家台账.jsonl"
```

清单每行格式为 `机构名,账号,密码`，后两项可省略。批量工具为每家生成一个交付文件夹和交付单，并写入 `交付清单.csv` 与统一 JSONL 台账。详见 [60 家医院交付指引](docs/60家交付指引.md)。

## 医院首次使用与续期

医院收到交付单后：

1. 启动 `SURS.App.exe`，输入交付单上的激活卡号、账号和密码。
2. 首次激活成功即开始 168 小时计时；之后每次启动输入账号和密码。
3. 到期后软件停用，联系甲方领取**新卡**，在续期页输入新卡号、账号和密码。

纯离线方案不能在发卡后远程提前停用一张有效卡。若需要提前停用、集中审计或限制院内使用，应升级为医院内网授权点。

## 自检

```powershell
dotnet run --project tools/SURS.LicenseSelfTest -c Debug
```

自检使用临时目录、假时钟和假机器指纹，不写真实授权路径或注册表。覆盖协议、密码预校验、首次使用、时钟回拨、过期、单卡单机一次性、影子恢复、旧代回放、跨机复制、续期和 60 家台账规模。

## 构建与发布

### Debug 构建

```powershell
dotnet build SURS.sln -c Debug
```

### Windows x64 单文件发布

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

发布后主程序为 `./Publish/SURS.App.exe`。

## 安全提醒

- `private_key.pem` 是授权系统的最高机密，严禁入库、发布或发送给医院。
- 交付单和台账包含明文密码，只能由授权管理员保管。
- 正式交付前必须重新生成正式密钥并替换内置公钥，否则使用演示密钥签发的卡不具备正式隔离性。
- 当前交付的正式密钥已生成并内置（发卡机 %LOCALAPPDATA%\SURS.LicenseTool\.keys；私钥备份在 `乙方机密\.keys-正式密钥-备份\`）。每 7 天续期前请核对两处 private_key.pem 一致；更换密钥 = 全量重发 60 家并重新发布。
- 纯离线授权无法防止重装整机、更换机器、伪造运行环境或精心逆向；应如实向甲方说明。
- 如果全部本地状态、卡文件和注册表镜像都被清除，程序会把电脑视为新机器，这是 L1 方案的残余风险。

## 文档索引

- [登录与授权设计（含甲方/医院操作手册）](docs/登录与授权设计.md)
- [60 家医院交付指引](docs/60家交付指引.md)
- [医院使用手册](docs/医院使用手册.md)
- 架构设计：`docs/架构设计文档.md`
- 架构实施：`docs/架构实施指南.md`
- 模块拆分：`docs/模块拆分详细方案.md`
- O-RADS 实现：`docs/O-RADS实现说明.md`
- 实施检查：`docs/实施检查清单.md`

## 许可

本项目采用 MIT License。