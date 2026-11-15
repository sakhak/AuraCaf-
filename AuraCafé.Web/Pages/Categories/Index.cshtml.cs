using System.ComponentModel.DataAnnotations;
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Categories;

[Authorize(Roles = "Admin")]
public class IndexModel(ProductApiService api, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int? EditId { get; set; }
    [BindProperty, Required, StringLength(100), Display(Name = "Category name")] public string Name { get; set; } = "";
    public CategoryPageDto? Data { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((Data?.TotalCount ?? 0) / 12d));
    public string? ErrorMessage { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid || PageNumber is < 1 or > 1000000 || Search?.Length > 100 || EditId <= 0) { ErrorMessage = "Invalid search, category or page."; return Page(); }
        if (EditId.HasValue)
        {
            try
            {
                var category = await api.GetCategoryAsync(EditId.Value);
                if (category is null) return NotFound();
                Name = category.Name;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = "Unable to load this category. Refresh before editing."; }
        }
        await LoadAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (EditId <= 0) ModelState.AddModelError(string.Empty, "Invalid category.");
        if (ModelState.IsValid)
        {
            try
            {
                if (EditId.HasValue) await api.UpdateCategoryAsync(EditId.Value, Name);
                else await api.CreateCategoryAsync(Name);
                SuccessMessage = EditId.HasValue ? "Category updated. Its product links are preserved." : "Category created. It is ready to use when adding products.";
                return RedirectToPage();
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                logger.LogWarning(ex, "Category save could not be confirmed");
                ModelState.AddModelError(string.Empty, ex is TaskCanceledException ? "The save result is unknown. Check the list before retrying." : ex.Message);
            }
        }
        await LoadAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try { await api.DeleteCategoryAsync(id); SuccessMessage = "Unused category deleted."; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            TempData["CategoryError"] = ex is TaskCanceledException ? "Deletion could not be confirmed. Refresh before retrying." : ex.Message;
        }
        return RedirectToPage(new { Search, PageNumber });
    }
    private async Task LoadAsync()
    {
        try
        {
            Data = await api.SearchCategoriesAsync(Search, PageNumber);
            if (PageNumber > TotalPages) { PageNumber = TotalPages; Data = await api.SearchCategoriesAsync(Search, PageNumber); }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Category list unavailable");
            ErrorMessage ??= "Unable to load categories. Check the API connection and refresh.";
        }
    }
}
