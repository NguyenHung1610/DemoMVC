using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace DemoMVC.Services;

public sealed class SmtpStudentEmailSender : IStudentEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpStudentEmailSender> _logger;

    public SmtpStudentEmailSender(
        IOptions<SmtpSettings> options,
        ILogger<SmtpStudentEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string recipient, string subject, string htmlBody)
    {
        var host = _settings.Host.Trim();
        var username = _settings.Username.Trim();
        var password = string.Concat(_settings.Password.Where(character => !char.IsWhiteSpace(character)));
        var senderEmail = _settings.SenderEmail.Trim();
        var isGmail = string.Equals(host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase);

        if (isGmail && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
        {
            throw new EmailDeliveryException(
                "Gmail SMTP cần tài khoản và Mật khẩu ứng dụng. Hãy bật xác minh 2 bước " +
                "trên Google, tạo Mật khẩu ứng dụng 16 ký tự rồi đặt Smtp:Username và Smtp:Password.");
        }

        var missingSettings = new List<string>();
        if (string.IsNullOrWhiteSpace(host))
        {
            missingSettings.Add("Smtp:Host");
        }

        if (_settings.Port is < 1 or > 65535)
        {
            missingSettings.Add("Smtp:Port");
        }

        if (isGmail)
        {
            senderEmail = username;
        }

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            missingSettings.Add("Smtp:SenderEmail");
        }

        if (!string.IsNullOrWhiteSpace(username) &&
            string.IsNullOrWhiteSpace(password))
        {
            missingSettings.Add("Smtp:Password");
        }

        if (missingSettings.Count > 0)
        {
            throw new EmailDeliveryException(
                $"Thiếu hoặc sai cấu hình SMTP: {string.Join(", ", missingSettings)}. " +
                "Hãy cấu hình bằng User Secrets rồi khởi động lại ứng dụng.");
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, _settings.SenderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(recipient));

            using var client = new SmtpClient(host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(
                    username,
                    password);
            }

            await client.SendMailAsync(message);
        }
        catch (Exception exception) when (
            exception is SmtpException or InvalidOperationException or FormatException)
        {
            if (exception is SmtpException smtpException)
            {
                var serverResponse = smtpException.InnerException?.Message;
                _logger.LogError(
                    smtpException,
                    "SMTP delivery failed. Host: {Host}; port: {Port}; status: {StatusCode}; server response: {ServerResponse}",
                    host,
                    _settings.Port,
                    smtpException.StatusCode,
                    serverResponse);
            }

            var message = exception switch
            {
                SmtpException when string.Equals(
                    host,
                    "smtp.gmail.com",
                    StringComparison.OrdinalIgnoreCase) =>
                    "Gmail từ chối đăng nhập SMTP. Smtp:Username phải là đúng tài khoản Gmail " +
                    "đã tạo App Password; Smtp:Password phải là App Password còn hiệu lực " +
                    "(hệ thống tự bỏ khoảng trắng). Với Gmail, địa chỉ người gửi tự lấy từ Username. " +
                    "Kiểm tra cấu hình của đúng dự án E:\\DemoMVC và khởi động lại ứng dụng.",
                SmtpException =>
                    "Máy chủ SMTP từ chối gửi email. Kiểm tra Host, Port, SSL, Username và Password.",
                FormatException =>
                    "Địa chỉ email gửi hoặc nhận không hợp lệ. Kiểm tra Smtp:SenderEmail và email người nhận.",
                _ =>
                    "Cấu hình SMTP không hợp lệ. Kiểm tra Host, Port, SSL và thông tin đăng nhập."
            };
            throw new EmailDeliveryException(
                message,
                exception);
        }
    }

}
