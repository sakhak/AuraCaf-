using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

internal static class CategoryChecks
{
    public static async Task RunAsync(string url, string adminToken, string cashierToken, Action<bool, string> check)
    {
        using var client = new HttpClient { BaseAddress = new Uri(url) };
        check((await client.GetAsync("api/categories/manage")).StatusCode == HttpStatusCode.Unauthorized, "Category management denies anonymous requests");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);
        check((await client.GetAsync("api/categories")).IsSuccessStatusCode, "Cashier retains category catalog access");
        check((await client.GetAsync("api/categories/manage")).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot load Admin category management");
        check((await client.PostAsJsonAsync("api/categories", new { Name = "Forbidden" })).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot create categories");
        check((await client.PutAsJsonAsync("api/categories/1", new { Name = "Forbidden" })).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot rename categories");
        check((await client.DeleteAsync("api/categories/1")).StatusCode == HttpStatusCode.Forbidden, "Cashier cannot delete categories");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsJsonAsync("api/categories", new { Name = " Lunch " });
        check(response.StatusCode == HttpStatusCode.Created, "Admin creates trimmed category");
        var created = await response.Content.ReadFromJsonAsync<CategoryRow>();
        check(created!.Name == "Lunch" && (await client.GetAsync(response.Headers.Location)).IsSuccessStatusCode, "Category creation returns accessible Location");
        var product = await client.PostAsJsonAsync("api/products", new { Name = "Lunch Product", PriceCents = 100, CategoryId = created.Id });
        var productData = await product.Content.ReadFromJsonAsync<ProductRow>();
        check(product.IsSuccessStatusCode, "Product can be assigned to a real category");
        check((await client.PutAsJsonAsync($"api/categories/{created.Id}", new { Name = "Meals" })).IsSuccessStatusCode, "Admin renames category");
        var linked = await client.GetFromJsonAsync<ProductRow>($"api/products/{productData!.Id}");
        check(linked!.CategoryId == created.Id && linked.CategoryName == "Meals", "Category rename preserves product ID links");
        check((await client.DeleteAsync($"api/categories/{created.Id}")).StatusCode == HttpStatusCode.Conflict, "Category containing products cannot be deleted");
        var managed = await client.GetFromJsonAsync<CategoryPage>("api/categories/manage?search=meals");
        check(managed!.TotalCount == 1 && managed.Items.Single().ProductCount == 1 && managed.AssignedProducts >= 1, "Category search and counts come from stored products");
        check((await client.PostAsJsonAsync("api/categories", new { Name = " meals " })).StatusCode == HttpStatusCode.Conflict, "Category duplicate names rejected ignoring case and surrounding spaces");
        var concurrent = await Task.WhenAll(new[] { "Concurrent Category", "concurrent category" }.Select(name => client.PostAsJsonAsync("api/categories", new { Name = name })));
        check(concurrent.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && concurrent.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Simultaneous category creates cannot bypass name check with letter case");
        check((await client.PostAsJsonAsync("api/categories", new { Name = "   " })).StatusCode == HttpStatusCode.BadRequest, "Blank category rejected");
        check((await client.PostAsJsonAsync("api/categories", new { Name = new string('x', 101) })).StatusCode == HttpStatusCode.BadRequest, "Overlong category rejected");
        var unused = await client.PostAsJsonAsync("api/categories", new { Name = "Unused Category" });
        var unusedCategory = (await unused.Content.ReadFromJsonAsync<CategoryRow>())!;
        check((await client.PutAsJsonAsync($"api/categories/{unusedCategory.Id}", new { Name = "Meals" })).StatusCode == HttpStatusCode.Conflict, "Category edit cannot duplicate another name");
        check((await client.DeleteAsync($"api/categories/{unusedCategory.Id}")).IsSuccessStatusCode && (await client.GetAsync($"api/categories/{unusedCategory.Id}")).StatusCode == HttpStatusCode.NotFound,
            "Unused category can be deleted without touching products");
        for (var i = 0; i < 13; i++) await client.PostAsJsonAsync("api/categories", new { Name = $"Paged Category {i:D2}" });
        var second = await client.GetFromJsonAsync<CategoryPage>("api/categories/manage?search=Paged%20Category&page=2");
        check(second!.TotalCount == 13 && second.Items.Count == 1, "Category management pagination returns correct total and page slice");
        var none = await client.GetFromJsonAsync<CategoryPage>("api/categories/manage?search=NoMatchingCategory");
        check(none!.TotalCount == 0 && none.Items.Count == 0, "Category search returns honest empty state");
        check((await client.GetAsync("api/categories/manage?page=0")).StatusCode == HttpStatusCode.BadRequest, "Invalid category page rejected");
    }
    private class CategoryRow { public int Id { get; set; } public string Name { get; set; } = ""; public int ProductCount { get; set; } }
    private class ProductRow { public int Id { get; set; } public int CategoryId { get; set; } public string CategoryName { get; set; } = ""; }
    private class CategoryPage { public List<CategoryRow> Items { get; set; } = []; public int TotalCount { get; set; } public int AssignedProducts { get; set; } }
}
