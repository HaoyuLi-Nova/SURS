namespace SURS.App.Models
{
    /// <summary>
    /// O-RADS分级计算结果
    /// </summary>
    public class ORadsResult
    {
        /// <summary>
        /// O-RADS分级 (0-5)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 分级理由说明
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 建议提示（如典型良性病变类型）
        /// </summary>
        public string Suggestion { get; set; } = string.Empty;

        /// <summary>
        /// 是否为典型良性病变（需要医生手动选择）
        /// </summary>
        public bool IsTypicalBenign { get; set; }

        /// <summary>
        /// 获取O-RADS分级字符串
        /// </summary>
        public string LevelString => $"O-RADS {Level}";
    }
}

