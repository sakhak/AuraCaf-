using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Inventory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class InventoryChecks
{
    public static async Task<Dictionary<int, long>> RunAsync(IServiceProvider services, string url, string adminToken, string cashierToken, int productId, Action<bool, string> check)
    {
        using var client = new HttpClient { BaseAddress = new Uri(url) };
        check((await client.GetAsync("api/inventory/summary")).StatusCode == HttpStatusCode.Unauthorized, "Inventory anonymous access denied");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
        check((await client.GetAsync("api/inventory/ingredients")).StatusCode == HttpStatusCode.Forbidden, "Inventory Cashier read denied");
        check((await client.PostAsJsonAsync("api/inventory/ingredients", new { Name = "Forbidden", Unit = "g" })).StatusCode == HttpStatusCode.Forbidden, "Inventory Cashier write denied");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var empty = await client.GetFromJsonAsync<InventorySummaryDto>("api/inventory/summary");
        check(empty!.Ingredients == 0 && empty.Movements == 0, "Inventory starts empty without illustrative seed records");
        async Task<IngredientDto> Add(string name, string unit, long minimum = 0)
        {
            var response = await client.PostAsJsonAsync("api/inventory/ingredients", new { Name = name, Unit = unit, MinimumQuantity = minimum });
            check(response.StatusCode == HttpStatusCode.Created, "Inventory ingredient created: " + name);
            return (await response.Content.ReadFromJsonAsync<IngredientDto>())!;
        }
        var beans = await Add("Coffee Beans", "g", 500);
        var milk = await Add("Fresh Milk", "ml");
        check(beans.QuantityOnHand == 0 && beans.StockStatus == "Out of stock", "Inventory ingredient begins at zero with empty-stock alert");
        check((await client.PostAsJsonAsync("api/inventory/ingredients", new { Name = " coffee beans ", Unit = "g" })).StatusCode == HttpStatusCode.Conflict, "Inventory normalized names are unique");
        foreach (var invalid in new object[]
        {
            new { Name = "Bad unit", Unit = "kg" }, new { Name = "Bad minimum", Unit = "g", MinimumQuantity = -1 },
            new { Name = "Bad fractional", Unit = "g", MinimumQuantity = 1.5 },
            new { Name = "Direct stock", Unit = "g", QuantityOnHand = 100 }
        }) check((await client.PostAsJsonAsync("api/inventory/ingredients", invalid)).StatusCode == HttpStatusCode.BadRequest, "Inventory invalid ingredient rejected: " + invalid);
        check((await client.PutAsJsonAsync($"api/inventory/ingredients/{beans.Id}", new { Name = beans.Name, MinimumQuantity = 500, IsActive = true, QuantityOnHand = 999 })).StatusCode == HttpStatusCode.BadRequest, "Inventory metadata cannot overwrite stock balance");
        check((await client.PutAsJsonAsync($"api/inventory/ingredients/{beans.Id}", new { Name = beans.Name, MinimumQuantity = 500, IsActive = true, Unit = "ml" })).StatusCode == HttpStatusCode.BadRequest, "Inventory metadata cannot change base units");
        object Move(Guid key, string type, long quantity, string reason) => new { RequestId = key, Type = type, Quantity = quantity, Reason = reason };
        async Task<StockMovementDto> Apply(string type, long quantity, string reason)
        {
            var response = await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", Move(Guid.NewGuid(), type, quantity, reason));
            check(response.IsSuccessStatusCode, "Inventory stock transaction: " + reason);
            return (await response.Content.ReadFromJsonAsync<StockMovementDto>())!;
        }
        var key = Guid.NewGuid();
        var stockIn = Move(key, "StockIn", 2000, "Receive supplies");
        var first = await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", stockIn);
        var firstMove = (await first.Content.ReadFromJsonAsync<StockMovementDto>())!;
        check(firstMove.BalanceBefore == 0 && firstMove.BalanceAfter == 2000 && firstMove.QuantityChange == 2000 && !string.IsNullOrEmpty(firstMove.PerformedByUserId), "Inventory stock-in creates audited ledger and balance");
        var retry = await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", stockIn);
        check((await retry.Content.ReadFromJsonAsync<StockMovementDto>())!.Id == firstMove.Id, "Inventory unchanged retry returns the same movement");
        check((await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", Move(key, "StockIn", 100, "Receive supplies"))).StatusCode == HttpStatusCode.Conflict, "Inventory reused request ID with changed intent rejected");
        check((await Apply("StockOut", 300, "Record wastage")).BalanceAfter == 1700, "Inventory stock-out subtracts quantity");
        check((await Apply("Adjustment", -100, "Count correction")).BalanceAfter == 1600, "Inventory negative adjustment records delta");
        check((await Apply("Adjustment", 200, "Found supplies")).BalanceAfter == 1800, "Inventory positive adjustment records delta");
        check((await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", Move(Guid.NewGuid(), "StockOut", 5000, "Too much stock"))).StatusCode == HttpStatusCode.Conflict, "Inventory rejects insufficient stock");
        foreach (var invalid in new object[]
        {
            Move(Guid.NewGuid(), "StockIn", 0, "Zero quantity"), Move(Guid.NewGuid(), "StockIn", -1, "Negative receipt"),
            Move(Guid.Empty, "StockIn", 1, "Missing identity"), Move(Guid.NewGuid(), "Unknown", 1, "Invalid type"),
            Move(Guid.NewGuid(), "StockIn", 1, "  "),
            new { RequestId = Guid.NewGuid(), Type = "StockIn", Quantity = 1.5, Reason = "Fractional grams" },
            new { RequestId = Guid.NewGuid(), Type = "StockIn", Quantity = 1, Reason = "Forged actor", PerformedByUserId = "someone-else" }
        }) check(!(await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", invalid)).IsSuccessStatusCode, "Inventory invalid movement rejected");
        await Apply("Adjustment", -1400, "Lower physical count");
        check((await client.GetFromJsonAsync<IngredientDto>($"api/inventory/ingredients/{beans.Id}"))!.StockStatus == "Low stock", "Inventory low-stock threshold calculated from balance");
        await Apply("StockOut", 400, "Use remaining stock");
        await Apply("StockIn", 100, "Concurrency fixture");
        var outs = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", Move(Guid.NewGuid(), "StockOut", 80, "Simultaneous usage"))));
        check(outs.Count(r => r.IsSuccessStatusCode) == 1 && outs.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Inventory concurrent stock-out cannot overspend");
        check((await client.GetFromJsonAsync<IngredientDto>($"api/inventory/ingredients/{beans.Id}"))!.QuantityOnHand == 20, "Inventory concurrent stock-out leaves correct nonnegative balance");
        var sharedKey = Guid.NewGuid();
        var receipts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", Move(sharedKey, "StockIn", 100, "Concurrent retry"))));
        var receiptA = await receipts[0].Content.ReadFromJsonAsync<StockMovementDto>();
        var receiptB = await receipts[1].Content.ReadFromJsonAsync<StockMovementDto>();
        check(receiptA!.Id == receiptB!.Id && receiptA.BalanceAfter == 120, "Inventory concurrent identical requests commit once");
        var sugar = await Add("Sugar", "g");
        check((await client.PostAsJsonAsync($"api/inventory/ingredients/{sugar.Id}/movements", Move(Guid.NewGuid(), "StockIn", long.MaxValue, "Large integer fixture"))).IsSuccessStatusCode, "Inventory supports exact 64-bit stock quantity");
        check((await client.PostAsJsonAsync($"api/inventory/ingredients/{sugar.Id}/movements", Move(Guid.NewGuid(), "StockIn", 1, "Overflow fixture"))).StatusCode == HttpStatusCode.BadRequest, "Inventory quantity overflow rejected without write");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailMovement BEFORE INSERT ON StockMovements BEGIN SELECT RAISE(ABORT, 'Injected failure'); END;");
        }
        check((await client.PostAsJsonAsync($"api/inventory/ingredients/{milk.Id}/movements", Move(Guid.NewGuid(), "StockIn", 150, "Rollback fixture"))).StatusCode == HttpStatusCode.Conflict, "Inventory movement failure returns controlled conflict");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            check((await db.Ingredients.FindAsync(milk.Id))!.QuantityOnHand == 0 && !await db.StockMovements.AnyAsync(m => m.IngredientId == milk.Id), "Inventory failed movement rolls balance and ledger back together");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailMovement;");
            async Task MustFail(string sql, string label)
            {
                try { await db.Database.ExecuteSqlRawAsync(sql); check(false, label); }
                catch (SqliteException) { check(true, label); }
            }
            await MustFail("UPDATE StockMovements SET Reason='changed' WHERE Id=" + firstMove.Id, "Inventory database blocks ledger updates");
            await MustFail("DELETE FROM StockMovements WHERE Id=" + firstMove.Id, "Inventory database blocks ledger deletes");
            await MustFail("UPDATE Ingredients SET Unit='ml' WHERE Id=" + beans.Id, "Inventory database blocks unit changes");
            await MustFail("UPDATE Ingredients SET QuantityOnHand=-1 WHERE Id=" + beans.Id, "Inventory database rejects negative balance");
        }
        var beforeRecipe = (await client.GetFromJsonAsync<InventorySummaryDto>("api/inventory/summary"))!.Movements;
        var recipe = new { Lines = new[] { new { IngredientId = beans.Id, QuantityRequired = 18 }, new { IngredientId = milk.Id, QuantityRequired = 150 } } };
        check((await client.PutAsJsonAsync($"api/inventory/products/{productId}/recipe", recipe)).IsSuccessStatusCode, "Inventory product recipe saved");
        var savedRecipe = await client.GetFromJsonAsync<ProductRecipeDto>($"api/inventory/products/{productId}/recipe");
        check(savedRecipe!.Lines.Count == 2 && savedRecipe.Lines.Single(l => l.IngredientId == milk.Id).Unit == "ml", "Inventory recipe retrieves ingredient quantities and units");
        check((await client.PutAsJsonAsync($"api/inventory/products/{productId}/recipe", new { Lines = new[] { new { IngredientId = beans.Id, QuantityRequired = 20 } } })).IsSuccessStatusCode, "Inventory recipe updates existing pair and removes absent lines");
        check((await client.GetFromJsonAsync<InventorySummaryDto>("api/inventory/summary"))!.Movements == beforeRecipe
            && (await client.GetFromJsonAsync<IngredientDto>($"api/inventory/ingredients/{beans.Id}"))!.QuantityOnHand == 120,
            "Inventory recipe changes do not create movements or deduct balance");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ProductIngredients SET QuantityRequired=0 WHERE ProductId={productId}");
                check(false, "Inventory recipe quantity database constraint");
            }
            catch (SqliteException) { check(true, "Inventory database rejects zero recipe quantity"); }
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProductIngredients (ProductId,IngredientId,QuantityRequired) VALUES ({productId},{beans.Id},1)");
                check(false, "Inventory recipe uniqueness database constraint");
            }
            catch (SqliteException) { check(true, "Inventory database rejects duplicate product/ingredient pair"); }
        }
        check((await client.PutAsJsonAsync($"api/inventory/products/{productId}/recipe", new { Lines = new[] { new { IngredientId = beans.Id, QuantityRequired = 1 }, new { IngredientId = beans.Id, QuantityRequired = 2 } } })).StatusCode == HttpStatusCode.BadRequest, "Inventory recipe rejects duplicate ingredients");
        check((await client.PutAsJsonAsync($"api/inventory/products/{productId}/recipe", new { Lines = new[] { new { IngredientId = milk.Id, QuantityRequired = 0 } } })).StatusCode == HttpStatusCode.BadRequest, "Inventory recipe rejects zero quantities");
        check((await client.PutAsJsonAsync($"api/inventory/products/{productId}/recipe", new { Lines = new[] { new { IngredientId = int.MaxValue, QuantityRequired = 1 } } })).StatusCode == HttpStatusCode.BadRequest, "Inventory recipe rejects missing ingredient");
        check((await client.PutAsJsonAsync("api/inventory/products/2147483647/recipe", recipe)).StatusCode == HttpStatusCode.NotFound, "Inventory recipe rejects missing product");
        await client.DeleteAsync($"api/inventory/ingredients/{beans.Id}");
        var inactive = await client.GetFromJsonAsync<IngredientDto>($"api/inventory/ingredients/{beans.Id}");
        check(!inactive!.IsActive && inactive.QuantityOnHand == 120 && (await client.GetFromJsonAsync<ProductRecipeDto>($"api/inventory/products/{productId}/recipe"))!.Lines.Count == 1, "Inventory deactivation preserves balance, ledger and recipe reference");
        check((await client.PostAsJsonAsync($"api/inventory/ingredients/{beans.Id}/movements", Move(Guid.NewGuid(), "StockIn", 10, "Inactive ingredient"))).StatusCode == HttpStatusCode.Conflict, "Inventory inactive ingredients reject stock changes");
        check((await client.PutAsJsonAsync($"api/inventory/products/{productId}/recipe", recipe)).StatusCode == HttpStatusCode.BadRequest, "Inventory recipe save rejects inactive ingredient");
        check((await client.PutAsJsonAsync($"api/inventory/ingredients/{beans.Id}", new { Name = "Coffee Beans", MinimumQuantity = 500, IsActive = true })).IsSuccessStatusCode, "Inventory ingredient can be reactivated");
        for (var i = 0; i < 21; i++) await client.PostAsJsonAsync("api/inventory/ingredients", new { Name = $"Paged Ingredient {i:D2}", Unit = "pcs" });
        var page = await client.GetFromJsonAsync<IngredientPageDto>("api/inventory/ingredients?search=paged&page=2");
        check(page!.TotalCount == 21 && page.Items.Count == 1, "Inventory SQL pagination returns correct slice and total");
        var alerts = await client.GetFromJsonAsync<IngredientPageDto>("api/inventory/ingredients?lowOnly=true&search=coffee");
        check(alerts!.Items.Single().Id == beans.Id, "Inventory search and low-stock filter combine");
        check((await client.GetAsync("api/inventory/ingredients?page=0")).StatusCode == HttpStatusCode.BadRequest, "Inventory invalid ingredient page rejected");
        var ledgerPage = await client.GetFromJsonAsync<MovementPageDto>($"api/inventory/movements?ingredientId={beans.Id}");
        check(ledgerPage!.Items.All(m => m.IngredientId == beans.Id) && ledgerPage.Items.Any(m => m.RequestId == sharedKey), "Inventory filtered immutable history includes idempotency references");
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var movements = await db.StockMovements.Where(m => m.IngredientId == beans.Id).OrderBy(m => m.Id).ToListAsync();
            long running = 0;
            foreach (var movement in movements)
            {
                if (movement.BalanceBefore != running) throw new Exception("Broken inventory ledger chain");
                running = checked(running + movement.QuantityChange);
                if (movement.BalanceAfter != running) throw new Exception("Broken inventory movement arithmetic");
            }
            check(running == (await db.Ingredients.FindAsync(beans.Id))!.QuantityOnHand, "Inventory ledger chain reconciles with stored balance");
            return await db.Ingredients.ToDictionaryAsync(i => i.Id, i => i.QuantityOnHand);
        }
    }
}
