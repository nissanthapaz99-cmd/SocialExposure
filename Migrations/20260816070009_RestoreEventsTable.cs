using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialExposure.Migrations
{
    public partial class RestoreEventsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),

                    EventName = table.Column<string>(
                        type: "TEXT",
                        nullable: false),

                    ClientName = table.Column<string>(
                        type: "TEXT",
                        nullable: false),

                    ClientEmail = table.Column<string>(
                        type: "TEXT",
                        nullable: false),

                    Description = table.Column<string>(
                        type: "TEXT",
                        nullable: false),

                    StartDate = table.Column<DateTime>(
                        type: "TEXT",
                        nullable: false),

                    Deadline = table.Column<DateTime>(
                        type: "TEXT",
                        nullable: false),

                    Status = table.Column<string>(
                        type: "TEXT",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}