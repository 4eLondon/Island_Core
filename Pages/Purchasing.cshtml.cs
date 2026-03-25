using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace IslandCore.Pages;

public class PurchasingModel(DataService data) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search         { get; set; }
    [BindProperty(SupportsGet = true)] public string? CategoryFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? SupplierFilter { get; set; }
    [BindProperty] public PurchaseInput Purchase { get; set; } = new();

    public List<InventoryItem> Items    { get; set; } = [];
    public List<InventoryItem> AllItems { get; set; } = [];
    public List<string> Categories      { get; set; } = [];
    public List<string> Suppliers       { get; set; } = [];
    public string? PurchaseMessage;
    public bool PurchaseSuccess;

    public class PurchaseInput
    {
        [Required] public string Customer { get; set; } = "";
        [Required] public string SKU      { get; set; } = "";
        [Required, Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");
        Load();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (HttpContext.Session.GetString("Username") is null)
            return RedirectToPage("/Login");

        Load();
        if (!ModelState.IsValid) return Page();

        try
        {
            // Fetch only the item being purchased — no need to load all inventory
            var item = data.GetInventoryItem(Purchase.SKU);

            if (item is null)
            {
                PurchaseMessage = "Product not found.";
                return Page();
            }
            if (Purchase.Quantity > item.Stock)
            {
                PurchaseMessage = $"Insufficient stock. Only {item.Stock} unit(s) available.";
                return Page();
            }

            var unitPrice   = item.CostPerUnit;
            var totalAmount = unitPrice * Purchase.Quantity;
            var newStock    = item.Stock - Purchase.Quantity;
            var updated     = item with
            {
                Stock          = newStock,
                TotalOrderCost = newStock * item.CostPerUnit,
                Status         = DataService.ComputeStatus(newStock, item.ReorderLevel)
            };

            data.UpdateInventoryItem(updated);
            data.AppendSale(new SalesRecord(
                DateTime.UtcNow, Purchase.Customer, item.SKU,
                item.ProductName, Purchase.Quantity, unitPrice, totalAmount));

            PurchaseSuccess = true;
            PurchaseMessage = $"Sale recorded. {Purchase.Quantity} × {item.ProductName} → {totalAmount:C0}";
            Purchase = new();
            ModelState.Clear();
            Load();
        }
        catch (Exception ex)
        {
            PurchaseMessage = $"Error processing purchase: {ex.Message}";
        }

        return Page();
    }

    private void Load()
    {
        try
        {
            Categories = data.GetCategories();
            Suppliers  = data.GetSuppliers();
            AllItems   = data.ReadInventory();
            Items      = data.ReadInventory(CategoryFilter, SupplierFilter, search: Search);
        }
        catch
        {
            Categories = [];
            Suppliers  = [];
            AllItems   = [];
            Items      = [];
        }
    }
}
