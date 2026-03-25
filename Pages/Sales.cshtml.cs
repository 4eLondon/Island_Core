using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IslandCore.Pages;

public class SalesModel(DataService data) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? CustomerFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? FromDate       { get; set; }
    [BindProperty(SupportsGet = true)] public string? ToDate         { get; set; }

    public List<SalesRecord> Sales { get; set; } = [];
    public int TotalRevenue        { get; set; }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        try
        {
            DateTime? from = DateTime.TryParse(FromDate, out var f) ? f.Date : null;
            DateTime? to   = DateTime.TryParse(ToDate,   out var t) ? t.Date.AddDays(1).AddTicks(-1) : null;

            Sales        = data.ReadSales(CustomerFilter, from, to);
            TotalRevenue = Sales.Sum(s => s.TotalAmount);
        }
        catch { /* show empty on error */ }

        return Page();
    }
}
