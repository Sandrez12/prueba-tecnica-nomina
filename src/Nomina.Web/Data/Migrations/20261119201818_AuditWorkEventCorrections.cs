using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nomina.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditWorkEventCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkEventCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkEventId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PreviousKind = table.Column<int>(type: "int", nullable: false),
                    NewKind = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkEventCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkEventCorrections_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkEventCorrections_WorkEvents_WorkEventId",
                        column: x => x.WorkEventId,
                        principalTable: "WorkEvents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkEventCorrections_ActorId",
                table: "WorkEventCorrections",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkEventCorrections_WorkEventId",
                table: "WorkEventCorrections",
                column: "WorkEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkEventCorrections");
        }
    }
}
