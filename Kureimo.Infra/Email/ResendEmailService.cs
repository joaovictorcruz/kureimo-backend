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
            _frontendUrl = configuration["FrontendUrl"] ?? throw new InvalidOperationException("FrontendUrl não configurada.");
            _logger = logger;
        }

        public async Task SendVerificationCodeAsync(string toEmail, string code, string usageType, CancellationToken ct = default)
        {
            var (subject, heading, description) = usageType switch
            {
                "ForgotPassword" => (
                    "Código de recuperação de senha — Kureimo",
                    "Redefinir sua senha",
                    "Recebemos um pedido para redefinir a senha da sua conta. Use o código abaixo para continuar."
                ),
                "SignIn" => (
                    "Código de verificação de login — Kureimo",
                    "Confirmar seu login",
                    "Use o código abaixo para confirmar que é você entrando na sua conta Kureimo."
                ),
                "Register" => (
                    "Código de verificação de cadastro — Kureimo",
                    "Bem-vindo(a) à Kureimo! 🍦",
                    "Falta só um passo para começar a claimar seus photocards favoritos. Confirme seu e-mail com o código abaixo."
                ),
                _ => (
                    "Código de verificação — Kureimo",
                    "Código de verificação",
                    "Use o código abaixo para continuar."
                )
            };

            var message = new EmailMessage();
            message.From = "onboarding@resend.dev"; // até pegar domínio próprio
            message.To.Add(toEmail);
            message.Subject = subject;
            message.HtmlBody = BuildVerificationEmailHtml(heading, description, code);

            await _resend.EmailSendAsync(message);
        }

        private static string BuildVerificationEmailHtml(string heading, string description, string code)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""pt-BR"">
            <head>
            <meta charset=""UTF-8"" />
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
            <title>Kureimo</title>
            <style>
                @media only screen and (max-width: 480px) {{
                .container {{ width: 100% !important; }}
                .card {{ padding: 28px 20px !important; }}
                .code {{ font-size: 30px !important; letter-spacing: 6px !important; }}
                }}
            </style>
            </head>
            <body style=""margin:0; padding:0; background-color:#FBF3EF; font-family:'Nunito', Verdana, sans-serif;"">
                <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#FBF3EF; padding: 32px 0;"">
                <tr>
                    <td align=""center"">
                    <table role=""presentation"" class=""container"" width=""480"" cellpadding=""0"" cellspacing=""0"" style=""width:480px; max-width:92%;"">

                        <!-- Logo / Header -->
                        <tr>
                        <td align=""center"" style=""padding-bottom: 24px;"">
                            <span style=""font-family:'DM Serif Display', Georgia, serif; font-size: 26px; color:#C0394A; letter-spacing: 0.5px;"">
                             Kureimo
                            </span>
                        </td>
                        </tr>

                        <!-- Card -->
                        <tr>
                        <td class=""card"" style=""background-color:#FFFFFF; border:1.5px solid #F2E1E1; border-radius: 20px; padding: 40px 36px; box-shadow: 0 4px 18px rgba(242,134,149,0.12);"">

                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                            <tr>
                                <td align=""center"" style=""padding-bottom: 8px;"">
                                <h1 style=""margin:0; font-family:'DM Serif Display', Georgia, serif; font-size: 22px; color:#3B2028; font-weight: 400;"">
                                    {heading}
                                </h1>
                                </td>
                            </tr>
                            <tr>
                                <td align=""center"" style=""padding: 8px 8px 28px; color:#7A6B6E; font-size: 14px; line-height: 1.6;"">
                                {description}
                                </td>
                            </tr>

                            <!-- Code box -->
                            <tr>
                                <td align=""center"" style=""padding-bottom: 24px;"">
                                <div class=""code"" style=""display:inline-block; background:linear-gradient(135deg, #FDEEF0, #F9E0DA); border: 1.5px dashed #F28695; border-radius: 14px; padding: 16px 32px; font-family:'Courier New', monospace; font-size: 34px; font-weight: 800; letter-spacing: 10px; color:#C0394A;"">
                                    {code}
                                </div>
                                </td>
                            </tr>

                            <tr>
                                <td align=""center"" style=""color:#9C8B8E; font-size: 12.5px; line-height: 1.6;"">
                                Este código expira em alguns minutos.<br/>
                                Se você não solicitou isso, pode ignorar este e-mail com segurança.
                                </td>
                            </tr>
                            </table>

                        </td>
                        </tr>

                        <!-- Footer -->
                        <tr>
                        <td align=""center"" style=""padding-top: 24px; color:#B8A6A9; font-size: 11.5px; line-height: 1.6;"">
                            © {DateTime.UtcNow.Year} Kureimo
                        </td>
                        </tr>

                    </table>
                    </td>
                </tr>
                </table>
            </body>
            </html>";
        }
    }
}
