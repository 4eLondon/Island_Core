using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IslandCore.Pages;

public class DashboardModel(DataService data) : PageModel
{
    public int TotalStockUnits { get; set; }
    public int DistinctItems   { get; set; }
    public int LowStockCount   { get; set; }
    public int InventoryValue  { get; set; }
    public int TotalSpent      { get; set; }
    public int TotalEarned     { get; set; }

    public List<InventoryItem> LowStockItems { get; set; } = [];
    public List<(string Key, int Count)> CategoryGroups { get; set; } = [];
    public List<(string Name, int TotalQty)> TopProducts { get; set; } = [];

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        try
        {
            var inv   = data.ReadInventory();
            var sales = data.ReadSales();

            TotalStockUnits = inv.Sum(i => i.Stock);
            DistinctItems   = inv.Count;
            LowStockCount   = inv.Count(i => i.Stock <= i.ReorderLevel);
            InventoryValue  = inv.Sum(i => i.TotalOrderCost);
            TotalSpent      = InventoryValue;
            TotalEarned     = sales.Sum(s => s.TotalAmount);

            LowStockItems = [.. inv
                .Where(i => i.Stock <= i.ReorderLevel)
                .OrderBy(i => i.Stock).ThenBy(i => i.ProductName).Take(5)];

            CategoryGroups = [.. inv
                .GroupBy(i => string.IsNullOrEmpty(i.Category) ? "(Unspecified)" : i.Category)
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)];

            TopProducts = [.. sales
                .GroupBy(s => string.IsNullOrEmpty(s.ProductName) ? "(Unspecified)" : s.ProductName)
                .Select(g => (g.Key, g.Sum(x => x.Quantity)))
                .OrderByDescending(x => x.Item2).Take(4)];
        }
        catch { /* show empty dashboard on DB error */ }

        return Page();
    }
}
