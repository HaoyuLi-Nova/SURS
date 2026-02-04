# O-RADS 自动分级实现方案

## 一、需求分析

根据表单要求，需要实现基于输入内容自动生成O-RADS分级（0-5级），并提供分级理由说明，帮助医生进行诊断。

## 二、实现架构

### 1. 核心组件

#### 1.1 ORadsCalculator 服务类
- **位置**: `SURS.App/Services/ORadsCalculator.cs`
- **职责**: 实现O-RADS分级计算逻辑
- **方法**:
  - `CalculateORadsLevel(SursReport report)`: 计算整个报告的O-RADS分级
  - `CalculateRegionORads(AdnexaRegion region)`: 计算单个区域的O-RADS分级
  - `GetORadsReason(int level, AdnexaRegion region)`: 获取分级理由说明

#### 1.2 ORadsResult 结果类
- **位置**: `SURS.App/Models/ORadsResult.cs`
- **属性**:
  - `int Level`: O-RADS分级 (0-5)
  - `string Reason`: 分级理由说明
  - `string Suggestion`: 建议提示（如典型良性病变类型）

### 2. 分级规则实现

#### 2.1 分级优先级（从高到低）
1. **O-RADS 5** (最高风险) - 优先判断
2. **O-RADS 4**
3. **O-RADS 3**
4. **O-RADS 2**
5. **O-RADS 1**
6. **O-RADS 0** (评价不完全)

#### 2.2 关键判断条件

**O-RADS 5 判断条件：**
- 单房囊肿：≥4个乳头状突起
- 有实性成分的多房囊肿：血流评分3-4分
- 实性肿物：光滑，血流评分4分
- 实性肿物：不规整，任意血流评分
- 腹水积液（合并积液）

**O-RADS 4 判断条件：**
- 多房囊肿（无实性成分）：
  - ≥10cm，内壁光滑，血流评分1-3分
  - 任意大小，内壁光滑，血流评分4分
  - 任意大小，内壁不规整或分隔不规则
- 有实性成分的单房囊肿：0-3个乳头样突起
- 有实性成分的多房囊肿：血流评分1-2分
- 实性肿物：光滑，血流评分2-3分

**O-RADS 3 判断条件：**
- 单房囊肿≥10cm（单纯或非单纯）
- 多房囊肿<10cm，光滑内壁，血流评分1-3分
- 实性肿物：光滑，血流评分1分
- 有厚度<3mm的不规则内壁的单房囊肿（任意大小）

**O-RADS 2 判断条件：**
- 单纯性囊肿：≤3cm, >3≤5cm, >5<10cm
- 非单纯性单房囊肿：内壁光滑，≤3cm 或 >3<10cm

**O-RADS 1 判断条件：**
- 正常卵巢，≤3cm的单纯性囊肿
- 黄体囊肿≤3cm

**O-RADS 0 判断条件：**
- 数据不完整，无法评价

### 3. 辅助方法

#### 3.1 工具方法
- `GetMaxDiameter(double length, double width, double height)`: 计算最大直径
- `IsSmoothWall(UnilocularCyst cyst)`: 判断内壁是否光滑
- `IsSmoothWall(MultilocularCyst cyst)`: 判断多房囊肿内壁是否光滑
- `IsSmoothSeptum(MultilocularCyst cyst)`: 判断分隔是否光滑
- `GetPapillaryCount(SolidCyst cyst)`: 获取乳头数量（转换为数字）
- `HasFluid(SursReport report)`: 判断是否有积液

## 三、实现步骤

### 步骤1: 创建ORadsResult类
```csharp
public class ORadsResult
{
    public int Level { get; set; }
    public string Reason { get; set; }
    public string Suggestion { get; set; }
    public bool IsTypicalBenign { get; set; } // 是否为典型良性病变
}
```

### 步骤2: 创建ORadsCalculator服务类
- 实现分级逻辑
- 按优先级从高到低判断
- 返回分级结果和理由

