using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCvMatchHardeningAndIngestionQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<string>(
                name: "ai_missing_keyword_evidence",
                table: "user_job_applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_score_breakdown",
                table: "user_job_applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "analysis_prompt_version",
                table: "user_job_applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "suspicious_instructions_detected",
                table: "user_job_applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "raw_posts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extraction_prompt_version",
                table: "raw_posts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "raw_posts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apply_email",
                table: "jobs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apply_phone",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extraction_prompt_version",
                table: "jobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_cv_match_eligible",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_raw_posts_content_hash",
                table: "raw_posts",
                column: "content_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_raw_posts_content_hash",
                table: "raw_posts");

            migrationBuilder.DropColumn(
                name: "ai_missing_keyword_evidence",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "ai_score_breakdown",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "analysis_prompt_version",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "suspicious_instructions_detected",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "raw_posts");

            migrationBuilder.DropColumn(
                name: "extraction_prompt_version",
                table: "raw_posts");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "raw_posts");

            migrationBuilder.DropColumn(
                name: "apply_email",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "apply_phone",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "extraction_prompt_version",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "is_cv_match_eligible",
                table: "jobs");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
