using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Migrations
{
    /// <inheritdoc />
    public partial class RequireSpaceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contract 前的保護：仍有未 backfill 的資料時中止，避免 NULL 被轉為 0 或破壞 FK
            migrationBuilder.Sql(@"
BEGIN NOT ATOMIC
    IF (SELECT COUNT(*) FROM `ChatHeader` WHERE `SpaceID` IS NULL) + (SELECT COUNT(*) FROM `Questions` WHERE `SpaceID` IS NULL) > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'RequireSpaceScope: backfill SpaceID before applying this migration';
    END IF;
END");

            migrationBuilder.AlterColumn<int>(
                name: "SpaceID",
                table: "Questions",
                type: "int(11)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int(11)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SpaceID",
                table: "ChatHeader",
                type: "int(11)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int(11)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SpaceID",
                table: "Questions",
                type: "int(11)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int(11)");

            migrationBuilder.AlterColumn<int>(
                name: "SpaceID",
                table: "ChatHeader",
                type: "int(11)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int(11)");
        }
    }
}
