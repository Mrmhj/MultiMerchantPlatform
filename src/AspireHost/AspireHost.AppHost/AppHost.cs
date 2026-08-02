using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// ── API 网关 ──
var apiGateway = builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithEndpoint(name: "http", port: 8000, targetPort: 8080);

// ── 基础设施服务 ──
// Phase 0: 自封装消息队列 (port 8010)
var messagingService = builder.AddProject<Projects.MessagingService>("messaging-service")
    .WithEndpoint(name: "http", port: 8010, targetPort: 8080);

// Phase 0: 自封装日志管理 (port 8011)
var loggingService = builder.AddProject<Projects.LoggingService>("logging-service")
    .WithEndpoint(name: "http", port: 8011, targetPort: 8080);

// Phase 0: 自封装邮件服务 (port 8015)
var emailService = builder.AddProject<Projects.EmailService>("email-service")
    .WithEndpoint(name: "http", port: 8015, targetPort: 8080);

// Phase 3: 压测 + 内存监控 (port 8017)
var performanceService = builder.AddProject<Projects.PerformanceService>("performance-service")
    .WithEndpoint(name: "http", port: 8017, targetPort: 8080);

// Phase 3: 风控/反刷单 (port 8018)
var riskService = builder.AddProject<Projects.RiskService>("risk-service")
    .WithEndpoint(name: "http", port: 8018, targetPort: 8080);

// 网关转发基础设施服务接口
apiGateway.WithReference(messagingService);
apiGateway.WithReference(loggingService);
apiGateway.WithReference(emailService);
apiGateway.WithReference(performanceService);
apiGateway.WithReference(riskService);

// ── 核心业务服务 (Phase 1) ──
// - identity-service (port 8001)
// - merchant-service (port 8002)
// - product-service (port 8003)
// - order-service (port 8004)
// - pay-service (port 8005)
// - stock-service (port 8006)

builder.Build().Run();
