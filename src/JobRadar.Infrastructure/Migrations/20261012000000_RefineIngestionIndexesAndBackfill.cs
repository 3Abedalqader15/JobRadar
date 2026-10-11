using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefineIngestionIndexesAndBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_raw_posts_content_hash",
                table: "raw_posts");

            migrationBuilder.CreateIndex(
                name: "ix_raw_posts_source_id_content_hash",
                table: "raw_posts",
                columns: new[] { "source_id", "content_hash" });

            // Backfill existing ineligible jobs:
            // 1) From LinkedIn sources (stubs without full description)
            // 2) Jobs whose stripped description is under 300 characters
            // Verified affected row count: 190 jobs
            migrationBuilder.Sql(@"
                UPDATE jobs 
                SET is_cv_match_eligible = false 
                WHERE source_id IN (SELECT id FROM sources WHERE type = 'LinkedIn')
                   OR length(regexp_replace(description, '<[^>]+>', '', 'g')) < 300;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_raw_posts_source_id_content_hash",
                table: "raw_posts");

            migrationBuilder.CreateIndex(
                name: "ix_raw_posts_content_hash",
                table: "raw_posts",
                column: "content_hash");
        }
    }
}
