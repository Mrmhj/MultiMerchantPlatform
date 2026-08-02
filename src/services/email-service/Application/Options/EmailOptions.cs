namespace EmailService.Application.Options;

/// <summary>
/// 邮件服务配置（SMTP + 发送策略）。
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP 服务器</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP 端口</summary>
    public int Port { get; set; } = 25;

    /// <summary>是否启用 SSL</summary>
    public bool UseSsl { get; set; }

    /// <summary>SMTP 用户名（可选）</summary>
    public string? Username { get; set; }

    /// <summary>SMTP 密码（可选）</summary>
    public string? Password { get; set; }

    /// <summary>默认发件人地址</summary>
    public string DefaultFrom { get; set; } = "noreply@multimerchant.local";

    /// <summary>默认发件人名称</summary>
    public string DefaultFromName { get; set; } = "多商户平台";

    /// <summary>
    /// 开发模式：不真实发送邮件，仅记录日志（本地无 SMTP 服务器时使用）。
    /// 生产环境必须设为 false 并配置真实 SMTP。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>重试指数退避基准间隔（秒）</summary>
    public int RetryBaseIntervalSeconds { get; set; } = 60;

    /// <summary>重试最大间隔（秒）</summary>
    public int MaxRetryDelaySeconds { get; set; } = 600;

    /// <summary>后台重试轮询间隔（秒）</summary>
    public int PollIntervalSeconds { get; set; } = 30;
}
