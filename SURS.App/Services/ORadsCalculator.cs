using System;
using System.Linq;
using SURS.App.Models;

namespace SURS.App.Services
{
    /// <summary>
    /// O-RADS自动分级计算服务
    /// 严格按照 docs/分级设置.md 中的规则实现
    /// </summary>
    public class ORadsCalculator
    {
        /// <summary>
        /// 计算整个报告的O-RADS分级
        /// </summary>
        public ORadsResult CalculateORadsLevel(SursReport report)
        {
            if (report == null || !report.IncludeOvary)
            {
                return new ORadsResult
                {
                    Level = 0,
                    Reason = "未包含卵巢附件部分，无法评价（O-RADS 0：评价不完全）"
                };
            }

            // O-RADS 5: 检查是否有积液（直接判断为O-RADS 5）
            if (report.HasFluid)
            {
                return new ORadsResult
                {
                    Level = 5,
                    Reason = "存在腹水积液，有或无腹膜结节，恶性高风险（≥50%恶性风险）"
                };
            }

            // 只处理已选择评价的区域
            var evaluatedRegions = report.AdnexaRegions.Where(r => r.HasEvaluation).ToList();
            
            if (!evaluatedRegions.Any())
            {
                return new ORadsResult
                {
                    Level = 0,
                    Reason = "未选择任何区域评价，无法进行O-RADS分级（O-RADS 0：评价不完全）"
                };
            }

            // 如果所有已评价区域都正常，返回O-RADS 1
            if (evaluatedRegions.All(r => r.IsNormal))
            {
                return new ORadsResult
                {
                    Level = 1,
                    Reason = "绝经前正常卵巢，所有已评价区域未见明显异常"
                };
            }

            // 计算所有异常区域的O-RADS分级，取最高风险等级
            int maxLevel = 0;
            ORadsResult? maxResult = null;

            foreach (var region in evaluatedRegions.Where(r => r.IsAbnormal))
            {
                var result = CalculateRegionORads(region);
                if (result.Level > maxLevel)
                {
                    maxLevel = result.Level;
                    maxResult = result;
                }
            }

            return maxResult ?? new ORadsResult
            {
                Level = 0,
                Reason = "评价不完全，数据不完整（O-RADS 0：评价不完全）"
            };
        }

        /// <summary>
        /// 计算单个区域的O-RADS分级
        /// 严格按照文档规则，从高到低判断（5 -> 4 -> 3 -> 2 -> 1）
        /// </summary>
        public ORadsResult CalculateRegionORads(AdnexaRegion region)
        {
            if (region == null)
            {
                return new ORadsResult
                {
                    Level = 0,
                    Reason = "评价不完全，数据不完整"
                };
            }

            // 正常区域：检查是否符合O-RADS 1
            if (region.IsNormal)
            {
                return CheckORads1(region);
            }

            // 异常区域：按优先级从高到低判断
            var result = CheckORads5(region);
            if (result != null) return result;

            result = CheckORads4(region);
            if (result != null) return result;

            result = CheckORads3(region);
            if (result != null) return result;

            result = CheckORads2(region);
            if (result != null) return result;

            // 默认返回O-RADS 0
            return new ORadsResult
            {
                Level = 0,
                Reason = "评价不完全，数据不完整（O-RADS 0：评价不完全）"
            };
        }

        #region O-RADS 5: 恶性高风险（≥50%恶性风险）

        private ORadsResult? CheckORads5(AdnexaRegion region)
        {
            // 1. 单房囊肿：任意大小，有 ≥4 个乳头状突起，任意血流评分
            if (region.HasSolidCyst && region.SolidCyst.HasPapillary)
            {
                int papillaryCount = ParsePapillaryCount(region.SolidCyst.PapillaryCount);
                if (papillaryCount >= 4)
                {
                    string countText = GetPapillaryCountText(region.SolidCyst.PapillaryCount, papillaryCount);
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = $"单房囊肿，任意大小，{countText}，任意血流评分，恶性高风险（≥50%恶性风险）"
                    };
                }
            }

