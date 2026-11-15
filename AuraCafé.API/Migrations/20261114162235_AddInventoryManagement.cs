using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuraCafé.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    QuantityOnHand = table.Column<long>(type: "INTEGER", nullable: false),
                    MinimumQuantity = table.Column<long>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.CheckConstraint("CK_Ingredient_Quantities", "QuantityOnHand >= 0 AND MinimumQuantity >= 0");
                    table.CheckConstraint("CK_Ingredient_Unit", "Unit IN ('g','ml','pcs')");
                });

            migrationBuilder.CreateTable(
                name: "ProductIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityRequired = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIngredients", x => x.Id);
                    table.CheckConstraint("CK_Recipe_Quantity", "QuantityRequired > 0");
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    QuantityChange = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceBefore = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceAfter = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PerformedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_Movement_Balance", "BalanceBefore >= 0 AND BalanceAfter >= 0 AND QuantityChange <> 0 AND BalanceAfter = BalanceBefore + QuantityChange");
                    table.CheckConstraint("CK_Movement_Type", "(Type = 'StockIn' AND QuantityChange > 0) OR (Type = 'StockOut' AND QuantityChange < 0) OR Type = 'Adjustment'");
                    table.ForeignKey(
                        name: "FK_StockMovements_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_NormalizedName",
                table: "Ingredients",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_IngredientId",
                table: "ProductIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_ProductId_IngredientId",
                table: "ProductIngredients",
                columns: new[] { "ProductId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_IngredientId_Id",
                table: "StockMovements",
                columns: new[] { "IngredientId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PerformedByUserId",
                table: "StockMovements",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_RequestId",
                table: "StockMovements",
                column: "RequestId",
                unique: true);
            migrationBuilder.Sql("""
                CREATE TRIGGER StockMovements_NoUpdate BEFORE UPDATE ON StockMovements
                BEGIN SELECT RAISE(ABORT, 'Stock movement history is immutable. Add a correcting movement.'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER StockMovements_NoDelete BEFORE DELETE ON StockMovements
                BEGIN SELECT RAISE(ABORT, 'Stock movement history is immutable.'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER Ingredients_UnitImmutable BEFORE UPDATE OF Unit ON Ingredients
                WHEN NEW.Unit <> OLD.Unit
                BEGIN SELECT RAISE(ABORT, 'Ingredient units are immutable.'); END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS StockMovements_NoUpdate;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS StockMovements_NoDelete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS Ingredients_UnitImmutable;");
            migrationBuilder.DropTable(
                name: "ProductIngredients");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Ingredients");
        }
    }
}
