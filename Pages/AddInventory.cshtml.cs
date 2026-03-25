using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace IslandCore.Pages;

public class AddInventoryModel(DataService data) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? EditSku { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search  { get; set; }

    public string? StatusMessage { get; set; }
    public bool IsSuccess        { get; set; }
    public List<InventoryItem> Items { get; set; } = [];

    public class InputModel
    {
        [Required] public string SKU         { get; set; } = "";
        [Required] public string ProductName { get; set; } = "";
        public string Category   { get; set; } = "";
        public string Supplier   { get; set; } = "";
        [Required, Range(0, int.MaxValue)] public int Stock       { get; set; }
        public int ReorderLevel  { get; set; }
        [Required, Range(0, int.MaxValue)] public int CostPerUnit { get; set; }
        public string Status     { get; set; } = "In Stock";
        public bool IsEdit       { get; set; }
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        LoadItems();

        // Pre-populate form if editing
        if (!string.IsNullOrEmpty(EditSku))
        {
            var item = data.GetInventoryItem(EditSku);
            if (item is not null)
            {
                Input = new InputModel
                {
                    SKU          = item.SKU,
                    ProductName  = item.ProductName,
                    Category     = item.Category,
                    Supplier     = item.Supplier,
                    Stock        = item.Stock,
                    ReorderLevel = item.ReorderLevel,
                    CostPerUnit  = item.CostPerUnit,
                    Status       = item.Status,
                    IsEdit       = true
                };
            }
        }

        return Page();
    }

    public IActionResult OnPostSave()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        LoadItems();
        if (!ModelState.IsValid) return Page();

        var totalOrderCost = Input.CostPerUnit * Input.Stock;
        var status = DataService.ComputeStatus(Input.Stock, Input.ReorderLevel);
        var item = new InventoryItem(
            Input.SKU, Input.ProductName, Input.Category,
            Input.Stock, Input.ReorderLevel, Input.Supplier,
            status, Input.CostPerUnit, totalOrderCost, DateTime.UtcNow);

        try
        {
            if (Input.IsEdit)
            {
                data.UpdateInventoryItem(item);
                IsSuccess = true;
                StatusMessage = $"'{Input.ProductName}' updated successfully.";
            }
            else
            {
                var (ok, error) = data.AddInventoryItem(item);
                if (!ok) { StatusMessage = error; LoadItems(); return Page(); }
                IsSuccess = true;
                StatusMessage = $"'{Input.ProductName}' added successfully.";
            }

            ModelState.Clear();
            Input = new();
            LoadItems();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving item: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnPostDelete(string sku)
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        try
        {
            data.DeleteInventoryItem(sku);
            IsSuccess = true;
            StatusMessage = $"Item '{sku}' deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting item: {ex.Message}";
        }

        LoadItems();
        return Page();
    }

    private void LoadItems()
    {
        try
        {
            Items = data.ReadInventory(search: Search);
        }
        catch { Items = []; }
    }
}
