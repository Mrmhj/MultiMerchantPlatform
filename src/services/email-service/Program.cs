using EmailService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 注册 email-service 核心服务（配置 / DbContext / SMTP / 发送 / 重试）
builder.Services.AddEmailService(builder.Configuration);

var app = builder.Build();

// 启动时自动迁移数据库（开发环境便利；生产应使用显式迁移）
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
        await db.Database.MigrateAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
