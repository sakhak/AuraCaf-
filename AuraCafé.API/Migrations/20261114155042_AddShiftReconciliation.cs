using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCafé.API.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_CollectedByStaffId",
                table: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "OpeningCashCents",
                table: "WorkShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShiftReconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkShiftId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedByStaffId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpeningCashCents = table.Column<int>(type: "INTEGER", nullable: false),
                    RevenueCents = table.Column<long>(type: "INTEGER", nullable: false),
                    PaidOrders = table.Column<int>(type: "INTEGER", nullable: false),
                    CashReceivedCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ChangeCents = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpectedCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    CountedCashCents = table.Column<long>(type: "INTEGER", nullable: false),
                    DifferenceCents = table.Column<long>(type: "INTEGER", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftReconciliations", x => x.Id);
                    table.CheckConstraint("CK_Reconciliation_Arithmetic", "ExpectedCashCents = OpeningCashCents + RevenueCents AND DifferenceCents = CountedCashCents - ExpectedCashCents AND CashReceivedCents - ChangeCents = RevenueCents");
                    table.CheckConstraint("CK_Reconciliation_Counts", "OpeningCashCents >= 0 AND CountedCashCents >= 0 AND RevenueCents >= 0 AND PaidOrders >= 0");
                    table.ForeignKey(
                        name: "FK_ShiftReconciliations_Staff_SubmittedByStaffId",
                        column: x => x.SubmittedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftReconciliations_WorkShifts_WorkShiftId",
                        column: x => x.WorkShiftId,
                        principalTable: "WorkShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedByStaffId_PaidAtUtc",
                table: "Payments",
                columns: new[] { "CollectedByStaffId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidAtUtc",
                table: "Payments",
                column: "PaidAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReconciliations_SubmittedByStaffId",
                table: "ShiftReconciliations",
                column: "SubmittedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftReconciliations_WorkShiftId",
                table: "ShiftReconciliations",
                column: "WorkShiftId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CollectedByStaffId_PaidAtUtc",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaidAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OpeningCashCents",
                table: "WorkShifts");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CollectedByStaffId",
                table: "Payments",
                column: "CollectedByStaffId");
        }
    }
}
