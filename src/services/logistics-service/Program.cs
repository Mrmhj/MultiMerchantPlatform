using System.Reflection;
using System.Text;
using BuildingBlocks.Security;
using LogisticsService.Domain.Entities;
using LogisticsService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger（仅开发环境启用，防生产泄露）
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    options.SwaggerDoc("v1", new() { Title = "Logistics Service API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "输入登录返回的 JWT（格式: Bearer {token}）",
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), [] },
    });
});

// JWT 认证
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("缺少 Jwt 配置节");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();

// 业务服务
builder.Services.AddLogisticsService(builder.Configuration);

var app = builder.Build();

// 自动迁移 + 种子数据（开发环境，生产用 dotnet ef database update）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogisticsDbContext>();
    await db.Database.MigrateAsync();

    // 物流公司种子（无数据时初始化，平台级基础数据）
    if (!await db.Companies.AnyAsync())
    {
        db.Companies.AddRange(
            new LogisticsCompany("SF", "顺丰速运", "https://www.sf-express.com/sf-service/web-service/waybill/query?billNo={no}"),
            new LogisticsCompany("YTO", "圆通速递", "https://www.yto.net.cn/tracking?waybillNo={no}"),
            new LogisticsCompany("ZTO", "中通快递", "https://www.zto.com/searchTracking?waybillNo={no}"),
            new LogisticsCompany("YUNDA", "韵达快递", "https://www.yundaex.com/query?no={no}"),
            new LogisticsCompany("JD", "京东物流", "https://www.jdl.com/query?waybillCode={no}"),
            new LogisticsCompany("EMS", "中国邮政EMS", "https://www.ems.com.cn/querying?mailNum={no}"));
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
