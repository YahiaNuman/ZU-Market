using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ZuMarket.Services;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "ZU Market";
}

public interface IAppEmailSender
{
    bool IsConfigured { get; }
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public sealed class SmtpEmailSender : IAppEmailSender
{
    private const int TimeoutMilliseconds = 15000;
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.FromAddress);

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return false;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress!, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(_options.Host!, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = TimeoutMilliseconds
            };
            if (!string.IsNullOrWhiteSpace(_options.UserName))
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning("Email delivery failed. Error type: {ErrorType}; SMTP status: {SmtpStatus}", exception.GetType().Name, (exception as SmtpException)?.StatusCode);
            return false;
        }
    }
}
