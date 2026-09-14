using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace JobRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "job_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_remote = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    salary_min = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    salary_max = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    salary_currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    job_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    experience_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    embedding_vector = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    posted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    job_source_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_postings", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_postings_job_sources_job_source_id",
                        column: x => x.job_source_id,
                        principalTable: "job_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "application_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_records_job_postings_job_posting_id",
                        column: x => x.job_posting_id,
                        principalTable: "job_postings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_records_applied_at",
                table: "application_records",
                column: "applied_at");

            migrationBuilder.CreateIndex(
                name: "ix_application_records_job_posting_id",
                table: "application_records",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_job_source_id",
                table: "job_postings",
                column: "job_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_posted_at",
                table: "job_postings",
                column: "posted_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_source_url",
                table: "job_postings",
                column: "source_url",
                unique: true,
                filter: "source_url IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_job_sources_name",
                table: "job_sources",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_records");

            migrationBuilder.DropTable(
                name: "job_postings");

            migrationBuilder.DropTable(
                name: "job_sources");
        }
    }
}
