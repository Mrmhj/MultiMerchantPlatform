using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlacklistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scene = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Dimension = table.Column<int>(type: "int", nullable: false),
                    DimensionKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OccurredCount = table.Column<int>(type: "int", nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "SYSUTCDATETIME()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scene = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scene = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Dimension = table.Column<int>(type: "int", nullable: false),
                    WindowSeconds = table.Column<int>(type: "int", nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<int>(type: "int", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "SYSUTCDATETIME()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistEntries_Enabled_ExpiresAt",
                table: "BlacklistEntries",
                columns: new[] { "Enabled", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistEntries_TargetType_TargetValue_MerchantId",
                table: "BlacklistEntries",
                columns: new[] { "TargetType", "TargetValue", "MerchantId" },
                unique: true,
                filter: "[MerchantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RiskCases_MerchantId_Status",
                table: "RiskCases",
                columns: new[] { "MerchantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskCases_RuleId_DimensionKey_Status",
                table: "RiskCases",
                columns: new[] { "RuleId", "DimensionKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskCases_Status_CreatedAt",
                table: "RiskCases",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskCases_UserId_Status",
                table: "RiskCases",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskEvents_OccurredAt",
                table: "RiskEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_RiskEvents_Scene_DeviceId_OccurredAt",
                table: "RiskEvents",
                columns: new[] { "Scene", "DeviceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskEvents_Scene_Ip_OccurredAt",
                table: "RiskEvents",
                columns: new[] { "Scene", "Ip", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskEvents_Scene_MerchantId_OccurredAt",
                table: "RiskEvents",
                columns: new[] { "Scene", "MerchantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskEvents_Scene_UserId_OccurredAt",
                table: "RiskEvents",
                columns: new[] { "Scene", "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskRules_MerchantId_Enabled",
                table: "RiskRules",
                columns: new[] { "MerchantId", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskRules_Scene_Enabled",
                table: "RiskRules",
                columns: new[] { "Scene", "Enabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlacklistEntries");

            migrationBuilder.DropTable(
                name: "RiskCases");

            migrationBuilder.DropTable(
                name: "RiskEvents");

            migrationBuilder.DropTable(
                name: "RiskRules");
        }
    }
}
