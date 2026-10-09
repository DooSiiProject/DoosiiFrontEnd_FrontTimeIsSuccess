using Microsoft.Extensions.Logging;
using Doosii.BLL.Interfaces;

namespace Doosii.BLL.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public Task SendOtpEmailAsync(string toEmail, string otpCode, string purpose)
        {
            // Console simulation for local development & testing
            _logger.LogInformation("==================================================");
            _logger.LogInformation("[DOOSII EMAIL SERVICE] Sending OTP to: {Email}", toEmail);
            _logger.LogInformation("[DOOSII EMAIL SERVICE] Purpose: {Purpose}", purpose);
            _logger.LogInformation("[DOOSII EMAIL SERVICE] OTP Code: >> {OtpCode} << (Expires in 5 minutes)", otpCode);
            _logger.LogInformation("==================================================");

            return Task.CompletedTask;
        }
    }
}
