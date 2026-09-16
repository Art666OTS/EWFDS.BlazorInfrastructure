using System;

namespace EWFDS.Common.Email
{
    /// <summary>
    /// Email send result
    /// </summary>
    public class EmailResult
    {
        /// <summary>
        /// Indicates if email was sent successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Success or error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Exception details if an error occurred
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Message ID from email provider (if available)
        /// </summary>
        public string MessageId { get; set; }

        public EmailResult() { }

        public EmailResult(bool success, string message = null)
        {
            Success = success;
            Message = message ?? string.Empty;
        }
    }

    public class EmailResultFactory
    {
        public static EmailResult Success(string message, string messageId = null)
        {
            return new EmailResult
            {
                Success = true,
                Message = message,
                MessageId = messageId
            };
        }

        public static EmailResult Failure(string message, Exception exception = null)
        {
            return new EmailResult
            {
                Success = false,
                Message = message,
                Exception = exception
            };
        }
    }
}
