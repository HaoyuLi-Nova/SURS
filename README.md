# SURS - 超声结构化报告系统 (Structured Ultrasound Reporting System)

SURS 是一个基于 WPF 开发的现代化医疗超声报告生成系统。它专为医生设计，用于快速录入卵巢超声检查数据，并基于 O-RADS 标准生成专业的 PDF 报告。

## ✨ 核心功能

*   **结构化数据录入**: 提供直观的卡片式界面，支持卵巢、囊肿、肿物等详细参数的录入。
*   **智能交互**: 根据选择的病灶类型（如单房/多房囊肿），动态显示/隐藏相关填写区域。
*   **O-RADS 分级**: 集成 O-RADS 分级标准，辅助医生进行风险评估。
*   **PDF 报告导出**: 一键生成排版精美、符合医疗规范的 A4 PDF 报告。
*   **现代化 UI**: 采用医疗蓝为主色调的现代化设计，减轻视觉疲劳，提升操作体验。

## 🛠 技术栈

*   **开发框架**: .NET 9.0
*   **UI 框架**: WPF (Windows Presentation Foundation)
*   **MVVM 库**: CommunityToolkit.Mvvm
*   **PDF 引擎**: QuestPDF
*   **IDE**: Visual Studio / VS Code (Trae IDE)

## 🚀 快速开始

### 环境要求

*   Windows 10/11
*   .NET 9.0 SDK

### 运行步骤

1.  克隆仓库：
    ```bash
    git clone https://github.com/yourusername/SURS.git
    cd SURS
    ```

2.  还原依赖并运行：
    ```bash
    cd SURS.App
    dotnet restore
    dotnet run
    ```
3.  导出exe文件：
   ```bash
   
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o d:\Myproject\SURS\Publish

    ```
   

## 📂 项目结构

```
SURS/
├── SURS.App/           # WPF 应用程序主项目
│   ├── Models/         # 数据模型 (Ovary, Lesion, SursReport)
│   ├── ViewModels/     # 视图模型 (MainViewModel)
│   ├── Services/       # 服务层 (PdfService - QuestPDF 实现)
│   ├── Views/          # XAML 视图文件
│   └── App.xaml        # 应用程序入口与资源定义
└── SURS.sln            # 解决方案文件
```

## 📝 许可证

本项目采用 MIT 许可证。



## 导出流程
cd d:\Myproject\SURS; dotnet publish SURS.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o Publish

cd d:\Myproject\SURS\Publish; Get-ChildItem -File | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize

位置：d:\Myproject\SURS\Publish\SURS.App.exe