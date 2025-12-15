using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public static class EmailService
{
    public static async Task<string> SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        string[] attachmentPaths = null)   // 👈 thêm tham số này
    {
        try
        {
            // Đọc cấu hình SMTP từ App.config
            string smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            int smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            string smtpUser = ConfigurationManager.AppSettings["SmtpUser"];
            string smtpPass = ConfigurationManager.AppSettings["SmtpPass"];
            bool enableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"]);
            string mailFrom = ConfigurationManager.AppSettings["MailFrom"];

            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                client.EnableSsl = enableSsl;

                using (var message = new MailMessage(mailFrom, toEmail, subject, body))
                {
                    message.IsBodyHtml = true;

                    // 👇 Thêm file đính kèm nếu có
                    if (attachmentPaths != null)
                    {
                        foreach (var path in attachmentPaths)
                        {
                            if (File.Exists(path))
                            {
                                message.Attachments.Add(new Attachment(path));
                            }
                        }
                    }

                    await client.SendMailAsync(message);
                }
            }

            return null; // null = gửi thành công
        }
        catch (Exception ex)
        {
            return ex.Message; // trả lỗi nếu có
        }
    }
}
