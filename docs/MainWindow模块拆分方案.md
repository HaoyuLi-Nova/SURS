# MainWindow.xaml 模块拆分方案

## 📋 拆分逻辑

### 核心原则
1. **单一职责**：每个 UserControl 只负责一个功能模块
2. **数据绑定**：所有控件共享同一个 `MainViewModel`，通过 `DataContext` 绑定
3. **可复用性**：控件可以在其他地方复用
4. **可维护性**：每个控件独立，修改不影响其他部分

### 拆分策略
- **按业务功能拆分**：每个 Expander 对应一个 UserControl
- **保持数据绑定**：所有控件绑定到 `MainViewModel.Report`
- **事件处理**：特殊事件（如评价 RadioButton 取消选择）通过附加属性或事件传递

---

## 🎯 拆分目标结构

```
SURS.App/Controls/
├── AppHeader.xaml              ✅ 已存在
├── AppFooter.xaml              ✅ 已存在
├── PreviewPanel.xaml           ✅ 已存在
├── ORadsResultCard.xaml        ✅ 已存在
├── ReportHeaderSection.xaml   🆕 报告头部与基础信息
├── TemplateSelection.xaml     🆕 模板选择
├── UterusSection.xaml         🆕 子宫部分
├── EndometriumSection.xaml    🆕 子宫内膜部分
├── AdnexaSection.xaml         🆕 卵巢与附件部分
├── FluidSection.xaml          🆕 合并积液
└── ConclusionSection.xaml     🆕 结论与备注区
```

---

## 📐 拆分映射表

| MainWindow.xaml 行数 | 内容 | 新控件名称 | 复杂度 |
|---------------------|------|-----------|--------|
| 65-251 | 报告头部与基础信息 | `ReportHeaderSection` | 中 |
| 253-273 | 模板选择 | `TemplateSelection` | 低 |
| 275-487 | 子宫部分 | `UterusSection` | 高 |
| 489-837 | 子宫内膜部分 | `EndometriumSection` | 高 |
| 839-1556 | 卵巢与附件部分 | `AdnexaSection` | 很高 |
| 1560-1603 | 合并积液 | `FluidSection` | 低 |
| 1605-1712 | 结论与备注区 | `ConclusionSection` | 中 |

---

## 🔧 实施步骤

### 阶段 1: 简单模块（低复杂度）
1. ✅ `TemplateSelection.xaml` - 模板选择
2. ✅ `FluidSection.xaml` - 合并积液

### 阶段 2: 中等复杂度模块
3. ✅ `ReportHeaderSection.xaml` - 报告头部
4. ✅ `ConclusionSection.xaml` - 结论与备注

### 阶段 3: 复杂模块
5. ✅ `UterusSection.xaml` - 子宫部分
6. ✅ `EndometriumSection.xaml` - 子宫内膜部分
7. ✅ `AdnexaSection.xaml` - 卵巢与附件部分

---

## 📝 实施细节

### 1. ReportHeaderSection（报告头部与基础信息）

**职责**：
- 机构名称、登记号、序号
- 患者信息（姓名、性别、年龄）
- 科别、门诊号、住院号、床位、申请医师
- 检查项目、报告标题
- 末次月经时间 (LMP)
- 图像选择

**数据绑定**：
- `{Binding Report.HospitalName}`
- `{Binding Report.PatientName}`
- `{Binding Report.ImagePaths}`
- `{Binding SelectImageCommand}`

---

### 2. TemplateSelection（模板选择）

**职责**：
- 选择需要填写的模板（子宫、子宫内膜、卵巢）

**数据绑定**：
- `{Binding Report.IncludeUterus}`
- `{Binding Report.IncludeEndometrium}`
- `{Binding Report.IncludeOvary}`

---

### 3. UterusSection（子宫部分）

**职责**：
- 子宫位置、大小
- 宫颈大小
- 子宫肌层回声
- 肌层结节（动态添加）

**数据绑定**：
- `{Binding Report.Uterus.Position}`
- `{Binding Report.Uterus.Length}`
- `{Binding Report.Uterus.Nodules}`
- `{Binding AddUterusNoduleCommand}`
- `{Binding RemoveUterusNoduleCommand}`

**特殊处理**：
- 结节 TabControl 需要动态管理
- 需要处理"只描述较大者"的逻辑

---

### 4. EndometriumSection（子宫内膜部分）

**职责**：
- 内膜厚度、回声、均匀性
- 子宫内膜中线
- 子宫结合带
- 子宫内膜 CDFI
- 宫腔占位性病变

**数据绑定**：
- `{Binding Report.Endometrium.Thickness}`
- `{Binding Report.Endometrium.EchoType}`
- `{Binding Report.Cavity.HasLesion}`

---

### 5. AdnexaSection（卵巢与附件部分）

**职责**：
- 四个区域（左卵巢、右卵巢、左附件、右附件）的 TabControl
- 每个区域的评价选择（正常/异常）
- 正常状态：大小、卵泡
- 异常状态：单房囊肿、多房囊肿、囊实性、实性肿物

