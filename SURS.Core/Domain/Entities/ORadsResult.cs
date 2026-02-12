namespace SURS.Core.Domain.Entities
{
    /// <summary>
    /// O-RADS 分级结果
    /// </summary>
    public class ORadsResult
    {
        /// <summary>
        /// O-RADS 分级 (0-5)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 分级理由说明
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 建议提示（如典型良性病变类型）
        /// </summary>
        public string? Suggestion { get; set; }

        /// <summary>
        /// 是否为典型良性病变
        /// </summary>
        public bool IsTypicalBenign { get; set; }

        /// <summary>
        /// 分级字符串（如 "O-RADS 5"）
        /// </summary>
        public string LevelString => $"O-RADS {Level}";
    }
}

