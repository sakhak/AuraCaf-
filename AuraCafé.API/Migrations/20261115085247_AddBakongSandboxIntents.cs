using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCafé.API.Migrations
{
    /// <inheritdoc />
    public partial class AddBakongSandboxIntents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BakongSandboxIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountCents = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiverAccountId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    QrPayload = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Md5 = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TransactionHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakongSandboxIntents", x => x.Id);
                    table.CheckConstraint("CK_BakongSandbox_Amount", "AmountCents > 0");
                    table.CheckConstraint("CK_BakongSandbox_Status", "Status IN ('Pending','Expired','TestVerified')");
                    table.CheckConstraint("CK_BakongSandbox_Verification", "(Status = 'TestVerified' AND TransactionHash IS NOT NULL AND VerifiedAtUtc IS NOT NULL) OR (Status <> 'TestVerified' AND TransactionHash IS NULL AND VerifiedAtUtc IS NULL)");
                    table.ForeignKey(
                        name: "FK_BakongSandboxIntents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BakongSandboxIntents_Md5",
                table: "BakongSandboxIntents",
                column: "Md5",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BakongSandboxIntents_OrderId",
                table: "BakongSandboxIntents",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BakongSandboxIntents_TransactionHash",
                table: "BakongSandboxIntents",
                column: "TransactionHash",
                unique: true,
                filter: "\"TransactionHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BakongSandboxIntents");
        }
    }
}
