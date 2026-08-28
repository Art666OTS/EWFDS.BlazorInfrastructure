using BusinessLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace EWFDS.BlazorInfrastructure.Services.Email
{
    /// <summary>
    /// MailGun SMTP email service implementation with attachment support and comprehensive error handling.
    /// Implements BusinessLibrary.IEmailService for use across all projects.
    /// </summary>
    public class MailGunEmailService : BusinessLibrary.IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MailGunEmailService> _logger;

        // Configuration keys
        private const string CONFIG_PATH = "SystemSettings:EmailSettings:MailGun";
        private const string DEFAULT_EMAIL_KEY = "MAILGUN_DEFAULT_EMAIL";
        private const string SERVER_KEY = "MAILGUN_SERVER";
        private const string USERNAME_KEY = "MAILGUN_USERNAME";
        private const string API_KEY_KEY = "MAILGUN_API_KEY";
        private const string PORT_KEY = "MAILGUN_PORT";
        private const string TLS_SSL_KEY = "MAILGUN_TLS_SSL";

        public MailGunEmailService(IConfiguration configuration, ILogger<MailGunEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<EmailResult> SendErrorEmailAsync(string subject, string body, bool isHTML = false)
        {
            string FromName = _configuration[$"EmailSettings:MailGun:MAILGUN_DEFAULT_EMAIL"] ?? string.Empty;
            string ToName = _configuration[$"EmailSettings:MailGun:MAILGUN_USERNAME"] ?? string.Empty;
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(FromName))
                errors.Add("Email must have a sender");
            if (string.IsNullOrWhiteSpace(ToName))
                errors.Add("Email must have a recipient");
            if (string.IsNullOrWhiteSpace(body))
                errors.Add("Email message cannot be null");
            if (errors.Count > 0)
                return EmailResultFactory.Failure(string.Join("; ", errors));

            return await SendEmailAsync(FromName, ToName, subject, body, isHTML);
        }

        /// <summary>
        /// Sends an email without attachments
        /// </summary>
        public async Task<EmailResult> SendEmailAsync(string? from, string to, string subject, string body, bool isHtml = true)
        {
            return await SendEmailAdvancedAsync(from, to, subject, body, null, null, null, isHtml);
        }

        /// <summary>
        /// Sends an email with attachments
        /// </summary>
        public async Task<EmailResult> SendEmailWithAttachmentsAsync(string? from, string to, string subject, string body, IEnumerable<EmailAttachment> attachments, bool isHtml = true)
        {
            return await SendEmailAdvancedAsync(from, to, subject, body, null, null, attachments, isHtml);
        }

        /// <summary>
        /// Sends an email with file attachments from disk paths
        /// </summary>
        public async Task<EmailResult> SendEmailWithFileAttachmentsAsync(string? from, string to, string subject, string body, IEnumerable<string> attachmentFilePaths, bool isHtml = true)
        {
            try
            {
                var attachments = new List<EmailAttachment>();
                var filePaths = attachmentFilePaths?.ToList() ?? [];

                foreach (var filePath in filePaths)
                {
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        _logger.LogWarning("Skipping empty file path in attachments");
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning("Attachment file not found: {FilePath}", filePath);
                        return EmailResultFactory.Failure($"Attachment file not found: {filePath}");
                    }

                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var contentType = GetMimeType(filePath);
                        var content = await File.ReadAllBytesAsync(filePath);

                        attachments.Add(new EmailAttachment
                        {
                            FileName = fileName,
                            ContentType = contentType,
                            Content = content
                        });

                        _logger.LogInformation("Loaded file attachment: {FileName} ({ContentType}, {Size} bytes)",
                            fileName, contentType, content.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read attachment file: {FilePath}", filePath);
                        return EmailResultFactory.Failure($"Failed to read attachment file: {filePath}", ex);
                    }
                }

                return await SendEmailWithAttachmentsAsync(from, to, subject, body, attachments, isHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing file attachments");
                return EmailResultFactory.Failure("An unexpected error occurred while processing file attachments", ex);
            }
        }

        /// <summary>
        /// Gets MIME type based on file extension
        /// </summary>
        private static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Sends an email to multiple recipients
        /// </summary>
        public async Task<EmailResult> SendEmailToMultipleRecipientsAsync(string? from, IEnumerable<string> toList, string subject, string body, bool isHtml = true)
        {
            try
            {
                // Validate input
                var recipients = toList?.ToList() ?? [];
                if (recipients.Count == 0)
                {
                    return EmailResultFactory.Failure("No recipients specified");
                }

                // Validate email addresses
                var invalidEmails = recipients.Where(email => !IsValidEmail(email)).ToList();
                if (invalidEmails.Count != 0)
                {
                    return EmailResultFactory.Failure($"Invalid email addresses: {string.Join(", ", invalidEmails)}");
                }

                // Get configuration
                var config = GetMailGunConfiguration();
                if (!config.IsValid)
                {
                    return EmailResultFactory.Failure("MailGun configuration is invalid or incomplete");
                }

                // Prepare sender
                string senderEmail = string.IsNullOrWhiteSpace(from) ? config.DefaultEmail : from;
                if (!IsValidEmail(senderEmail))
                {
                    return EmailResultFactory.Failure($"Invalid sender email address: {senderEmail}");
                }
                // Apply domain check for sender in non-production environments
                senderEmail = CheckMailDomain(senderEmail, config.AppMode);

                // Prepend application mode to subject for non-production environments
                string emailSubject = PrependAppModeToSubject(subject, config.AppMode);

                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail),
                    Subject = emailSubject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                // Add all recipients (with domain check for non-production)
                foreach (var recipient in recipients)
                {
                    string checkedRecipient = CheckMailDomain(recipient, config.AppMode);
                    message.To.Add(new MailAddress(checkedRecipient));
                }

                // Send email
                return await SendMailMessageAsync(message, config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to multiple recipients");
                return EmailResultFactory.Failure("An unexpected error occurred while sending email", ex);
            }
        }

        /// <summary>
        /// Sends an email with full control over CC, BCC, and attachments
        /// </summary>
        public async Task<EmailResult> SendEmailAdvancedAsync(string? from, string to, string subject, string body, IEnumerable<string>? cc = null, IEnumerable<string>? bcc = null, IEnumerable<EmailAttachment>? attachments = null, bool isHtml = true)
        {
            try
            {
                // Validate primary recipient
                if (string.IsNullOrWhiteSpace(to))
                {
                    return EmailResultFactory.Failure("Recipient email address is required");
                }

                if (!IsValidEmail(to))
                {
                    return EmailResultFactory.Failure($"Invalid recipient email address: {to}");
                }

                // Get configuration
                var config = GetMailGunConfiguration();
                if (!config.IsValid)
                {
                    return EmailResultFactory.Failure("MailGun configuration is invalid or incomplete");
                }

                // Apply domain check for non-production environments (redirect to internal domain)
                to = CheckMailDomain(to, config.AppMode);

                // Prepare sender
                string senderEmail = string.IsNullOrWhiteSpace(from) ? config.DefaultEmail : from;
                if (!IsValidEmail(senderEmail))
                {
                    return EmailResultFactory.Failure($"Invalid sender email address: {senderEmail}");
                }
                // Apply domain check for sender in non-production environments
                senderEmail = CheckMailDomain(senderEmail, config.AppMode);

                // Prepend application mode to subject for non-production environments
                string emailSubject = PrependAppModeToSubject(subject, config.AppMode);

                // Build message
                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail),
                    Subject = emailSubject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                // Handle semicolon-delimited recipients
                string[] toAddresses = to.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string toEmail in toAddresses)
                {
                    message.To.Add(new MailAddress(toEmail));
                }

                // Add CC recipients
                if (cc != null)
                {
                    foreach (var ccEmail in cc.Where(e => !string.IsNullOrWhiteSpace(e)))
                    {
                        if (IsValidEmail(ccEmail))
                        {
                            string checkedCcEmail = CheckMailDomain(ccEmail, config.AppMode);
                            message.CC.Add(new MailAddress(checkedCcEmail));
                        }
                        else
                        {
                            _logger.LogWarning("Skipping invalid CC email address: {Email}", ccEmail);
                        }
                    }
                }

                // Add BCC recipients
                if (bcc != null)
                {
                    foreach (var bccEmail in bcc.Where(e => !string.IsNullOrWhiteSpace(e)))
                    {
                        if (IsValidEmail(bccEmail))
                        {
                            string checkedBccEmail = CheckMailDomain(bccEmail, config.AppMode);
                            message.Bcc.Add(new MailAddress(checkedBccEmail));
                        }
                        else
                        {
                            _logger.LogWarning("Skipping invalid BCC email address: {Email}", bccEmail);
                        }
                    }
                }

                // Add attachments
                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        try
                        {
                            Attachment mailAttachment;

                            if (attachment.ContentStream != null)
                            {
                                // Stream-based attachment
                                mailAttachment = new Attachment(attachment.ContentStream, attachment.FileName, attachment.ContentType);
                            }
                            else if (attachment.Content != null && attachment.Content.Length > 0)
                            {
                                // Byte array attachment
                                var stream = new MemoryStream(attachment.Content);
                                mailAttachment = new Attachment(stream, attachment.FileName, attachment.ContentType);
                            }
                            else
                            {
                                _logger.LogWarning("Skipping attachment with no content: {FileName}", attachment.FileName);
                                continue;
                            }

                            mailAttachment.ContentDisposition!.FileName = attachment.FileName;
                            message.Attachments.Add(mailAttachment);

                            _logger.LogInformation("Added attachment: {FileName} ({ContentType})", attachment.FileName, attachment.ContentType);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to add attachment: {FileName}", attachment.FileName);
                            return EmailResultFactory.Failure($"Failed to add attachment: {attachment.FileName}", ex);
                        }
                    }
                }

                // Send email
                return await SendMailMessageAsync(message, config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to {To}", to);
                return EmailResultFactory.Failure("An unexpected error occurred while sending email", ex);
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Sends the mail message using MailGun SMTP
        /// </summary>
        private async Task<EmailResult> SendMailMessageAsync(MailMessage message, MailGunConfig config)
        {
            SmtpClient? smtpClient = null;
            try
            {
                // Create SMTP client
                smtpClient = new SmtpClient(config.Server, config.Port)
                {
                    EnableSsl = config.EnableTls,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(config.Username, config.ApiKey),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000 // 30 seconds
                };

                _logger.LogInformation(
                    "Sending email via MailGun - From: {From}, To: {To}, Subject: {Subject}, Attachments: {AttachmentCount}",
                    message.From?.Address,
                    string.Join(", ", message.To.Select(t => t.Address)),
                    message.Subject,
                    message.Attachments.Count);

                // Send email
                await smtpClient.SendMailAsync(message);

                _logger.LogInformation("Email sent successfully to {To}",
                    string.Join(", ", message.To.Select(t => t.Address)));

                return EmailResultFactory.Success("Email sent successfully");
            }
            catch (SmtpException smtpEx)
            {
                string errorMessage = $"SMTP error sending email: {smtpEx.Message}";
                _logger.LogError(smtpEx, "SMTP error - Status: {StatusCode}, To: {To}",
                    smtpEx.StatusCode,
                    string.Join(", ", message.To.Select(t => t.Address)));

                return EmailResultFactory.Failure(errorMessage, smtpEx);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error sending email: {ex.Message}";
                _logger.LogError(ex, "Error sending email to {To}",
                    string.Join(", ", message.To.Select(t => t.Address)));

                return EmailResultFactory.Failure(errorMessage, ex);
            }
            finally
            {
                smtpClient?.Dispose();
            }
        }

        /// <summary>
        /// Retrieves MailGun configuration from appsettings
        /// </summary>
        private MailGunConfig GetMailGunConfiguration()
        {
            try
            {
                var config = new MailGunConfig
                {
                    DefaultEmail = _configuration[$"{CONFIG_PATH}:{DEFAULT_EMAIL_KEY}"] ?? string.Empty,
                    Server = _configuration[$"{CONFIG_PATH}:{SERVER_KEY}"] ?? string.Empty,
                    Username = _configuration[$"{CONFIG_PATH}:{USERNAME_KEY}"] ?? string.Empty,
                    ApiKey = _configuration[$"{CONFIG_PATH}:{API_KEY_KEY}"] ?? string.Empty,
                    Port = int.TryParse(_configuration[$"{CONFIG_PATH}:{PORT_KEY}"], out int port) ? port : 587,
                    EnableTls = (_configuration[$"{CONFIG_PATH}:{TLS_SSL_KEY}"] ?? "enabled").Equals("enabled", StringComparison.OrdinalIgnoreCase),
                    AppMode = _configuration["SystemSettings:ApplicationMode"] ?? string.Empty
                };

                // Validate configuration
                if (string.IsNullOrWhiteSpace(config.Server))
                {
                    _logger.LogError("MailGun SMTP server not configured");
                }
                if (string.IsNullOrWhiteSpace(config.Username))
                {
                    _logger.LogError("MailGun username not configured");
                }
                if (string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    _logger.LogError("MailGun API key not configured");
                }
                if (string.IsNullOrWhiteSpace(config.DefaultEmail))
                {
                    _logger.LogWarning("MailGun default email not configured");
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving MailGun configuration");
                return new MailGunConfig();
            }
        }

        /// <summary>
        /// Validates email address format
        /// </summary>
        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Simple regex for email validation
                var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return emailRegex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        private string CheckMailDomain(string s, string AppMode)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            // Handle semicolon-delimited email addresses
            string[] emails = s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (emails.Length == 0)
                return string.Empty;

            var processedEmails = new List<string>();

            foreach (string email in emails)
            {
                processedEmails.Add(CheckSingleMailDomain(email, AppMode));
            }

            return string.Join(";", processedEmails);
        }

        private string CheckSingleMailDomain(string s, string AppMode)
        {
            // Only check one email address at a time as the mail.add does not like concatenated email addresses.
            string res = string.Empty;
            if (AppMode.Trim().Equals(string.Empty))
            {
                res = s;
            }
            else
            {
                if (AppMode.ToLower().Trim().Equals("development") || AppMode.ToLower().Trim().Equals("staging"))
                {
                    // Only do this for development environments.
                    string[] d = s.Split('@');
                    if (d.Length == 2 && d[1].ToLower().Equals("wfds.com.au"))
                    {
                        res = s;
                    }
                    else if (d.Length == 2)
                    {
                        res = string.Format("{0}@{1}", d[0], "wfds.com.au");
                    }
                    else
                    {
                        res = s; // Invalid email format, return as-is
                    }
                }
                else
                {
                    res = s; // Production mode, return original
                }
            }
            return res;
        }

        public async Task<bool> ValidateEmailAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return await Task.FromResult(false);

            return await Task.FromResult(IsValidEmail(address));
        }

        /// <summary>
        /// Prepends the application mode to the subject line for non-production environments
        /// </summary>
        private static string PrependAppModeToSubject(string subject, string appMode)
        {
            if (string.IsNullOrWhiteSpace(appMode))
                return subject;

            string mode = appMode.ToLower().Trim();
            if (mode.Equals("development") || mode.Equals("staging"))
            {
                return $"[{appMode.ToUpper()}] {subject}";
            }

            return subject;
        }

        #endregion Private Helper Methods

        #region Configuration Model

        /// <summary>
        /// MailGun configuration model
        /// </summary>
        private class MailGunConfig
        {
            public string DefaultEmail { get; set; } = string.Empty;
            public string Server { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public int Port { get; set; } = 587;
            public bool EnableTls { get; set; } = true;
            public string AppMode { get; set; } = string.Empty;

            public bool IsValid =>
                !string.IsNullOrWhiteSpace(Server) &&
                !string.IsNullOrWhiteSpace(Username) &&
                !string.IsNullOrWhiteSpace(ApiKey);
        }

        #endregion Configuration Model
    }
}
