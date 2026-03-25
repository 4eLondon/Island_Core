using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace IslandCore.Pages;

public class FinanceModel(DataService data) : PageModel
{
    public int     TotalRevenue   { get; set; }
    public decimal GrossMarginPct { get; set; }
    public long    NetCashFlow    { get; set; }
    public int     DSO            { get; set; }
    public int     OverdueCount   { get; set; }
    public bool    IsLowCash      { get; set; }

    public List<(string Category, int Revenue)>              RevenueByCategory { get; set; } = [];
    public List<(string Category, int Cost)>                 CostByCategory    { get; set; } = [];
    public List<(string Supplier, int Amount, string Status)> AP               { get; set; } = [];
    public List<(string Customer, int Amount, string Status)> AR               { get; set; } = [];

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");
        Load();
        return Page();
    }

    public IActionResult OnPostExportCsv()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");
        Load();
        var sb = new StringBuilder();
        sb.AppendLine("Type,Name,Amount");
        foreach (var r in AP) sb.AppendLine($"AP,\"{r.Supplier.Replace("\"","\"\"")}\",{r.Amount}");
        foreach (var r in AR) sb.AppendLine($"AR,\"{r.Customer.Replace("\"","\"\"")}\",{r.Amount}");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "Finance_AP_AR_Summary.csv");
    }

    private void Load()
    {
        try
        {
            var inventory = data.ReadInventory();
            var sales     = data.ReadSales();

            TotalRevenue = sales.Sum(s => s.TotalAmount);

            var skuCost = inventory
                .GroupBy(i => i.SKU, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().CostPerUnit, StringComparer.OrdinalIgnoreCase);

            long cogs = sales.Sum(s =>
                skuCost.TryGetValue(s.SKU, out var c) ? (long)c * s.Quantity : 0);

            GrossMarginPct = TotalRevenue == 0 ? 0
                : (decimal)(TotalRevenue - cogs) / Math.Max(1, TotalRevenue) * 100;
            NetCashFlow = TotalRevenue - cogs;

            var now  = DateTime.UtcNow;
            var ages = sales.Where(s => s.CreatedUtc != DateTime.MinValue)
                            .Select(s => (now - s.CreatedUtc).TotalDays).ToList();
            DSO          = ages.Any() ? (int)Math.Round(ages.Average()) : 0;
            OverdueCount = sales.Count(s => s.CreatedUtc != DateTime.MinValue && (now - s.CreatedUtc).TotalDays > 60);
            IsLowCash    = inventory.Sum(i => i.TotalOrderCost) > TotalRevenue;

            var skuToCat = inventory
                .GroupBy(i => i.SKU, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                    g => string.IsNullOrEmpty(g.First().Category) ? "(Unspecified)" : g.First().Category,
                    StringComparer.OrdinalIgnoreCase);

            RevenueByCategory = [.. sales
                .GroupBy(s => skuToCat.TryGetValue(s.SKU, out var cat) ? cat
                            : string.IsNullOrEmpty(s.ProductName) ? "(Unspecified)" : s.ProductName)
                .Select(g => (g.Key, g.Sum(x => x.TotalAmount)))
                .OrderByDescending(x => x.Item2)];

            CostByCategory = [.. inventory
                .GroupBy(i => string.IsNullOrEmpty(i.Category) ? "(Unspecified)" : i.Category)
                .Select(g => (g.Key, g.Sum(x => x.TotalOrderCost)))
                .OrderByDescending(x => x.Item2)];

            AP = [.. inventory
                .GroupBy(i => string.IsNullOrEmpty(i.Supplier) ? "(Unspecified)" : i.Supplier)
                .Select(g => (g.Key, g.Sum(x => x.TotalOrderCost),
                             g.OrderBy(x => x.Status).First().Status ?? "Pending"))
                .OrderByDescending(x => x.Item2).Take(10)];

            AR = [.. sales
                .GroupBy(s => string.IsNullOrEmpty(s.Customer) ? "(Unknown)" : s.Customer)
                .Select(g =>
                {
                    var total  = g.Sum(x => x.TotalAmount);
                    var latest = g.Max(x => x.CreatedUtc);
                    var age    = latest == DateTime.MinValue ? 0 : (now - latest).TotalDays;
                    var status = age > 60 ? "Overdue" : age > 30 ? "Pending" : "Open";
                    return (g.Key, total, status);
                })
                .OrderByDescending(x => x.total).Take(10)];
        }
        catch { /* show empty on DB error */ }
    }
}
