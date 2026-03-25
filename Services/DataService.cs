using Npgsql;

namespace IslandCore.Services;

// ── Models ────────────────────────────────────────────────────────────────────

public record UserRecord(
    string Username, string Email, string FirstName, string LastName,
    string Telephone, string City, string Gender, string Password,
    DateTime CreatedUtc);

public record InventoryItem(
    string SKU, string ProductName, string Category,
    int Stock, int ReorderLevel, string Supplier,
    string Status, int CostPerUnit, int TotalOrderCost, DateTime CreatedUtc);

public record SalesRecord(
    DateTime CreatedUtc, string Customer, string SKU,
    string ProductName, int Quantity, int UnitPrice, int TotalAmount);

// ── Service ───────────────────────────────────────────────────────────────────

public class DataService
{
    private readonly string _connStr;

    public DataService(IConfiguration config)
    {
        _connStr = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing 'DefaultConnection' in appsettings.json");
    }

    private NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(_connStr);
        conn.Open();
        return conn;
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public UserRecord? ValidateLogin(string username, string password)
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand(@"
            SELECT username, email, first_name, last_name, telephone, city, gender, password_hash, created_utc
            FROM users WHERE lower(username) = lower(@u)", conn);
        cmd.Parameters.AddWithValue("u", username);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read()) return null;
        var user = MapUser(rdr);
        bool valid = user.Password.StartsWith("$2")
            ? BCrypt.Net.BCrypt.Verify(password, user.Password)
            : user.Password == password;
        return valid ? user : null;
    }

    public (bool ok, string? error) CreateUser(UserRecord u)
    {
        using var conn = Open();
        using (var chk = new NpgsqlCommand(
            "SELECT 1 FROM users WHERE lower(username)=lower(@u) OR lower(email)=lower(@e)", conn))
        {
            chk.Parameters.AddWithValue("u", u.Username);
            chk.Parameters.AddWithValue("e", u.Email);
            using var r = chk.ExecuteReader();
            if (r.Read()) return (false, "Username or email already exists.");
        }
        var hash = BCrypt.Net.BCrypt.HashPassword(u.Password);
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO users(username,email,first_name,last_name,telephone,city,gender,password_hash,created_utc)
            VALUES(@username,@email,@first,@last,@tel,@city,@gender,@hash,now())", conn);
        cmd.Parameters.AddWithValue("username", u.Username);
        cmd.Parameters.AddWithValue("email",    u.Email);
        cmd.Parameters.AddWithValue("first",    u.FirstName);
        cmd.Parameters.AddWithValue("last",     u.LastName);
        cmd.Parameters.AddWithValue("tel",      u.Telephone ?? "");
        cmd.Parameters.AddWithValue("city",     u.City ?? "");
        cmd.Parameters.AddWithValue("gender",   u.Gender ?? "");
        cmd.Parameters.AddWithValue("hash",     hash);
        cmd.ExecuteNonQuery();
        return (true, null);
    }

    private static UserRecord MapUser(NpgsqlDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
        r.IsDBNull(4) ? "" : r.GetString(4),
        r.IsDBNull(5) ? "" : r.GetString(5),
        r.IsDBNull(6) ? "" : r.GetString(6),
        r.GetString(7), r.GetDateTime(8));

    // ── Inventory ─────────────────────────────────────────────────────────────

    public List<InventoryItem> ReadInventory(
        string? category = null, string? supplier = null, string? statusFilter = null, string? search = null)
    {
        using var conn = Open();
        var sql = @"SELECT sku, product_name, category, stock, reorder_level,
                           supplier, status, cost_per_unit, total_order_cost, created_utc
                    FROM inventory WHERE 1=1";
        if (!string.IsNullOrEmpty(category)) sql += " AND lower(category)=lower(@cat)";
        if (!string.IsNullOrEmpty(supplier)) sql += " AND lower(supplier)=lower(@sup)";
        if (!string.IsNullOrEmpty(search))   sql += " AND (lower(sku) LIKE lower(@search) OR lower(product_name) LIKE lower(@search))";
        if (!string.IsNullOrEmpty(statusFilter)) sql += statusFilter switch {
            "In Stock"     => " AND stock > reorder_level",
            "Low Stock"    => " AND stock > 0 AND stock <= reorder_level",
            "Out of Stock" => " AND stock <= 0",
            _              => ""
        };
        sql += " ORDER BY product_name";

        using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrEmpty(category)) cmd.Parameters.AddWithValue("cat",    category);
        if (!string.IsNullOrEmpty(supplier)) cmd.Parameters.AddWithValue("sup",    supplier);
        if (!string.IsNullOrEmpty(search))   cmd.Parameters.AddWithValue("search", $"%{search}%");

        var list = new List<InventoryItem>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapInventory(rdr));
        return list;
    }

    public InventoryItem? GetInventoryItem(string sku)
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand(@"
            SELECT sku, product_name, category, stock, reorder_level,
                   supplier, status, cost_per_unit, total_order_cost, created_utc
            FROM inventory WHERE lower(sku)=lower(@sku)", conn);
        cmd.Parameters.AddWithValue("sku", sku);
        using var rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapInventory(rdr) : null;
    }

    public List<string> GetCategories() => GetDistinct(
        "SELECT DISTINCT category FROM inventory WHERE category IS NOT NULL AND category <> '' ORDER BY category");

    public List<string> GetSuppliers() => GetDistinct(
        "SELECT DISTINCT supplier FROM inventory WHERE supplier IS NOT NULL AND supplier <> '' ORDER BY supplier");

    private List<string> GetDistinct(string sql)
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        var list = new List<string>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(rdr.GetString(0));
        return list;
    }

    public (bool ok, string? error) AddInventoryItem(InventoryItem i)
    {
        using var conn = Open();
        using (var chk = new NpgsqlCommand("SELECT 1 FROM inventory WHERE lower(sku)=lower(@sku)", conn))
        {
            chk.Parameters.AddWithValue("sku", i.SKU);
            using var r = chk.ExecuteReader();
            if (r.Read()) return (false, $"SKU '{i.SKU}' already exists.");
        }
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO inventory(sku,product_name,category,stock,reorder_level,supplier,status,cost_per_unit,total_order_cost,created_utc)
            VALUES(@sku,@name,@cat,@stock,@reorder,@sup,@status,@cpu,@toc,now())", conn);
        SetInventoryParams(cmd, i);
        cmd.ExecuteNonQuery();
        return (true, null);
    }

    public void AppendInventory(InventoryItem i) => AddInventoryItem(i);

    public void UpdateInventoryItem(InventoryItem i)
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand(@"
            UPDATE inventory SET
                product_name=@name, category=@cat, stock=@stock,
                reorder_level=@reorder, supplier=@sup, status=@status,
                cost_per_unit=@cpu, total_order_cost=@toc
            WHERE lower(sku)=lower(@sku)", conn);
        SetInventoryParams(cmd, i);
        cmd.ExecuteNonQuery();
    }

    public void DeleteInventoryItem(string sku)
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand("DELETE FROM inventory WHERE lower(sku)=lower(@sku)", conn);
        cmd.Parameters.AddWithValue("sku", sku);
        cmd.ExecuteNonQuery();
    }

    // Used by Purchasing — updates only the single purchased item instead of full list
    public void SaveInventory(IEnumerable<InventoryItem> items)
    {
        foreach (var i in items) UpdateInventoryItem(i);
    }

    private static void SetInventoryParams(NpgsqlCommand cmd, InventoryItem i)
    {
        cmd.Parameters.AddWithValue("sku",     i.SKU);
        cmd.Parameters.AddWithValue("name",    i.ProductName);
        cmd.Parameters.AddWithValue("cat",     i.Category   ?? "");
        cmd.Parameters.AddWithValue("stock",   i.Stock);
        cmd.Parameters.AddWithValue("reorder", i.ReorderLevel);
        cmd.Parameters.AddWithValue("sup",     i.Supplier   ?? "");
        cmd.Parameters.AddWithValue("status",  i.Status     ?? "");
        cmd.Parameters.AddWithValue("cpu",     i.CostPerUnit);
        cmd.Parameters.AddWithValue("toc",     i.TotalOrderCost);
    }

    private static InventoryItem MapInventory(NpgsqlDataReader r) => new(
        r.GetString(0), r.GetString(1),
        r.IsDBNull(2) ? "" : r.GetString(2),
        r.GetInt32(3), r.GetInt32(4),
        r.IsDBNull(5) ? "" : r.GetString(5),
        r.IsDBNull(6) ? "" : r.GetString(6),
        r.GetInt32(7), r.GetInt32(8),
        r.GetDateTime(9));

    // ── Sales ─────────────────────────────────────────────────────────────────

    public List<SalesRecord> ReadSales(
        string? customer = null, DateTime? from = null, DateTime? to = null)
    {
        using var conn = Open();
        var sql = @"SELECT created_utc, customer, sku, product_name, quantity, unit_price, total_amount
                    FROM sales WHERE 1=1";
        if (!string.IsNullOrEmpty(customer)) sql += " AND lower(customer) LIKE lower(@cust)";
        if (from.HasValue) sql += " AND created_utc >= @from";
        if (to.HasValue)   sql += " AND created_utc <= @to";
        sql += " ORDER BY created_utc DESC";

        using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrEmpty(customer)) cmd.Parameters.AddWithValue("cust", $"%{customer}%");
        if (from.HasValue) cmd.Parameters.AddWithValue("from", from.Value);
        if (to.HasValue)   cmd.Parameters.AddWithValue("to",   to.Value);

        var list = new List<SalesRecord>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read()) list.Add(MapSale(rdr));
        return list;
    }

    public void AppendSale(SalesRecord s)
    {
        using var conn = Open();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO sales(created_utc,customer,sku,product_name,quantity,unit_price,total_amount)
            VALUES(now(),@customer,@sku,@name,@qty,@unit,@total)", conn);
        cmd.Parameters.AddWithValue("customer", s.Customer    ?? "");
        cmd.Parameters.AddWithValue("sku",      s.SKU         ?? "");
        cmd.Parameters.AddWithValue("name",     s.ProductName ?? "");
        cmd.Parameters.AddWithValue("qty",      s.Quantity);
        cmd.Parameters.AddWithValue("unit",     s.UnitPrice);
        cmd.Parameters.AddWithValue("total",    s.TotalAmount);
        cmd.ExecuteNonQuery();
    }

    private static SalesRecord MapSale(NpgsqlDataReader r) => new(
        r.GetDateTime(0),
        r.IsDBNull(1) ? "" : r.GetString(1),
        r.IsDBNull(2) ? "" : r.GetString(2),
        r.IsDBNull(3) ? "" : r.GetString(3),
        r.GetInt32(4), r.GetInt32(5), r.GetInt32(6));

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string ComputeStatus(int stock, int reorder) =>
        stock <= 0 ? "Out of Stock" : stock <= reorder ? "Low Stock" : "In Stock";
}