            // 2. 有实性成分的多房囊肿：任意大小，彩色血流评分 3~4 分
            if (region.HasMultilocularCyst && region.HasSolidCyst)
            {
                int flowScore = region.SolidCyst.BloodFlowScore;
                if (flowScore >= 3 && flowScore <= 4)
                {
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = $"有实性成分的多房囊肿，任意大小，彩色血流评分{flowScore}分，恶性高风险（≥50%恶性风险）"
                    };
                }
            }

            // 3. 实性肿物：
            if (region.HasSolidMass)
            {
                // 3a. 光滑，任意大小，彩色血流评分 4 分
                if (region.SolidMass.Boundary == "规则" && region.SolidMass.BloodFlowScore == 4)
                {
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = "实性肿物，光滑，任意大小，彩色血流评分4分，恶性高风险（≥50%恶性风险）"
                    };
                }

                // 3b. 不规整，任意大小，任意血流评分
                if (region.SolidMass.Boundary == "不规则")
                {
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = "实性肿物，不规整，任意大小，任意血流评分，恶性高风险（≥50%恶性风险）"
                    };
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 4: 恶性中等风险病变（10%~50%恶性风险）

        private ORadsResult? CheckORads4(AdnexaRegion region)
        {
            // 1. 多房囊肿，没有实性成分
            if (region.HasMultilocularCyst && !region.HasSolidCyst)
            {
                double maxDiameter = GetMaxDiameter(
                    region.MultilocularCyst.Length,
                    region.MultilocularCyst.Width,
                    region.MultilocularCyst.Height);

                bool isSmoothWall = region.MultilocularCyst.EchoSmoothWall;
                bool isRoughWall = region.MultilocularCyst.EchoRoughWall;
                bool isRoughSeptum = region.MultilocularCyst.EchoRoughSeptum;
                int flowScore = region.MultilocularCyst.BloodFlowScore;

                // 1a. ≥10cm，内壁光滑，彩色血流评分 1~3 分
                if (maxDiameter >= 10 && isSmoothWall && flowScore >= 1 && flowScore <= 3)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"多房囊肿（无实性成分），≥10cm，内壁光滑，彩色血流评分{flowScore}分，恶性中等风险（10%~50%恶性风险）"
                    };
                }

