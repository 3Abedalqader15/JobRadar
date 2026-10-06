using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20261006_AddEnhancedApplicationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_analysis_status",
                table: "user_job_applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "ai_analysis_summary",
                table: "user_job_applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ai_match_score",
                table: "user_job_applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "ai_missing_keywords",
                table: "user_job_applications",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applicant_email",
                table: "user_job_applications",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "applicant_full_name",
                table: "user_job_applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "applicant_phone",
                table: "user_job_applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "cv_file_path",
                table: "user_job_applications",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cv_original_file_name",
                table: "user_job_applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_application_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    question_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    options_json = table.Column<string>(type: "text", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_application_questions_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_application_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_job_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_application_answers_job_application_questions_question_~",
                        column: x => x.question_id,
                        principalTable: "job_application_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_application_answers_user_job_applications_user_job_appl~",
                        column: x => x.user_job_application_id,
                        principalTable: "user_job_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_job_applications_ai_analysis_status",
                table: "user_job_applications",
                column: "ai_analysis_status");

            migrationBuilder.CreateIndex(
                name: "ix_user_job_applications_ai_match_score",
                table: "user_job_applications",
                column: "ai_match_score");

            migrationBuilder.CreateIndex(
                name: "ix_job_application_answers_app_id",
                table: "job_application_answers",
                column: "user_job_application_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_application_answers_app_question",
                table: "job_application_answers",
                columns: new[] { "user_job_application_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_application_answers_question_id",
                table: "job_application_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_application_questions_job_id",
                table: "job_application_questions",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_application_questions_job_order",
                table: "job_application_questions",
                columns: new[] { "job_id", "display_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_application_answers");

            migrationBuilder.DropTable(
                name: "job_application_questions");

            migrationBuilder.DropIndex(
                name: "ix_user_job_applications_ai_analysis_status",
                table: "user_job_applications");

            migrationBuilder.DropIndex(
                name: "ix_user_job_applications_ai_match_score",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "ai_analysis_status",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "ai_analysis_summary",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "ai_match_score",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "ai_missing_keywords",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "applicant_email",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "applicant_full_name",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "applicant_phone",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "cv_file_path",
                table: "user_job_applications");

            migrationBuilder.DropColumn(
                name: "cv_original_file_name",
                table: "user_job_applications");
        }
    }
}
