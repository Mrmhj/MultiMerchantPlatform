using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// ── API 网关 ──
var apiGateway = builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithEndpoint(name: "http", port: 8000, targetPort: 8080);

// ── 基础设施服务 ──
// Phase 0 将逐步添加:
// - messaging-service (port 8010)
// - logging-service (port 8011)
// - email-service (port 8015)

// ── 核心业务服务 (Phase 1) ──
// - identity-service (port 8001)
// - merchant-service (port 8002)
// - product-service (port 8003)
// - order-service (port 8004)
// - pay-service (port 8005)
// - stock-service (port 8006)

builder.Build().Run();
