using System;
using System.Linq;
using SURS.App.Models;

namespace SURS.App.Services
{
    /// <summary>
    /// O-RADS自动分级计算服务
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
                    Reason = "未包含卵巢附件部分，无法评价"
                };
            }

            // 如果所有区域都正常，返回O-RADS 1
            if (report.AdnexaRegions.All(r => r.IsNormal))
            {
                return new ORadsResult
                {
                    Level = 1,
                    Reason = "所有区域未见明显异常"
                };
            }

            // 检查是否有积液（直接判断为O-RADS 5）
            if (report.HasFluid)
            {
                return new ORadsResult
                {
                    Level = 5,
                    Reason = "存在腹水积液，恶性高风险（≥50%恶性风险）"
                };
            }

            // 计算所有异常区域的O-RADS分级，取最高风险等级
            int maxLevel = 0;
            ORadsResult? maxResult = null;

            foreach (var region in report.AdnexaRegions.Where(r => r.IsAbnormal))
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
                Reason = "评价不完全，数据不完整"
            };
        }

        /// <summary>
        /// 计算单个区域的O-RADS分级
        /// </summary>
        public ORadsResult CalculateRegionORads(AdnexaRegion region)
        {
            if (region == null || region.IsNormal)
            {
                var orads1Result = CheckORads1(region);
                if (orads1Result != null)
                    return orads1Result;
                
                // 如果CheckORads1返回null，返回默认的O-RADS 1
                return new ORadsResult
                {
                    Level = 1,
                    Reason = "绝经前正常卵巢，未见明显异常"
                };
            }

            // 按优先级从高到低判断
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
                Reason = "评价不完全，数据不完整"
            };
        }

        #region O-RADS 5: 恶性高风险（≥50%恶性风险）

        private ORadsResult? CheckORads5(AdnexaRegion region)
        {
            // 1. 单房囊肿，任意大小，有≥4个乳头状突起
            if (region.HasSolidCyst && region.SolidCyst.HasPapillary)
            {
                int papillaryCount = ParsePapillaryCount(region.SolidCyst.PapillaryCount);
                if (papillaryCount >= 4)
                {
                    string countText = GetPapillaryCountText(region.SolidCyst.PapillaryCount, papillaryCount);
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = $"单房囊肿，{countText}，恶性高风险（≥50%恶性风险）"
                    };
                }
            }

            // 2. 有实性成分的多房囊肿，任意大小，彩色血流评分3-4分
            if (region.HasMultilocularCyst && region.HasSolidCyst)
            {
                int flowScore = region.SolidCyst.BloodFlowScore;
                if (flowScore >= 3 && flowScore <= 4)
                {
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = $"有实性成分的多房囊肿，彩色血流评分{flowScore}分，恶性高风险（≥50%恶性风险）"
                    };
                }
            }

            // 3. 实性肿物，光滑，任意大小，彩色血流评分4分
            if (region.HasSolidMass)
            {
                if (region.SolidMass.Boundary == "规则" && region.SolidMass.BloodFlowScore == 4)
                {
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = "实性肿物，光滑，彩色血流评分4分，恶性高风险（≥50%恶性风险）"
                    };
                }

                // 4. 实性肿物，不规整，任意大小，任意血流评分
                if (region.SolidMass.Boundary == "不规则")
                {
                    return new ORadsResult
                    {
                        Level = 5,
                        Reason = "实性肿物，不规整，任意血流评分，恶性高风险（≥50%恶性风险）"
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
                bool isSmoothSeptum = region.MultilocularCyst.EchoSmoothSeptum;
                bool isRoughSeptum = region.MultilocularCyst.EchoRoughSeptum;
                int flowScore = region.MultilocularCyst.BloodFlowScore;

                // ≥10cm，内壁光滑，彩色血流评分1~3分
                if (maxDiameter >= 10 && isSmoothWall && flowScore >= 1 && flowScore <= 3)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"多房囊肿（无实性成分），≥10cm，内壁光滑，彩色血流评分{flowScore}分，恶性中等风险（10%~50%恶性风险）"
                    };
                }

                // 任意大小，内壁光滑，彩色血流评分4分
                if (isSmoothWall && flowScore == 4)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"多房囊肿（无实性成分），内壁光滑，彩色血流评分4分，恶性中等风险（10%~50%恶性风险）"
                    };
                }

                // 任意大小，内壁不规整和（或）分隔不规则，任意彩色血流评分
                if (isRoughWall || isRoughSeptum)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = "多房囊肿（无实性成分），内壁不规整或分隔不规则，恶性中等风险（10%~50%恶性风险）"
                    };
                }
            }

            // 2. 有实性成分的单房囊肿：任意大小，0~3个乳头样突起
            if (region.HasSolidCyst && region.SolidCyst.HasPapillary)
            {
                int papillaryCount = ParsePapillaryCount(region.SolidCyst.PapillaryCount);
                if (papillaryCount >= 0 && papillaryCount <= 3)
                {
                    string countText = GetPapillaryCountText(region.SolidCyst.PapillaryCount, papillaryCount);
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"有实性成分的单房囊肿，{countText}，恶性中等风险（10%~50%恶性风险）"
                    };
                }
            }

            // 3. 有实性成分的多房囊肿：任意大小，彩色血流评分1~2分
            if (region.HasMultilocularCyst && region.HasSolidCyst)
            {
                int flowScore = region.SolidCyst.BloodFlowScore;
                if (flowScore >= 1 && flowScore <= 2)
                {
                    return new ORadsResult
                    {
                        Level = 4,
                        Reason = $"有实性成分的多房囊肿，彩色血流评分{flowScore}分，恶性中等风险（10%~50%恶性风险）"
                    };
                }
            }

            // 4. 实性肿物：光滑，任意大小，彩色血流评分2~3分
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
                            Reason = $"实性肿物，光滑，彩色血流评分{flowScore}分，恶性中等风险（10%~50%恶性风险）"
                        };
                    }
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 3: 恶性低风险风险病变（1%~10%恶性风险）

        private ORadsResult? CheckORads3(AdnexaRegion region)
        {
            // 1. 单房囊肿≥10cm（单纯或非单纯性囊肿）
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

                // 2. 有厚度 ＜3mm 的不规则内壁的单房囊肿，任意大小
                if (region.UnilocularCyst.EchoRoughWall)
                {
                    // 注意：这里假设不规则内壁意味着可能有厚度<3mm的突起
                    // 实际可能需要更详细的判断
                    return new ORadsResult
                    {
                        Level = 3,
                        Reason = "单房囊肿，内壁不光滑（可能有厚度<3mm的不规则内壁），任意大小，恶性低风险（1%~10%恶性风险）"
                    };
                }
            }

            // 3. 多房囊肿＜10cm，光滑内壁，彩色血流评分1~3分
            if (region.HasMultilocularCyst)
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

            // 4. 实性，光滑，任意大小，彩色血流评分1分
            if (region.HasSolidMass)
            {
                if (region.SolidMass.Boundary == "规则" && region.SolidMass.BloodFlowScore == 1)
                {
                    return new ORadsResult
                    {
                        Level = 3,
                        Reason = "实性肿物，光滑，彩色血流评分1分，恶性低风险（1%~10%恶性风险）"
                    };
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 2: 几乎可以肯定的良性病变（＜1%恶性风险）

        private ORadsResult? CheckORads2(AdnexaRegion region)
        {
            // 1. 单纯性囊肿：≤3cm, >3≤5cm, >5<10cm
            if (region.HasUnilocularCyst && region.UnilocularCyst.IsSimpleCyst)
            {
                double maxDiameter = GetMaxDiameter(
                    region.UnilocularCyst.Length,
                    region.UnilocularCyst.Width,
                    region.UnilocularCyst.Height);

                if (maxDiameter > 0 && maxDiameter < 10)
                {
                    string sizeDesc = maxDiameter <= 3 ? "≤3cm" : (maxDiameter <= 5 ? ">3≤5cm" : ">5<10cm");
                    return new ORadsResult
                    {
                        Level = 2,
                        Reason = $"单纯性囊肿，{sizeDesc}，几乎可以肯定的良性病变（＜1%恶性风险）",
                        IsTypicalBenign = maxDiameter <= 10,
                        Suggestion = maxDiameter <= 10 ? "如为典型良性病变（≤10cm的典型卵巢出血性囊肿、典型卵巢成熟性畸胎瘤、典型卵巢子宫内膜异位囊肿、单纯卵巢旁囊肿、典型的单纯性腹膜包涵囊肿、典型输卵管积水），请在备注中注明。" : string.Empty
                    };
                }
            }

            // 2. 非单纯性单房囊肿：内壁光滑，≤3cm 或 >3<10cm
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
                        string sizeDesc = maxDiameter <= 3 ? "≤3cm" : ">3<10cm";
                        return new ORadsResult
                        {
                            Level = 2,
                            Reason = $"非单纯性单房囊肿，内壁光滑，{sizeDesc}，几乎可以肯定的良性病变（＜1%恶性风险）",
                            IsTypicalBenign = maxDiameter <= 10,
                            Suggestion = maxDiameter <= 10 ? "如为典型良性病变（≤10cm的典型卵巢出血性囊肿、典型卵巢成熟性畸胎瘤、典型卵巢子宫内膜异位囊肿、单纯卵巢旁囊肿、典型的单纯性腹膜包涵囊肿、典型输卵管积水），请在备注中注明。" : string.Empty
                        };
                    }
                }
            }

            return null;
        }

        #endregion

        #region O-RADS 1: 绝经前正常卵巢

        private ORadsResult? CheckORads1(AdnexaRegion? region)
        {
            if (region == null || !region.IsNormal)
            {
                return null;
            }

            // 判断是否为≤3cm的单纯性囊肿（通过正常状态下的卵泡信息判断）
            // 注意：正常状态下，如果MaxCystDiameter <= 3，可能是单纯性囊肿
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

            // 处理各种可能的格式
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
                ">3" => 4,  // 半角大于号（UI中使用的格式）
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
                return "有乳头（数量未选择）";
            }

            // 如果原始值包含">"或">="，显示为">3个"或">=4个"
            string trimmedCount = originalCount.Trim();
            if (trimmedCount.Contains(">") || trimmedCount.Contains("＞") || trimmedCount.Contains("≥"))
            {
                // 如果是">3"或">3"，显示为">3个"或"≥4个"
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

            return "有乳头（数量未选择）";
        }

        #endregion
    }
}

