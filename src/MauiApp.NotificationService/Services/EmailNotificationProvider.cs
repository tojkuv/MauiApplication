using System.Net;
using System.Net.Mail;

namespace MauiApp.NotificationService.Services;

public class EmailNotificationProvider : IEmailNotificationProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationProvider> _logger;
    private readonly SmtpClient _smtpClient;

    public EmailNotificationProvider(IConfiguration configuration, ILogger<EmailNotificationProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var username = _configuration["Email:Username"] ?? "noreply@example.com";
        var password = _configuration["Email:Password"] ?? "password";

        _smtpClient = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
        };
    }

    public async Task<bool> SendEmailNotificationAsync(string email, string subject, string body, string? actionUrl = null)
    {
        try
        {
            var fromAddress = _configuration["Email:FromAddress"] ?? "noreply@example.com";
            var fromName = _configuration["Email:FromName"] ?? "MauiApp Notifications";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = CreateEmailBody(subject, body, actionUrl),
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await _smtpClient.SendMailAsync(mailMessage);
            
            _logger.LogInformation("Email notification sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification to {Email}", email);
            return false;
        }
    }

    private string CreateEmailBody(string subject, string body, string? actionUrl)
    {
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{subject}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 30px 20px;
            text-align: center;
            border-radius: 8px 8px 0 0;
        }}
        .content {{
            background: #f8f9fa;
            padding: 30px 20px;
            border-radius: 0 0 8px 8px;
        }}
        .message {{
            background: white;
            padding: 20px;
            border-radius: 6px;
            margin-bottom: 20px;
            border-left: 4px solid #667eea;
        }}
        .action-button {{
            display: inline-block;
            background: #667eea;
            color: white;
            padding: 12px 24px;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            margin-top: 15px;
        }}
        .footer {{
            text-align: center;
            color: #666;
            font-size: 14px;
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #e0e0e0;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>MauiApp</h1>
        <p>Project Management & Collaboration</p>
    </div>
    <div class='content'>
        <div class='message'>
            <h2>{subject}</h2>
            <p>{body}</p>
            {(string.IsNullOrEmpty(actionUrl) ? "" : $"<a href='{actionUrl}' class='action-button'>View Details</a>")}
        </div>
        <div class='footer'>
            <p>This email was sent by MauiApp. If you no longer wish to receive these notifications, you can update your preferences in the app.</p>
            <p>&copy; {DateTime.Now.Year} MauiApp. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

        return htmlBody;
    }

    public void Dispose()
    {
        _smtpClient?.Dispose();
    }
}