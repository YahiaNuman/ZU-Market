using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuMarket.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceLanguage",
                table: "Listings",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedDescription",
                table: "Listings",
                type: "nvarchar(max)",
                maxLength: 6000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslatedTitle",
                table: "Listings",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TranslationAttempts",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TranslationNextAttemptUtc",
                table: "Listings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ListingReportSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    SuggestedAction = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ReportCount = table.Column<int>(type: "int", nullable: false),
                    LatestReportId = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingReportSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingReportSummaries_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingReportSummaries_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingReportSummaries_CreatedById",
                table: "ListingReportSummaries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ListingReportSummaries_ListingId_CreatedAtUtc",
                table: "ListingReportSummaries",
                columns: new[] { "ListingId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingReportSummaries");

            migrationBuilder.DropColumn(
                name: "SourceLanguage",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TranslatedDescription",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TranslatedTitle",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TranslationAttempts",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "TranslationNextAttemptUtc",
                table: "Listings");
        }
    }
}
