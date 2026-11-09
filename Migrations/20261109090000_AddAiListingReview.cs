using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZuMarket.Migrations;

public partial class AddAiListingReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(name: "UniversityId", table: "AspNetUsers", type: "nvarchar(40)", maxLength: 40, nullable: true, oldClrType: typeof(string), oldType: "nvarchar(40)", oldMaxLength: 40);
        migrationBuilder.AlterColumn<string>(name: "College", table: "AspNetUsers", type: "nvarchar(120)", maxLength: 120, nullable: true, oldClrType: typeof(string), oldType: "nvarchar(120)", oldMaxLength: 120);

        migrationBuilder.DropIndex(name: "IX_AspNetUsers_UniversityId", table: "AspNetUsers");
        migrationBuilder.CreateIndex(name: "IX_AspNetUsers_UniversityId", table: "AspNetUsers", column: "UniversityId", unique: true, filter: "[UniversityId] IS NOT NULL");

        migrationBuilder.AddColumn<int>(
            name: "ReviewState",
            table: "Listings",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateTable(
            name: "AiListingReviews",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ListingId = table.Column<int>(type: "int", nullable: false),
                Decision = table.Column<int>(type: "int", nullable: false),
                Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                RuleChecksJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                FinalDecisionWasAi = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiListingReviews", x => x.Id);
                table.ForeignKey("FK_AiListingReviews_Listings_ListingId", x => x.ListingId, "Listings", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AiListingReviews_ListingId_CreatedAtUtc",
            table: "AiListingReviews",
            columns: new[] { "ListingId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AiListingReviews");
        migrationBuilder.DropColumn(name: "ReviewState", table: "Listings");
        migrationBuilder.DropIndex(name: "IX_AspNetUsers_UniversityId", table: "AspNetUsers");
        migrationBuilder.CreateIndex(name: "IX_AspNetUsers_UniversityId", table: "AspNetUsers", column: "UniversityId", unique: true);
        migrationBuilder.AlterColumn<string>(name: "UniversityId", table: "AspNetUsers", type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "ADMIN", oldClrType: typeof(string), oldType: "nvarchar(40)", oldMaxLength: 40);
        migrationBuilder.AlterColumn<string>(name: "College", table: "AspNetUsers", type: "nvarchar(120)", maxLength: 120, nullable: false, defaultValue: "Administration", oldClrType: typeof(string), oldType: "nvarchar(120)", oldMaxLength: 120);
    }
}
