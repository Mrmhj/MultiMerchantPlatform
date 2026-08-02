using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PerformanceService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MetricType = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CurrentValue = table.Column<double>(type: "float", nullable: false),
                    Threshold = table.Column<double>(type: "float", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoadTestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Concurrency = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalRequests = table.Column<long>(type: "bigint", nullable: false),
                    SuccessCount = table.Column<long>(type: "bigint", nullable: false),
                    FailCount = table.Column<long>(type: "bigint", nullable: false),
                    Qps = table.Column<double>(type: "float", nullable: false),
                    AvgLatencyMs = table.Column<double>(type: "float", nullable: false),
                    P50Ms = table.Column<double>(type: "float", nullable: false),
                    P95Ms = table.Column<double>(type: "float", nullable: false),
                    P99Ms = table.Column<double>(type: "float", nullable: false),
                    MaxLatencyMs = table.Column<double>(type: "float", nullable: false),
                    ErrorRatePercent = table.Column<double>(type: "float", nullable: false),
                    ReportPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadTestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoadTestTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Concurrency = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoadTestTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUp = table.Column<bool>(type: "bit", nullable: false),
                    ResponseMs = table.Column<double>(type: "float", nullable: false),
                    ManagedMemoryMb = table.Column<double>(type: "float", nullable: true),
                    WorkingSetMb = table.Column<double>(type: "float", nullable: true),
                    CpuPercent = table.Column<double>(type: "float", nullable: true),
                    Gen0GcCount = table.Column<long>(type: "bigint", nullable: true),
                    Gen1GcCount = table.Column<long>(type: "bigint", nullable: true),
                    Gen2GcCount = table.Column<long>(type: "bigint", nullable: true),
                    ThreadPoolAvailable = table.Column<int>(type: "int", nullable: true),
                    ThreadPoolMax = table.Column<int>(type: "int", nullable: true),
                    SourceJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRecords_ServiceName_Status",
                table: "AlertRecords",
                columns: new[] { "ServiceName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRecords_Status_CreatedAt",
                table: "AlertRecords",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoadTestRuns_CreatedAt",
                table: "LoadTestRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoadTestRuns_Status",
                table: "LoadTestRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LoadTestRuns_TaskId",
                table: "LoadTestRuns",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricsSnapshots_CapturedAt",
                table: "MetricsSnapshots",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MetricsSnapshots_ServiceName_CapturedAt",
                table: "MetricsSnapshots",
                columns: new[] { "ServiceName", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRecords");

            migrationBuilder.DropTable(
                name: "LoadTestRuns");

            migrationBuilder.DropTable(
                name: "LoadTestTasks");

            migrationBuilder.DropTable(
                name: "MetricsSnapshots");
        }
    }
}
