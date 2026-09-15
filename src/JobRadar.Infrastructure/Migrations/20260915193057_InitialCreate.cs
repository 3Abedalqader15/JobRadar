using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace JobRadar.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "cv_templates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                preview_image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                is_ats_friendly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table => table.PrimaryKey("PK_cv_templates", x => x.id));

        migrationBuilder.CreateTable(
            name: "skills",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_skills", x => x.id));

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                preferred_job_titles = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                preferred_locations = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                preferred_skills = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                experience_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                telegram_chat_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_users", x => x.id));

        migrationBuilder.CreateTable(
            name: "cvs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                template_id = table.Column<Guid>(type: "uuid", nullable: true),
                content_json = table.Column<string>(type: "jsonb", nullable: false),
                generated_pdf_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cvs", x => x.id);
                table.ForeignKey(
                    name: "FK_cvs_cv_templates_template_id",
                    column: x => x.template_id,
                    principalTable: "cv_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_cvs_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "sources",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                added_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                fetch_interval_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                last_fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sources", x => x.id);
                table.ForeignKey(
                    name: "FK_sources_users_added_by_user_id",
                    column: x => x.added_by_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "raw_posts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: false),
                raw_content = table.Column<string>(type: "text", nullable: false),
                raw_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processing_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_raw_posts", x => x.id);
                table.ForeignKey(
                    name: "FK_raw_posts_sources_source_id",
                    column: x => x.source_id,
                    principalTable: "sources",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "jobs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                raw_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                source_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                is_remote = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                employment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                experience_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                salary_min = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                salary_max = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                salary_currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                external_apply_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                posted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                views_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                applicants_click_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_jobs", x => x.id);
                table.ForeignKey(
                    name: "FK_jobs_raw_posts_raw_post_id",
                    column: x => x.raw_post_id,
                    principalTable: "raw_posts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_jobs_sources_source_id",
                    column: x => x.source_id,
                    principalTable: "sources",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "cv_job_match_analyses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                cv_id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                ats_score = table.Column<int>(type: "integer", nullable: false),
                missing_keywords = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'::text[]"),
                suggestions = table.Column<string>(type: "text", nullable: false),
                analyzed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cv_job_match_analyses", x => x.id);
                table.ForeignKey(
                    name: "FK_cv_job_match_analyses_cvs_cv_id",
                    column: x => x.cv_id,
                    principalTable: "cvs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_cv_job_match_analyses_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "job_skill_maps",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_job_skill_maps", x => new { x.job_id, x.skill_id });
                table.ForeignKey(
                    name: "FK_job_skill_maps_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_job_skill_maps_skills_skill_id",
                    column: x => x.skill_id,
                    principalTable: "skills",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: true),
                channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notifications", x => x.id);
                table.ForeignKey(
                    name: "FK_notifications_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_notifications_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_job_applications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                notes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_job_applications", x => x.id);
                table.ForeignKey(
                    name: "FK_user_job_applications_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_job_applications_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_saved_jobs",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_saved_jobs", x => new { x.user_id, x.job_id });
                table.ForeignKey(
                    name: "FK_user_saved_jobs_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_saved_jobs_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_cv_job_match_analyses_analyzed_at",
            table: "cv_job_match_analyses",
            column: "analyzed_at");

        migrationBuilder.CreateIndex(
            name: "ix_cv_job_match_analyses_cv_job",
            table: "cv_job_match_analyses",
            columns: ["cv_id", "job_id"]);

        migrationBuilder.CreateIndex(
            name: "IX_cv_job_match_analyses_job_id",
            table: "cv_job_match_analyses",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "ix_cv_templates_name",
            table: "cv_templates",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_cvs_template_id",
            table: "cvs",
            column: "template_id");

        migrationBuilder.CreateIndex(
            name: "ix_cvs_user_id",
            table: "cvs",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_job_skill_maps_skill_id",
            table: "job_skill_maps",
            column: "skill_id");

        migrationBuilder.CreateIndex(
            name: "ix_jobs_employment_type",
            table: "jobs",
            column: "employment_type");

        migrationBuilder.CreateIndex(
            name: "ix_jobs_is_active",
            table: "jobs",
            column: "is_active");

        migrationBuilder.CreateIndex(
            name: "ix_jobs_posted_at",
            table: "jobs",
            column: "posted_at");

        migrationBuilder.CreateIndex(
            name: "IX_jobs_raw_post_id",
            table: "jobs",
            column: "raw_post_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_jobs_source_id",
            table: "jobs",
            column: "source_id");

        migrationBuilder.CreateIndex(
            name: "ix_notifications_created_at",
            table: "notifications",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_notifications_job_id",
            table: "notifications",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "ix_notifications_status",
            table: "notifications",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_notifications_user_id",
            table: "notifications",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_raw_posts_fetched_at",
            table: "raw_posts",
            column: "fetched_at");

        migrationBuilder.CreateIndex(
            name: "ix_raw_posts_processing_status",
            table: "raw_posts",
            column: "processing_status");

        migrationBuilder.CreateIndex(
            name: "ix_raw_posts_source_id",
            table: "raw_posts",
            column: "source_id");

        migrationBuilder.CreateIndex(
            name: "ix_skills_name",
            table: "skills",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_skills_slug",
            table: "skills",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_sources_added_by_user_id",
            table: "sources",
            column: "added_by_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_sources_status",
            table: "sources",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_sources_url",
            table: "sources",
            column: "url",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_job_applications_applied_at",
            table: "user_job_applications",
            column: "applied_at");

        migrationBuilder.CreateIndex(
            name: "IX_user_job_applications_job_id",
            table: "user_job_applications",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_job_applications_user_job",
            table: "user_job_applications",
            columns: ["user_id", "job_id"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_saved_jobs_job_id",
            table: "user_saved_jobs",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_saved_jobs_saved_at",
            table: "user_saved_jobs",
            column: "saved_at");

        migrationBuilder.CreateIndex(
            name: "ix_users_email",
            table: "users",
            column: "email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_telegram_chat_id",
            table: "users",
            column: "telegram_chat_id",
            unique: true,
            filter: "telegram_chat_id IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cv_job_match_analyses");

        migrationBuilder.DropTable(
            name: "job_skill_maps");

        migrationBuilder.DropTable(
            name: "notifications");

        migrationBuilder.DropTable(
            name: "user_job_applications");

        migrationBuilder.DropTable(
            name: "user_saved_jobs");

        migrationBuilder.DropTable(
            name: "cvs");

        migrationBuilder.DropTable(
            name: "skills");

        migrationBuilder.DropTable(
            name: "jobs");

        migrationBuilder.DropTable(
            name: "cv_templates");

        migrationBuilder.DropTable(
            name: "raw_posts");

        migrationBuilder.DropTable(
            name: "sources");

        migrationBuilder.DropTable(
            name: "users");
    }
}
