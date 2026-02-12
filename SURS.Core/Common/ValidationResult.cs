namespace SURS.Core.Common
{
    /// <summary>
    /// 验证结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid => !Errors.Any();

        /// <summary>
        /// 验证错误列表
        /// </summary>
        public List<ValidationError> Errors { get; } = new List<ValidationError>();

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(string propertyName, string errorMessage)
        {
            Errors.Add(new ValidationError(propertyName, errorMessage));
        }

        /// <summary>
        /// 添加错误
        /// </summary>
        public void AddError(ValidationError error)
        {
            Errors.Add(error);
        }
    }

    /// <summary>
    /// 验证错误
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }

        public ValidationError(string propertyName, string errorMessage)
        {
            PropertyName = propertyName;
            ErrorMessage = errorMessage;
        }
    }
}

