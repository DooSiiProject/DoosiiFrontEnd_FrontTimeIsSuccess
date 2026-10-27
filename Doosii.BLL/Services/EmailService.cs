using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Doosii.BLL.Interfaces;

namespace Doosii.BLL.Services;

public class EmailService(IConfiguration configuration, IHostEnvironment environment, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendOtpEmailAsync(string toEmail, string otpCode, string purpose)
    {
        var settings = configuration.GetSection("EmailSettings");
        var host = settings["Host"];
        var mode = settings["DeliveryMode"] ?? (string.IsNullOrWhiteSpace(host) && environment.IsDevelopment() ? "Development" : "Smtp");
        if (mode == "Development" && environment.IsDevelopment()) {
            logger.LogWarning("DEVELOPMENT ONLY: email was not sent. Recovery OTP for {Email}: {Code} (5 minutes)", toEmail, otpCode);
            return;
        }
        if (mode != "Smtp" || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(settings["FromAddress"]))
            throw new InvalidOperationException("SMTP is not configured.");
        using var client = new SmtpClient(host, settings.GetValue("Port", 587)) {
            EnableSsl = settings.GetValue("EnableSsl", true),
            UseDefaultCredentials = false,
        };
        if (!string.IsNullOrWhiteSpace(settings["Username"])) client.Credentials = new NetworkCredential(settings["Username"], settings["Password"]);
        using var message = new MailMessage(new MailAddress(settings["FromAddress"]!, settings["FromName"] ?? "DooSii"), new MailAddress(toEmail)) {
            Subject = "DooSii - Mã khôi phục mật khẩu",
            Body = $"Mã khôi phục mật khẩu của bạn là {otpCode}. Mã hết hạn sau 5 phút. Không chia sẻ mã này. Nếu bạn không yêu cầu, hãy bỏ qua email này.",
            IsBodyHtml = false,
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message, timeout.Token);
    }
}
