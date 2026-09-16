using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EWFDS.Common.Email
{

    /// <summary>
    /// Email attachment model
    /// </summary>
    public class EmailAttachment
    {
        /// <summary>
        /// File name for the attachment
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Content type/MIME type (e.g., "application/pdf", "image/png")
        /// </summary>
        public string ContentType { get; set; } = "application/octet-stream";

        /// <summary>
        /// Attachment content as byte array
        /// </summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Optional: Stream-based content (alternative to byte array)
        /// </summary>
        public Stream ContentStream { get; set; }
    }

    /// <summary>
    /// Email service interface for sending emails via SMTP.
    /// Implementation is provided by EWFDS.BlazorInfrastructure.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends an error notification email to the configured notification recipient
        /// </summary>
        Task<EmailResult> SendErrorEmailAsync(string subject, string body, bool isHTML = false);

        /// <summary>
        /// Sends an email without attachments
        /// </summary>
        Task<EmailResult> SendEmailAsync(string from, string to, string subject, string body, bool isHtml = true);

        /// <summary>
        /// Sends an email with attachments
        /// </summary>
        Task<EmailResult> SendEmailWithAttachmentsAsync(string from, string to, string subject, string body, IEnumerable<EmailAttachment> attachments, bool isHtml = true);

        /// <summary>
        /// Sends an email with file attachments from disk paths
        /// </summary>
        Task<EmailResult> SendEmailWithFileAttachmentsAsync(string from, string to, string subject, string body, IEnumerable<string> attachmentFilePaths, bool isHtml = true);

        /// <summary>
        /// Sends an email to multiple recipients
        /// </summary>
        Task<EmailResult> SendEmailToMultipleRecipientsAsync(string from, IEnumerable<string> toList, string subject, string body, bool isHtml = true);

        /// <summary>
        /// Sends an email with CC and BCC support
        /// </summary>
        Task<EmailResult> SendEmailAdvancedAsync(string from, string to, string subject, string body, IEnumerable<string> cc = null, IEnumerable<string> bcc = null, IEnumerable<EmailAttachment> attachments = null, bool isHtml = true);

        /// <summary>
        /// Validates an email address format
        /// </summary>
        Task<bool> ValidateEmailAddressAsync(string address);
    }
}
