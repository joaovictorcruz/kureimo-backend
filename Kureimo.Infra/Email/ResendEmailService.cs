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

        public async Task SendPasswordResetEmailAsync(
            string toEmail,
            string username,
            string token,
            CancellationToken ct = default)
        {
            var resetLink = $"{_frontendUrl}/reset-password?token={token}";

            var message = new EmailMessage();
            message.From = "noreply@kureimo.com";
            message.To.Add(toEmail);
            message.Subject = "Redefinição de senha — Kureimo";
            message.HtmlBody = $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                  <title>Redefinir senha — kureimo</title>
                </head>
                <body style="margin:0; padding:0; background-color:#FDF5F0; font-family:'Helvetica Neue', Helvetica, Arial, sans-serif;">

                  <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#FDF5F0; padding: 40px 16px;">
                    <tr>
                      <td align="center">

                        <table width="100%" cellpadding="0" cellspacing="0"
                               style="max-width:480px; background:#FFFFFF; border-radius:16px; border:1.5px solid rgba(242,134,149,0.2); box-shadow:0 8px 32px rgba(59,32,40,0.08); overflow:hidden;">

                          <!-- HEADER -->
                          <tr>
                            <td style="background:linear-gradient(135deg, #FDE8EC 0%, #F9EDE8 100%); padding:36px 40px 28px; text-align:center;">
          
                              <div style="width:56px; height:56px; background:linear-gradient(135deg,#F28695,#d96475); border-radius:50%; margin:0 auto 12px; display:flex; align-items:center; justify-content:center;">
                                <span style="font-size:22px; font-weight:900; color:#ffffff;">k</span>
                              </div>

                              <div style="font-size:24px; color:#d96475;">
                                kureimo
                              </div>

                              <h1 style="margin:16px 0 0; font-size:22px; color:#3B2028;">
                                Redefinição de senha
                              </h1>
                            </td>
                          </tr>

                          <!-- BODY -->
                          <tr>
                            <td style="padding:32px 40px;">

                              <p style="margin:0 0 16px; font-size:15px; color:#3B2028; font-weight:600;">
                                Olá, {username}!
                              </p>

                              <p style="margin:0 0 24px; font-size:14px; color:#6B5560;">
                                Recebemos uma solicitação para redefinir a senha da sua conta no kureimo.
                              </p>

                              <table cellpadding="0" cellspacing="0" style="margin:0 auto 28px;">
                                <tr>
                                  <td align="center" style="border-radius:10px; background:#F28695;">
                                    <a href="{resetLink}"
                                       target="_blank"
                                       style="display:inline-block; padding:14px 32px; font-size:14px; font-weight:800; color:#ffffff; text-decoration:none;">
                                      Redefinir minha senha
                                    </a>
                                  </td>
                                </tr>
                              </table>

                              <p style="font-size:12px; text-align:center; color:#9B8A90;">
                                Ou copie e cole:<br/>
                                <a href="{resetLink}" style="color:#d96475;">{resetLink}</a>
                              </p>

                              <p style="margin-top:24px; font-size:12px; color:#6B5560;">
                                Este link expira em <strong style="color:#d96475;">15 minutos</strong>.
                              </p>

                              <p style="font-size:12px; color:#9B8A90; text-align:center;">
                                Se você não solicitou, ignore este e-mail.
                              </p>

                            </td>
                          </tr>

                          <!-- FOOTER -->
                          <tr>
                            <td style="background:#FDF0F2; padding:20px; text-align:center;">
                              <p style="margin:4px 0 0; font-size:11px; color:#9B8A90;">
                                Este e-mail foi enviado automaticamente.
                              </p>
                            </td>
                          </tr>

                        </table>

                      </td>
                    </tr>
                  </table>

                </body>
                </html>
                """;

            await _resend.EmailSendAsync(message);

            _logger.LogInformation("Email de reset enviado para: {Email}", toEmail);
        }
    }
}
