namespace SmartClinic.Services.Exceptions
{
    /// <summary>
    /// Exception cho các l?i liên quan ??n business logic (có th? hi?n th? cho user)
    /// </summary>
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message)
        {
        }

        public BusinessException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