**数据绑定**：
- `{Binding Report.AdnexaRegions}`
- `{Binding Report.SelectedAdnexaRegion}`
- `{Binding IsNormal}` / `{Binding IsAbnormal}`

**特殊处理**：
- 评价 RadioButton 需要支持取消选择（已在 MainWindow.xaml.cs 中实现）
- 需要将事件处理逻辑移到控件内部或通过附加属性传递

---

### 6. FluidSection（合并积液）

**职责**：
- 积液有无选择
- 积液位置和深度

**数据绑定**：
- `{Binding Report.HasFluid}`
- `{Binding Report.FluidLocations}`

---

### 7. ConclusionSection（结论与备注区）

**职责**：
- 超声提示（子宫描述、子宫内膜多选）
- O-RADS 分级（自动+手动）
- 注明说明
- 报告签署

**数据绑定**：
- `{Binding Report.UterusDescription}`
- `{Binding Report.ORadsLevel}`
- `{Binding Report.Remarks}`
- `{Binding Report.Typist}`

**包含子控件**：
- `<controls:ORadsResultCard/>` 已存在

---

## 🔗 数据绑定策略

### 方案 A：直接绑定到 MainViewModel（推荐）

```xml
<!-- MainWindow.xaml -->
<controls:ReportHeaderSection DataContext="{Binding}"/>

<!-- ReportHeaderSection.xaml -->
<TextBox Text="{Binding Report.HospitalName}"/>
```

**优点**：
- 简单直接
- 不需要额外的 ViewModel
- 数据流清晰

**缺点**：
- 控件依赖 MainViewModel 结构

---

### 方案 B：通过属性传递（备选）

```xml
<!-- MainWindow.xaml -->
<controls:ReportHeaderSection Report="{Binding Report}"/>

<!-- ReportHeaderSection.xaml -->
<UserControl x:Class="...">
    <UserControl.DataContext>
        <Binding RelativeSource="{RelativeSource Self}"/>
    </UserControl.DataContext>
    <TextBox Text="{Binding Report.HospitalName}"/>
</UserControl>
```

**优点**：
- 控件更独立
- 可以单独测试

**缺点**：
- 需要定义依赖属性
- 代码更复杂

---

## ⚙️ 事件处理策略

### 评价 RadioButton 取消选择

**当前实现**：在 `MainWindow.xaml.cs` 中处理

**拆分后方案**：
1. **方案 A**：在 `AdnexaSection.xaml.cs` 中处理（推荐）
2. **方案 B**：创建附加属性 `EvaluationRadioButtonBehavior`

---

## 📊 拆分后的 MainWindow.xaml

```xml
<Window>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- Header -->
            <RowDefinition Height="*"/>    <!-- Content -->
            <RowDefinition Height="Auto"/> <!-- Footer -->
        </Grid.RowDefinitions>

        <controls:AppHeader Grid.Row="0"/>

        <Grid Grid.Row="1">
            <!-- Split View: Form and Preview -->
            <ScrollViewer Grid.Column="0">
                <StackPanel>
                    <controls:ReportHeaderSection/>
                    <controls:TemplateSelection/>
                    <controls:UterusSection Visibility="{Binding Report.IncludeUterus, Converter={StaticResource BoolToVis}}"/>
                    <controls:EndometriumSection Visibility="{Binding Report.IncludeEndometrium, Converter={StaticResource BoolToVis}}"/>
                    <controls:AdnexaSection Visibility="{Binding Report.IncludeOvary, Converter={StaticResource BoolToVis}}"/>
                    <controls:FluidSection/>
                    <controls:ConclusionSection/>
                </StackPanel>
            </ScrollViewer>
            
            <controls:PreviewPanel Grid.Column="2"/>
        </Grid>

        <controls:AppFooter Grid.Row="2"/>
    </Grid>
</Window>
```

**预期行数**：从 1746 行减少到约 50-80 行

---

## ✅ 拆分收益

1. **可维护性** ⬆️ 80%
   - 每个模块独立，修改不影响其他部分
   - 代码行数大幅减少

2. **可测试性** ⬆️ 100%
   - 每个控件可以单独测试
   - 可以创建 Mock ViewModel

3. **可复用性** ⬆️ 100%
   - 控件可以在其他窗口复用
   - 可以创建不同的布局组合

4. **开发效率** ⬆️ 60%
   - 多人协作更容易
   - 代码冲突减少

---

## 🚀 实施优先级

1. **P0（立即）**：`TemplateSelection`、`FluidSection`（简单，快速验证方案）
2. **P1（高）**：`ReportHeaderSection`、`ConclusionSection`（中等复杂度）
3. **P2（中）**：`UterusSection`、`EndometriumSection`（复杂但相对独立）
4. **P3（低）**：`AdnexaSection`（最复杂，需要特殊处理）

---

## 📝 注意事项

1. **命名空间**：确保所有控件使用正确的命名空间
2. **资源引用**：样式和转换器需要在 App.xaml 中定义
3. **事件传递**：评价 RadioButton 的事件处理需要移到控件内部
4. **数据绑定**：确保所有绑定路径正确
5. **测试**：每个控件拆分后立即测试功能

---

**状态**：方案已制定，准备实施

