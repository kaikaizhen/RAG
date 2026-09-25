using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpaceID",
                table: "Questions",
                type: "int(11)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpaceID",
                table: "ChatHeader",
                type: "int(11)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_space_user",
                table: "Questions",
                columns: new[] { "SpaceID", "UserID" });

            migrationBuilder.CreateIndex(
                name: "ix_chatheader_space_user",
                table: "ChatHeader",
                columns: new[] { "SpaceID", "UserID" });

            migrationBuilder.AddForeignKey(
                name: "fk_chatheader_space",
                table: "ChatHeader",
                column: "SpaceID",
                principalTable: "Space",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_questions_space",
                table: "Questions",
                column: "SpaceID",
                principalTable: "Space",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chatheader_space",
                table: "ChatHeader");

            migrationBuilder.DropForeignKey(
                name: "fk_questions_space",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "ix_questions_space_user",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "ix_chatheader_space_user",
                table: "ChatHeader");

            migrationBuilder.DropColumn(
                name: "SpaceID",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "SpaceID",
                table: "ChatHeader");
        }
    }
}
