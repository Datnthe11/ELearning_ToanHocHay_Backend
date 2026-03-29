using System.Net;
using System.Net.Mail;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendConfirmEmailAsync(string toEmail, string fullName, string confirmLink)
        {
            try
            {
                var subject = "Xác nhận đăng ký tài khoản";
                var body = $@"
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Bạn vừa đăng ký tài khoản trên hệ thống <b>E-Learning Toán Học Hay</b>.</p>
            <p>Vui lòng nhấn vào nút bên dưới để xác nhận email:</p>
            <p>
                <a href='{confirmLink}'
                   style='display:inline-block;
                          padding:10px 20px;
                          background:#2563eb;
                          color:#ffffff;
                          text-decoration:none;
                          border-radius:6px;'>
                   Xác nhận email
                </a>
            </p>
            <p>Link có hiệu lực trong <b>24 giờ</b>.</p>
            <p>Nếu bạn không thực hiện đăng ký, vui lòng bỏ qua email này.</p>
            <br/>
            <p>Trân trọng,<br/>E-Learning Team</p>
        ";

                await SendEmailAsync(toEmail, subject, body);
            }
            catch (SmtpException smtpEx)
            {
                Console.WriteLine($"SMTP Error: {smtpEx.StatusCode} - {smtpEx.Message}");
                throw new Exception($"Lỗi gửi email: {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Error: {ex.Message}");
                throw;
            }
        }

        public async Task SendTabSwitchNotificationAsync(
            string toEmail,
            string parentName,
            string studentName,
            string exerciseName,
            DateTime switchedAt,
            int switchCount)
        {
            try
            {
                var subject = $"⚠️ Cảnh báo: {studentName} đã chuyển tab trong lúc làm bài!";
                var timeStr = switchedAt.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy");
                var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden;'>
                    <div style='background: linear-gradient(135deg, #ef4444, #dc2626); padding: 24px; text-align: center;'>
                        <h1 style='color: white; margin: 0; font-size: 22px;'>⚠️ Cảnh Báo Gian Lận Bài Thi</h1>
                        <p style='color: #fca5a5; margin: 4px 0 0 0;'>Hệ thống E-Learning Toán Học Hay</p>
                    </div>
                    <div style='padding: 28px;'>
                        <p>Kính gửi Phụ huynh <strong>{parentName}</strong>,</p>
                        <p>Hệ thống đã phát hiện hành vi <strong>chuyển tab trình duyệt</strong> trong lúc làm bài kiểm tra:</p>

                        <table style='width: 100%; border-collapse: collapse; margin: 16px 0;'>
                            <tr style='background: #f8fafc;'>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0; font-weight: bold; width: 40%;'>👨‍🎓 Học sinh</td>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0;'>{studentName}</td>
                            </tr>
                            <tr>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0; font-weight: bold;'>📝 Bài thi</td>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0;'>{exerciseName}</td>
                            </tr>
                            <tr style='background: #f8fafc;'>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0; font-weight: bold;'>🕐 Thời điểm</td>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0;'>{timeStr}</td>
                            </tr>
                            <tr>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0; font-weight: bold;'>🔢 Số lần chuyển tab</td>
                                <td style='padding: 10px 14px; border: 1px solid #e2e8f0; color: #dc2626; font-weight: bold;'>{switchCount} lần</td>
                            </tr>
                        </table>

                        <div style='background: #fff7ed; border-left: 4px solid #f97316; padding: 14px 18px; border-radius: 4px; margin-top: 16px;'>
                            <p style='margin: 0; color: #92400e;'>Học sinh đã được cảnh báo trực tiếp trên màn hình. Vui lòng nhắc nhở con em về tính trung thực trong học tập.</p>
                        </div>
                    </div>
                    <div style='background: #f1f5f9; padding: 16px; text-align: center;'>
                        <p style='margin: 0; color: #64748b; font-size: 13px;'>Email này được gửi tự động từ hệ thống <b>E-Learning Toán Học Hay</b>.</p>
                    </div>
                </div>";

                await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TabSwitch Email Error] {ex.Message}");
                throw;
            }
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var message = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var smtp = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
            {
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                EnableSsl = _emailSettings.EnableSsl
            };

            await smtp.SendMailAsync(message);
        }
    }
}
