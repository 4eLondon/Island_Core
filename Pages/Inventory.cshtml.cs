using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IslandCore.Pages;

public class InventoryModel(DataService data) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? CategoryFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? SupplierFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? StatusFilter   { get; set; }

    public List<InventoryItem> Items { get; set; } = [];
    public List<string> Categories   { get; set; } = [];
    public List<string> Suppliers    { get; set; } = [];
    public int TotalItems, InStock, LowStock, OutOfStock, InventoryValue, TotalAll;

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        try
        {
            // Filters and distinct lists all hit the DB directly
            Categories = data.GetCategories();
            Suppliers  = data.GetSuppliers();

            var all = data.ReadInventory(); // unfiltered for totals
            TotalAll = all.Count;

            Items          = data.ReadInventory(CategoryFilter, SupplierFilter, StatusFilter);
            TotalItems     = Items.Count;
            OutOfStock     = Items.Count(i => i.Stock <= 0);
            LowStock       = Items.Count(i => i.Stock > 0 && i.Stock <= i.ReorderLevel);
            InStock        = Items.Count(i => i.Stock > i.ReorderLevel);
            InventoryValue = Items.Sum(i => i.TotalOrderCost);
        }
        catch { /* show empty on error */ }

        return Page();
    }
}
