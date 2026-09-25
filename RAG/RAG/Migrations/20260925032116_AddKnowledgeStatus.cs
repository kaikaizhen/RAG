using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAG.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Questions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Verified",
                collation: "latin1_swedish_ci")
                .Annotation("MySql:CharSet", "latin1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_status",
                table: "Questions",
                sql: "`Status` IN ('Verified','Stale','Inferred','Superseded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_status",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Questions");
        }
    }
}
