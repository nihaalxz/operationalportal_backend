using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TexasOperationalPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationalAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    EntityDocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedToUserId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Assigned"),
                    LastModifiedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalAssignments_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAssignments_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAssignments_Users_LastModifiedByUserId",
                        column: x => x.LastModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalAssignmentHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PerformedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldAssignedUserId = table.Column<long>(type: "bigint", nullable: true),
                    NewAssignedUserId = table.Column<long>(type: "bigint", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAssignmentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalAssignmentHistories_OperationalAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "OperationalAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationalAssignmentHistories_Users_NewAssignedUserId",
                        column: x => x.NewAssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAssignmentHistories_Users_OldAssignedUserId",
                        column: x => x.OldAssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalAssignmentHistories_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignmentHistories_AssignmentId",
                table: "OperationalAssignmentHistories",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignmentHistories_NewAssignedUserId",
                table: "OperationalAssignmentHistories",
                column: "NewAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignmentHistories_OldAssignedUserId",
                table: "OperationalAssignmentHistories",
                column: "OldAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignmentHistories_PerformedByUserId",
                table: "OperationalAssignmentHistories",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignments_AssignedToUserId",
                table: "OperationalAssignments",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignments_CompletedByUserId",
                table: "OperationalAssignments",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignments_EntityType_EntityId",
                table: "OperationalAssignments",
                columns: new[] { "EntityType", "EntityId" },
                unique: true,
                filter: "[Status] = 'Assigned'");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAssignments_LastModifiedByUserId",
                table: "OperationalAssignments",
                column: "LastModifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalAssignmentHistories");

            migrationBuilder.DropTable(
                name: "OperationalAssignments");
        }
    }
}
