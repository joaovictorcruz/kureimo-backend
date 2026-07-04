using Kureimo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kureimo.Infra.Email
{
    public class ResendEmailService : IEmailService
    {
        private readonly IResend _resend;
        private readonly string _frontendUrl;
        private readonly ILogger<ResendEmailService> _logger;

        public ResendEmailService(
            IResend resend,
            IConfiguration configuration,
            ILogger<ResendEmailService> logger)
        {
            _resend = resend;
            _frontendUrl = configuration["FrontendUrl"]
                ?? throw new InvalidOperationException("FrontendUrl não configurada.");
            _logger = logger;
        }

        public async Task SendVerificationCodeAsync(string toEmail, string code, string usageType, CancellationToken ct = default)
        {
            var subject = usageType switch
            {
                "ForgotPassword" => "Código de recuperação de senha — Kureimo",
                "SignIn" => "Código de verificação de login — Kureimo",
                "Register" => "Código de verificação de cadastro — Kureimo",
                _ => "Código de verificação — Kureimo"
            };

            var message = new EmailMessage();
            message.From = "onboarding@resend.dev"; // até pegar domínio próprio
            message.To.Add(toEmail);
            message.Subject = subject;
            message.HtmlBody = $"<p>Seu código de verificação é: <strong>{code}</strong></p><p>Ele expira em alguns minutos.</p>";

            await _resend.EmailSendAsync(message);
        }
    }
}