### 步骤3: 在SursReport模型中集成
- 添加 `ORadsResult AutoORadsResult` 属性
- 添加 `CalculateAutoORads()` 方法
- 在相关属性变化时触发自动计算

### 步骤4: 在ViewModel中监听数据变化
- 监听AdnexaRegions的变化
- 监听各异常类型属性的变化
- 自动调用计算并更新UI

### 步骤5: 更新UI显示
- 显示自动计算的O-RADS分级
- 显示分级理由说明
- 提供手动覆盖选项
- 显示建议提示（如典型良性病变）

## 四、关键实现细节

### 4.1 最大直径计算
```csharp
private double GetMaxDiameter(double length, double width, double height)
{
    return Math.Max(Math.Max(length, width), height);
}
```

### 4.2 乳头数量解析
```csharp
private int ParsePapillaryCount(string count)
{
    return count switch
    {
        "1" => 1,
        "2" => 2,
        "3" => 3,
        "＞3" => 4, // 视为≥4
        _ => 0
    };
}
```

### 4.3 分级判断流程
```csharp
public ORadsResult CalculateRegionORads(AdnexaRegion region)
{
    // 1. 检查是否正常
    if (region.IsNormal)
    {
        // 判断O-RADS 1的条件
        return CheckORads1(region);
    }
    
    // 2. 按优先级从高到低判断
    var result = CheckORads5(region);
    if (result != null) return result;
    
    result = CheckORads4(region);
    if (result != null) return result;
    
    result = CheckORads3(region);
    if (result != null) return result;
    
    result = CheckORads2(region);
    if (result != null) return result;
    
    // 3. 默认返回O-RADS 0
    return new ORadsResult { Level = 0, Reason = "评价不完全" };
}
```

### 4.4 多区域处理
- 如果多个区域都有异常，取最高风险等级
- 如果所有区域都正常，返回O-RADS 1

## 五、UI集成方案

### 5.1 显示位置
在"结论与备注区"的O-RADS分级部分

### 5.2 UI元素
1. **自动分级显示**
   - 显示自动计算的O-RADS分级（带颜色标识）
   - 显示分级理由说明

2. **手动选择**
   - 保留原有的RadioButton选择
   - 标记为"自动建议"或"手动选择"

3. **建议提示**
   - 如果是O-RADS 2或3，显示典型良性病变选择提示
   - 显示相关备注说明

### 5.3 视觉设计
- 自动分级用不同颜色标识风险等级
- O-RADS 5: 红色（高风险）
- O-RADS 4: 橙色（中高风险）
- O-RADS 3: 黄色（低风险）
- O-RADS 2: 浅绿色（几乎良性）
- O-RADS 1: 绿色（正常）
- O-RADS 0: 灰色（不适用）

## 六、测试用例

### 测试用例1: 单纯性囊肿
- 输入: 单房囊肿，单纯性，5cm
- 预期: O-RADS 2

### 测试用例2: 大单房囊肿
- 输入: 单房囊肿，12cm
- 预期: O-RADS 3

### 测试用例3: 多乳头单房囊肿
- 输入: 有实性成分的单房囊肿，4个乳头
- 预期: O-RADS 5

### 测试用例4: 多房囊肿
- 输入: 多房囊肿，8cm，光滑内壁，血流评分2
- 预期: O-RADS 3

### 测试用例5: 实性肿物
- 输入: 实性肿物，不规整，血流评分3
- 预期: O-RADS 5

## 七、注意事项

1. **数据完整性**: 如果关键数据缺失，返回O-RADS 0
2. **优先级**: 严格按照从高到低的优先级判断
3. **典型良性病变**: O-RADS 2和3中的典型良性病变需要医生手动选择，系统只提示
4. **积液判断**: 有积液时直接判断为O-RADS 5
5. **多区域**: 多个区域异常时，取最高风险等级

## 八、后续优化

1. 添加分级置信度显示
2. 添加历史分级对比
3. 添加分级规则说明文档链接
4. 支持分级规则的自定义配置

