using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace IslandCore.Pages;

public class ReportsModel(DataService data) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? InvCategory   { get; set; }
    [BindProperty(SupportsGet = true)] public string? InvSupplier   { get; set; }
    [BindProperty(SupportsGet = true)] public string? SalesCustomer { get; set; }
    [BindProperty(SupportsGet = true)] public string? SalesFrom     { get; set; }
    [BindProperty(SupportsGet = true)] public string? SalesTo       { get; set; }

    public List<InventoryItem> InventoryRows { get; set; } = [];
    public List<SalesRecord>   SalesRows     { get; set; } = [];
    public List<string> InvCategories        { get; set; } = [];
    public List<string> InvSuppliers         { get; set; } = [];
    public int TotalStockUnits, DistinctItems, TotalSales;

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");
        Load();
        return Page();
    }

    public IActionResult OnPostExportInventory(string? invCategory, string? invSupplier)
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");
        InvCategory = invCategory;
        InvSupplier = invSupplier;
        Load();
        var sb = new StringBuilder();
        sb.AppendLine("SKU,Product,Category,Stock,Reorder,Supplier,CostPerUnit,TotalCost,Status");
        foreach (var i in InventoryRows)
            sb.AppendLine($"\"{i.SKU}\",\"{i.ProductName}\",\"{i.Category}\",{i.Stock},{i.ReorderLevel},\"{i.Supplier}\",{i.CostPerUnit},{i.TotalOrderCost},\"{i.Status}\"");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "Inventory_Report.csv");
    }

    public IActionResult OnPostExportSales(string? salesCustomer, string? salesFrom, string? salesTo)
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");
        SalesCustomer = salesCustomer;
        SalesFrom     = salesFrom;
        SalesTo       = salesTo;
        Load();
        var sb = new StringBuilder();
        sb.AppendLine("Date,Customer,SKU,Product,Quantity,UnitPrice,Total");
        foreach (var s in SalesRows)
        {
            var d = s.CreatedUtc == DateTime.MinValue ? "" : s.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd");
            sb.AppendLine($"\"{d}\",\"{s.Customer}\",\"{s.SKU}\",\"{s.ProductName}\",{s.Quantity},{s.UnitPrice},{s.TotalAmount}");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "Sales_Report.csv");
    }

    private void Load()
    {
        try
        {
            InvCategories = data.GetCategories();
            InvSuppliers  = data.GetSuppliers();

            // All inventory unfiltered for summary stats
            var allInv = data.ReadInventory();
            TotalStockUnits = allInv.Sum(i => i.Stock);
            DistinctItems   = allInv.Count;

            // Filtered views pushed to DB
            InventoryRows = data.ReadInventory(InvCategory, InvSupplier);

            DateTime? from = DateTime.TryParse(SalesFrom, out var f) ? f.Date : null;
            DateTime? to   = DateTime.TryParse(SalesTo,   out var t) ? t.Date.AddDays(1).AddTicks(-1) : null;
            SalesRows  = data.ReadSales(SalesCustomer, from, to);
            TotalSales = SalesRows.Sum(s => s.TotalAmount);
        }
        catch { /* show empty on DB error */ }
    }
}
