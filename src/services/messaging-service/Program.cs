using MessagingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 注册 messaging-service 核心服务（配置 / DbContext / 发布器 / 分发器）
builder.Services.AddMessagingService(builder.Configuration);

var app = builder.Build();

// 启动时自动迁移数据库（开发环境便利；生产应使用显式迁移）
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
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
