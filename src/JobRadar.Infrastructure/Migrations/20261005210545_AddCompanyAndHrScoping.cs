using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAndHrScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_company_id",
                table: "users",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_company_id",
                table: "jobs",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_companies_slug",
                table: "companies",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_jobs_companies_company_id",
                table: "jobs",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_companies_company_id",
                table: "users",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_jobs_companies_company_id",
                table: "jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_users_companies_company_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropIndex(
                name: "ix_users_company_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_jobs_company_id",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "jobs");
        }
    }
}
