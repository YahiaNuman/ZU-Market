using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZuMarket.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCategoriesWithFaculties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Listings have to leave the old categories before those rows go; Charity listings stay free.
            migrationBuilder.Sql("UPDATE Listings SET IsFree = CASE WHEN CategoryId = 3 THEN 1 ELSE IsFree END, CategoryId = 4 WHERE CategoryId IN (1, 2, 3);");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "SortOrder",
                value: 15);

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Key", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { 5, "FacultyIT", "it", 1 },
                    { 6, "FacultyNursing", "nursing", 2 },
                    { 7, "FacultyMedical", "medical", 3 },
                    { 8, "FacultyLiterature", "literature", 4 },
                    { 9, "FacultyDentistry", "dentistry", 5 },
                    { 10, "FacultyPharmacy", "pharmacy", 6 },
                    { 11, "FacultyEngineering", "engineering", 7 },
                    { 12, "FacultyScience", "science", 8 },
                    { 13, "FacultyBusiness", "business", 9 },
                    { 14, "FacultyLaw", "law", 10 },
                    { 15, "FacultyEducation", "education", 11 },
                    { 16, "FacultyMedia", "media", 12 },
                    { 17, "FacultyDesign", "design", 13 },
                    { 18, "FacultySharia", "sharia", 14 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Listings SET CategoryId = 4 WHERE CategoryId BETWEEN 5 AND 18;");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "SortOrder",
                value: 4);

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Key", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Books", "books", 1 },
                    { 2, "Computer", "computer", 2 },
                    { 3, "Charity", "charity", 3 }
                });
        }
    }
}