                // 1b. 任意大小，内壁光滑，彩色血流评分 4 分
                if (isSmoothWall && flowScore == 4)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"多房囊肿（无实性成分），任意大小，内壁光滑，彩色血流评分4分，恶性中等风险（10%~50%恶性风险）"
                    };
                }

                // 1c. 任意大小，内壁不规整和（或）分隔不规则，任意彩色血流评分
                if (isRoughWall || isRoughSeptum)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = "多房囊肿（无实性成分），任意大小，内壁不规整和（或）分隔不规则，任意彩色血流评分，恶性中等风险（10%~50%恶性风险）"
                    };
                }
            }

            // 2. 有实性成分的单房囊肿：任意大小，0~3 个乳头样突起，任意彩色血流评分
            if (region.HasSolidCyst && (region.SolidCyst.HasPapillary || region.SolidCyst.HasNoPapillary))
            {
                int papillaryCount = region.SolidCyst.HasNoPapillary
                    ? 0
                    : ParsePapillaryCount(region.SolidCyst.PapillaryCount);
                if (papillaryCount >= 0 && papillaryCount <= 3)
                {
                    string countText = GetPapillaryCountText(region.SolidCyst.PapillaryCount, papillaryCount);
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"有实性成分的单房囊肿，任意大小，{countText}，任意彩色血流评分，恶性中等风险（10%~50%恶性风险）"
                    };
                }
            }

            // 3. 有实性成分的多房囊肿：任意大小，彩色血流评分 1~2 分
            if (region.HasMultilocularCyst && region.HasSolidCyst)
            {
                int flowScore = region.SolidCyst.BloodFlowScore;
                if (flowScore >= 1 && flowScore <= 2)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"有实性成分的多房囊肿，任意大小，彩色血流评分{flowScore}分，恶性中等风险（10%~50%恶性风险）"
                    };
                }
            }

            // 4. 实性肿物：光滑，任意大小，彩色血流评分 2~3 分
            if (region.HasSolidMass)
            {
                if (region.SolidMass.Boundary == "规则")
                {
                    int flowScore = region.SolidMass.BloodFlowScore;
                    if (flowScore >= 2 && flowScore <= 3)
                    {
                        return new ORadsResult
                        {
                            Level = 4,
                            Reason = $"实性肿物，光滑，任意大小，彩色血流评分{flowScore}分，恶性中等风险（10%~50%恶性风险）"
                        };
                    }
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 3: 恶性低风险病变（1%~10%恶性风险）

        private ORadsResult? CheckORads3(AdnexaRegion region)
        {
            // 1. 典型良性病变（>10cm）：表3由医生选择，不自动分级
            // 3. 典型病变 ≥10cm：仅对特定病变自动分级（黄体囊肿、卵巢子宫内膜异位囊肿、卵巢出血性囊肿）
            var typicalBenignResult = CheckTypicalBenignLesion(region, minSize: 10, maxSize: double.MaxValue);
            if (IsAutoEligibleTypicalBenignOver10(typicalBenignResult))
            {
                return new ORadsResult
                {
                    Level = 3,
                    Reason = $"典型病变≥10cm：{typicalBenignResult}，恶性低风险（1%~10%恶性风险）"
                };
            }

            // 2. 单房囊肿 ≥10cm（单纯或非单纯性囊肿）
            if (region.HasUnilocularCyst)
            {
                double maxDiameter = GetMaxDiameter(
                    region.UnilocularCyst.Length,
                    region.UnilocularCyst.Width,
                    region.UnilocularCyst.Height);

                if (maxDiameter >= 10)
                {
                    string cystType = region.UnilocularCyst.IsSimpleCyst ? "单纯性" : "非单纯性";
                    return new ORadsResult
                    {
                        Level = 3,
                        Reason = $"{cystType}单房囊肿≥10cm，恶性低风险（1%~10%恶性风险）",
                        Suggestion = "如为典型良性病变（>10cm的典型卵巢出血性囊肿、典型卵巢成熟性畸胎瘤、典型卵巢子宫内膜异位囊肿），请在备注中注明。"
                    };
                }

                // 3. 不规则内壁单房囊肿：有厚度 ＜3mm 的不规则内壁，任意大小
                // 注意：这里假设 EchoRoughWall 表示有不规则内壁
                // 实际判断厚度<3mm需要更详细的测量数据，这里用EchoRoughWall作为近似判断
                if (region.UnilocularCyst.EchoRoughWall)
                {
                    return new ORadsResult
                    {
                        Level = 3,
                        Reason = "不规则内壁单房囊肿，有厚度<3mm的不规则内壁，任意大小，恶性低风险（1%~10%恶性风险）"
                    };
                }
            }

            // 4. 多房囊肿：＜10cm，光滑内壁，彩色血流评分 1~3 分
            if (region.HasMultilocularCyst && !region.HasSolidCyst)
            {
                double maxDiameter = GetMaxDiameter(
                    region.MultilocularCyst.Length,
                    region.MultilocularCyst.Width,
                    region.MultilocularCyst.Height);

                if (maxDiameter < 10 && region.MultilocularCyst.EchoSmoothWall)
                {
                    int flowScore = region.MultilocularCyst.BloodFlowScore;
                    if (flowScore >= 1 && flowScore <= 3)
                    {
                        return new ORadsResult
                        {
                            Level = 3,
                            Reason = $"多房囊肿<10cm，光滑内壁，彩色血流评分{flowScore}分，恶性低风险（1%~10%恶性风险）"
                        };
                    }
                }
            }

            // 5. 实性病变：光滑，任意大小，彩色血流评分 1 分
            if (region.HasSolidMass)
            {
                if (region.SolidMass.Boundary == "规则" && region.SolidMass.BloodFlowScore == 1)
                {
                    return new ORadsResult
                    {
                        Level = 3,
                        Reason = "实性肿物，光滑，任意大小，彩色血流评分1分，恶性低风险（1%~10%恶性风险）"
                    };
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 2: 几乎可以肯定的良性病变（＜1%恶性风险）

        private ORadsResult? CheckORads2(AdnexaRegion region)
        {
            // 1. 单纯性囊肿：≤3cm, ＞3cm≤5cm, ＞5cm＜10cm
            if (region.HasUnilocularCyst && region.UnilocularCyst.IsSimpleCyst)
            {
                double maxDiameter = GetMaxDiameter(
                    region.UnilocularCyst.Length,
                    region.UnilocularCyst.Width,
                    region.UnilocularCyst.Height);

                if (maxDiameter > 0 && maxDiameter < 10)
                {
                    string sizeDesc = maxDiameter <= 3 ? "≤3cm" : (maxDiameter <= 5 ? "＞3cm≤5cm" : "＞5cm＜10cm");
                    return new ORadsResult
                    {
                        Level = 2,
                        Reason = $"单纯性囊肿，{sizeDesc}，几乎可以肯定的良性病变（＜1%恶性风险）",
                        IsTypicalBenign = maxDiameter <= 10,
                        Suggestion = "如为典型良性病变（≤10cm的典型卵巢出血性囊肿、典型卵巢成熟性畸胎瘤、典型卵巢子宫内膜异位囊肿、单纯卵巢旁囊肿（任意大小）、典型的单纯性腹膜包涵囊肿（任意大小）、典型输卵管积水（任意大小）），请在备注中注明。"
                    };
                }
            }

            // 2. 典型良性病变（≤10cm）：见表2描述术语
            // 通过回声特征自动判断典型良性病变
            var typicalBenignResult = CheckTypicalBenignLesion(region, minSize: 0, maxSize: 10);
            if (typicalBenignResult != null)
            {
                return new ORadsResult
                {
                    Level = 2,
                    Reason = $"典型良性病变（≤10cm）：{typicalBenignResult}，几乎可以肯定的良性病变（＜1%恶性风险）",
                    IsTypicalBenign = true
                };
            }

            // 3. 囊内壁光滑的非单纯单房囊肿：≤3cm, ＞3cm＜10cm
            if (region.HasUnilocularCyst && !region.UnilocularCyst.IsSimpleCyst)
            {
                if (region.UnilocularCyst.EchoSmoothWall)
                {
                    double maxDiameter = GetMaxDiameter(
                        region.UnilocularCyst.Length,
                        region.UnilocularCyst.Width,
                        region.UnilocularCyst.Height);

                    if (maxDiameter > 0 && maxDiameter < 10)
                    {
                        string sizeDesc = maxDiameter <= 3 ? "≤3cm" : "＞3cm＜10cm";
                        return new ORadsResult
                        {
                            Level = 2,
                            Reason = $"囊内壁光滑的非单纯单房囊肿，{sizeDesc}，几乎可以肯定的良性病变（＜1%恶性风险）",
                            IsTypicalBenign = maxDiameter <= 10,
                            Suggestion = "如为典型良性病变（≤10cm的典型卵巢出血性囊肿、典型卵巢成熟性畸胎瘤、典型卵巢子宫内膜异位囊肿、单纯卵巢旁囊肿（任意大小）、典型的单纯性腹膜包涵囊肿（任意大小）、典型输卵管积水（任意大小）），请在备注中注明。"
                        };
                    }
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 1: 绝经前正常卵巢

        private ORadsResult CheckORads1(AdnexaRegion region)
        {
            if (region == null)
            {
                return new ORadsResult
                {
                    Level = 1,
                    Reason = "绝经前正常卵巢，未见明显异常"
                };
            }

            // 1. ≤3cm 的单纯性囊肿
            // 2. 黄体囊肿 ≤3cm
            // 注意：正常状态下，如果MaxCystDiameter <= 3，可能是单纯性囊肿或黄体囊肿
            if (region.MaxCystDiameter > 0 && region.MaxCystDiameter <= 3)
            {
                return new ORadsResult
                {
                    Level = 1,
                    Reason = $"绝经前正常卵巢，≤3cm的囊性回声（可能为单纯性囊肿或黄体囊肿）"
                };
            }

            return new ORadsResult
            {
                Level = 1,
                Reason = "绝经前正常卵巢，未见明显异常"
            };
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否为典型良性病变
        /// 根据超声回声特征自动判断
        /// </summary>
        /// <param name="region">附件区域</param>
        /// <param name="minSize">最小直径（cm）</param>
        /// <param name="maxSize">最大直径（cm）</param>
        /// <returns>典型良性病变类型描述，如果不是则返回null</returns>
        private string? CheckTypicalBenignLesion(AdnexaRegion region, double minSize, double maxSize)
        {
            if (region == null) return null;

            double maxDiameter = 0;
            UnilocularCyst? unilocularCyst = null;
            MultilocularCyst? multilocularCyst = null;
            SolidCyst? solidCyst = null;

            // 确定病变类型和大小
            if (region.HasUnilocularCyst)
            {
                unilocularCyst = region.UnilocularCyst;
                maxDiameter = GetMaxDiameter(unilocularCyst.Length, unilocularCyst.Width, unilocularCyst.Height);
            }
            else if (region.HasMultilocularCyst)
            {
                multilocularCyst = region.MultilocularCyst;
                maxDiameter = GetMaxDiameter(multilocularCyst.Length, multilocularCyst.Width, multilocularCyst.Height);
            }
            else if (region.HasSolidCyst)
            {
                solidCyst = region.SolidCyst;
                maxDiameter = GetMaxDiameter(solidCyst.Length, solidCyst.Width, solidCyst.Height);
            }
            else
            {
                return null;
            }

            // 检查大小范围
            if (maxDiameter <= 0 || maxDiameter < minSize || maxDiameter > maxSize)
            {
                return null;
            }

            // 1. 典型卵巢出血性囊肿
            // 特征：密集点状回声 + 网格样回声 或 絮状回声
            if (unilocularCyst != null)
            {
                if ((unilocularCyst.EchoDenseDots && unilocularCyst.EchoGrid) ||
                    (unilocularCyst.EchoDenseDots && unilocularCyst.EchoFlocculent))
                {
                    if (maxSize <= 10)
                        return "典型卵巢出血性囊肿（≤10cm）";
                    else
                        return "典型卵巢出血性囊肿（>10cm）";
                }
            }
            else if (solidCyst != null)
            {
                if ((solidCyst.EchoDenseDots && solidCyst.EchoGrid) ||
                    (solidCyst.EchoDenseDots && solidCyst.EchoFlocculent))
                {
                    if (maxSize <= 10)
                        return "典型卵巢出血性囊肿（≤10cm）";
                    else
                        return "典型卵巢出血性囊肿（>10cm）";
                }
            }

            // 2. 典型卵巢成熟性畸胎瘤
            // 特征：强回声团 + 短线样强回声 或 弱点状回声
            if (unilocularCyst != null)
            {
                if ((unilocularCyst.EchoStrongMass && unilocularCyst.EchoShortLines) ||
                    (unilocularCyst.EchoStrongMass && unilocularCyst.EchoWeakDots))
                {
                    if (maxSize <= 10)
                        return "典型卵巢成熟性畸胎瘤（≤10cm）";
                    else
                        return "典型卵巢成熟性畸胎瘤（>10cm）";
                }
            }
            else if (solidCyst != null)
            {
                if ((solidCyst.EchoStrongMass && solidCyst.EchoShortLines) ||
                    (solidCyst.EchoStrongMass && solidCyst.EchoWeakDots))
                {
                    if (maxSize <= 10)
                        return "典型卵巢成熟性畸胎瘤（≤10cm）";
                    else
                        return "典型卵巢成熟性畸胎瘤（>10cm）";
                }
            }

            // 3. 典型卵巢子宫内膜异位囊肿
            // 特征：密集点状回声 + 网格样回声 + 透声差
            if (unilocularCyst != null)
            {
                if (unilocularCyst.EchoDenseDots && unilocularCyst.EchoGrid)
                {
                    // 注意：UnilocularCyst 没有 EchoPoorTransmission，但可以通过其他特征判断
                    if (maxSize <= 10)
                        return "典型卵巢子宫内膜异位囊肿（≤10cm）";
                    else
                        return "典型卵巢子宫内膜异位囊肿（>10cm）";
                }
            }
            else if (solidCyst != null)
            {
                if (solidCyst.EchoDenseDots && solidCyst.EchoGrid && solidCyst.EchoPoorTransmission)
                {
                    if (maxSize <= 10)
                        return "典型卵巢子宫内膜异位囊肿（≤10cm）";
                    else
                        return "典型卵巢子宫内膜异位囊肿（>10cm）";
                }
            }

            // 4. 典型黄体囊肿（≥10cm时）
            // 特征：密集点状回声 + 网格样回声（类似出血性囊肿，但通常较小）
            if (maxSize > 10 && unilocularCyst != null)
            {
                if (unilocularCyst.EchoDenseDots && unilocularCyst.EchoGrid)
                {
                    return "典型黄体囊肿（≥10cm）";
                }
            }

            // 5. 单纯卵巢旁囊肿（任意大小）
            // 特征：位置在卵巢旁 + 单纯性囊肿
            if (unilocularCyst != null && unilocularCyst.IsSimpleCyst)
            {
                string location = unilocularCyst.Location?.ToLower() ?? "";
                if (location.Contains("旁") || location.Contains("paratubal") || 
                    location.Contains("附件") || location.Contains("adnexal"))
                {
                    return "单纯卵巢旁囊肿（任意大小）";
                }
            }

            // 6. 典型的单纯性腹膜包涵囊肿（任意大小）
            // 特征：位置在腹膜 + 单纯性囊肿
            if (unilocularCyst != null && unilocularCyst.IsSimpleCyst)
            {
                string location = unilocularCyst.Location?.ToLower() ?? "";
                if (location.Contains("腹膜") || location.Contains("peritoneal") ||
                    location.Contains("包涵") || location.Contains("inclusion"))
                {
                    return "典型的单纯性腹膜包涵囊肿（任意大小）";
                }
            }

            // 7. 典型输卵管积水（任意大小）
            // 特征：位置在输卵管 + 单纯性囊肿或非单纯性囊肿
            if (unilocularCyst != null)
            {
                string location = unilocularCyst.Location?.ToLower() ?? "";
                if (location.Contains("输卵管") || location.Contains("fallopian") ||
                    location.Contains("tube") || location.Contains("积水") ||
                    location.Contains("hydrosalpinx"))
                {
                    return "典型输卵管积水（任意大小）";
                }
            }

            return null;
        }

        /// <summary>
        /// 计算最大直径
        /// </summary>
        private double GetMaxDiameter(double length, double width, double height)
        {
            return Math.Max(Math.Max(length, width), height);
        }

        /// <summary>
        /// 解析乳头数量
        /// </summary>
        private int ParsePapillaryCount(string count)
        {
            if (string.IsNullOrWhiteSpace(count))
                return 0;

            count = count.Trim();
            
            // 先尝试直接解析为数字
            if (int.TryParse(count, out int numValue))
            {
                return numValue;
            }
            
            // 处理特殊格式
            return count switch
            {
                "1" => 1,
                "2" => 2,
                "3" => 3,
                ">3" => 4,  // 半角大于号
                "＞3" => 4,  // 全角大于号
                ">=4" => 4,
                "≥4" => 4,
                _ => 0
            };
        }

        /// <summary>
        /// 获取乳头数量的显示文本
        /// </summary>
        private string GetPapillaryCountText(string originalCount, int parsedCount)
        {
            if (string.IsNullOrWhiteSpace(originalCount))
            {
                return parsedCount == 0 ? "无乳头" : "有乳头（数量未选择）";
            }

            string trimmedCount = originalCount.Trim();
            if (trimmedCount.Contains(">") || trimmedCount.Contains("＞") || trimmedCount.Contains("≥"))
            {
                if (trimmedCount == ">3" || trimmedCount == "＞3")
                {
                    return "有>3个乳头状突起";
                }
                if (parsedCount >= 4)
                {
                    return "有≥4个乳头状突起";
                }
                return $"有{trimmedCount}个乳头状突起";
            }

            // 正常数字显示
            if (parsedCount > 0)
            {
                return $"有{parsedCount}个乳头样突起";
            }

            return "无乳头";
        }

        /// <summary>
        /// 仅允许自动分级的典型良性病变（≥10cm）
        /// </summary>
        private bool IsAutoEligibleTypicalBenignOver10(string? typicalBenignResult)
        {
            if (string.IsNullOrWhiteSpace(typicalBenignResult))
            {
                return false;
            }

            return typicalBenignResult.Contains("卵巢出血性囊肿", StringComparison.Ordinal) ||
                   typicalBenignResult.Contains("卵巢子宫内膜异位囊肿", StringComparison.Ordinal) ||
                   typicalBenignResult.Contains("黄体囊肿", StringComparison.Ordinal);
        }

        #endregion
    }
}
