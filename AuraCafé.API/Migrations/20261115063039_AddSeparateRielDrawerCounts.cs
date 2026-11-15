using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCafé.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSeparateRielDrawerCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OpeningCashRiel",
                table: "WorkShifts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CountedCashRiel",
                table: "ShiftReconciliations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DifferenceCashRiel",
                table: "ShiftReconciliations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExpectedCashRiel",
                table: "ShiftReconciliations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OpeningCashRiel",
                table: "ShiftReconciliations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reconciliation_Riel",
                table: "ShiftReconciliations",
                sql: "(OpeningCashRiel IS NULL AND ExpectedCashRiel IS NULL AND CountedCashRiel IS NULL AND DifferenceCashRiel IS NULL) OR (OpeningCashRiel IS NOT NULL AND ExpectedCashRiel IS NOT NULL AND CountedCashRiel IS NOT NULL AND DifferenceCashRiel IS NOT NULL AND OpeningCashRiel >= 0 AND CountedCashRiel >= 0 AND ExpectedCashRiel = OpeningCashRiel AND DifferenceCashRiel = CountedCashRiel - ExpectedCashRiel)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reconciliation_Riel",
                table: "ShiftReconciliations");

            migrationBuilder.DropColumn(
                name: "OpeningCashRiel",
                table: "WorkShifts");

            migrationBuilder.DropColumn(
                name: "CountedCashRiel",
                table: "ShiftReconciliations");

            migrationBuilder.DropColumn(
                name: "DifferenceCashRiel",
                table: "ShiftReconciliations");

            migrationBuilder.DropColumn(
                name: "ExpectedCashRiel",
                table: "ShiftReconciliations");

            migrationBuilder.DropColumn(
                name: "OpeningCashRiel",
                table: "ShiftReconciliations");
        }
    }
}
