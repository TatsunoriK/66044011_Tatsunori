using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using _66044011_Tatsunori.Models;
using _66044011_Tatsunori.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace _66044011_Tatsunori.Controllers;

public class ProjectController : Controller
{
    private readonly Csi402dbContext _db;
    public ProjectController(Csi402dbContext db) { _db = db; }

    private string? SessionUser => HttpContext.Session.GetString("Username");
    private int? SessionUserId => HttpContext.Session.GetInt32("UserId");
    private int? SessionRoleId => HttpContext.Session.GetInt32("RoleId");

    public IActionResult Login()
    {
        if (SessionUser != null) return RedirectToAction("ProductList");
        return View();
    }

    [HttpPost]
    public IActionResult Login(LoginViewModels data)
    {
        var user = _db.Users.Include(u => u.Role)
            .FirstOrDefault(u => u.Username == data.UserId && u.Password == data.Password);
        if (user != null)
        {
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetInt32("RoleId", user.RoleId ?? 5);
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Username);
            return RedirectToAction("ProductList");
        }
        ViewBag.Error = "Username หรือ Password ไม่ถูกต้อง";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    public IActionResult Project1() => RedirectToAction("ProductList");
    public IActionResult Index() => RedirectToAction("ProductList");

    public IActionResult Register()
    {
        if (SessionUser != null) return RedirectToAction("ProductList");
        return View();
    }

    [HttpPost]
    public IActionResult Register(User user, string Gender, DateOnly Birthday, string Address, string Phone)
    {
        if (_db.Users.Any(u => u.Username == user.Username)) { ViewBag.Error = "Username นี้ถูกใช้งานแล้ว"; return View(); }
        if (_db.Users.Any(u => u.Email == user.Email)) { ViewBag.Error = "Email นี้ถูกใช้งานแล้ว"; return View(); }

        user.RoleId = 4;
        user.CreatedAt = DateTime.Now;
        _db.Users.Add(user);
        _db.SaveChanges();

        _db.Userprofiles.Add(new Userprofile { UserId = user.Id, Gender = Gender, Birthday = Birthday, Address = Address, Tel = Phone });
        _db.SaveChanges();
        return RedirectToAction("Login");
    }

    // PRODUCT LIST
    public IActionResult ProductList(string? search, int? catId, int? brandId, decimal? minPrice, decimal? maxPrice, string? sort)
    {
        var query = _db.Products.Include(p => p.Cat).Include(p => p.Brand).Include(p => p.Productstock).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Pname.Contains(search));
        if (catId.HasValue) query = query.Where(p => p.CatId == catId);
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
        if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice);
        if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice);

        query = sort switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            _ => query.OrderBy(p => p.Pid)
        };

        ViewBag.Categories = _db.Categories.ToList();
        ViewBag.Brands = _db.Brands.ToList();
        ViewBag.Search = search; ViewBag.CatId = catId; ViewBag.BrandId = brandId;
        ViewBag.MinPrice = minPrice; ViewBag.MaxPrice = maxPrice; ViewBag.Sort = sort;
        ViewBag.Username = SessionUser; ViewBag.RoleId = SessionRoleId;

        return View("Project1", query.ToList());
    }

    // CART
    public IActionResult CartPage()
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var cart = GetCart();
        if (!cart.Any()) return View(new List<CartItemViewModel>());
        var pids = cart.Keys.ToList();
        var products = _db.Products.Where(p => pids.Contains(p.Pid)).ToList();
        var items = products.Select(p => new CartItemViewModel { Pid = p.Pid, Pname = p.Pname, Price = p.Price, Qty = cart[p.Pid], Subtotal = p.Price * cart[p.Pid] }).ToList();
        return View(items);
    }

    [HttpPost]
    public IActionResult AddToCart(int pid, int qty = 1)
    {
        if (SessionUser == null) return RedirectToAction("Login");
        if (SessionRoleId != 4) { TempData["CartMsg"] = "เฉพาะ Customer เท่านั้นที่สามารถซื้อสินค้าได้"; return RedirectToAction("ProductList"); }
        var cart = GetCart();
        cart[pid] = cart.ContainsKey(pid) ? cart[pid] + qty : qty;
        SaveCart(cart);
        TempData["CartMsg"] = "เพิ่มสินค้าลงตะกร้าแล้ว";
        return RedirectToAction("CartPage");
    }

    [HttpPost]
    public IActionResult UpdateCart(int pid, int qty)
    {
        var cart = GetCart();
        if (qty <= 0) cart.Remove(pid); else cart[pid] = qty;
        SaveCart(cart);
        return RedirectToAction("CartPage");
    }

    [HttpPost]
    public IActionResult RemoveFromCart(int pid)
    {
        var cart = GetCart();
        cart.Remove(pid);
        SaveCart(cart);
        return RedirectToAction("CartPage");
    }

    [HttpPost]
    public IActionResult PlaceOrder()
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var cart = GetCart();
        if (!cart.Any()) return RedirectToAction("CartPage");
        var pids = cart.Keys.ToList();
        var products = _db.Products.Where(p => pids.Contains(p.Pid)).ToList();
        decimal total = products.Sum(p => p.Price * cart[p.Pid]);

        var order = new Order { UserId = SessionUserId, OrderDate = DateTime.Now, TotalAmount = total, Status = "Pending" };
        _db.Orders.Add(order);
        _db.SaveChanges();

        foreach (var p in products)
            _db.Orderdetails.Add(new Orderdetail { OrderId = order.OrderId, Pid = p.Pid, Qty = cart[p.Pid], UnitPrice = p.Price });
        _db.SaveChanges();

        HttpContext.Session.Remove("Cart");
        TempData["OrderSuccess"] = $"สั่งซื้อสำเร็จ! หมายเลขออเดอร์: #{order.OrderId}";
        return RedirectToAction("OrderHistory");
    }

    // ORDER HISTORY
    public IActionResult OrderHistory()
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var orders = _db.Orders
            .Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation)
            .Where(o => o.UserId == SessionUserId)
            .OrderByDescending(o => o.OrderDate)
            .ToList();
        return View(orders);
    }

    // DASHBOARD
    public IActionResult Dashboard()
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var now = DateTime.Today;
        var month = new DateTime(now.Year, now.Month, 1);

        ViewBag.TotalUsers = _db.Users.Count();
        ViewBag.TotalProducts = _db.Products.Count();
        ViewBag.TotalOrders = _db.Orders.Count();
        ViewBag.PendingOrders = _db.Orders.Count(o => o.Status == "Pending");
        ViewBag.TotalRevenue = _db.Orders.Where(o => o.Status != "Cancelled").Sum(o => (decimal?)o.TotalAmount) ?? 0;
        ViewBag.MonthRevenue = _db.Orders.Where(o => o.Status != "Cancelled" && o.OrderDate >= month).Sum(o => (decimal?)o.TotalAmount) ?? 0;
        ViewBag.LowStock = _db.Productstocks.Count(s => (s.Quantity ?? 0) > 0 && (s.Quantity ?? 0) <= 5);
        ViewBag.OutOfStock = _db.Productstocks.Count(s => (s.Quantity ?? 0) <= 0);

        ViewBag.RecentOrders = _db.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate).Take(5).ToList();

        ViewBag.TopProducts = _db.Orderdetails
            .Include(d => d.PidNavigation)
            .GroupBy(d => d.PidNavigation!.Pname)
            .Select(g => new { Name = g.Key, Qty = g.Sum(d => d.Qty) })
            .OrderByDescending(x => x.Qty).Take(5).ToList();

        ViewBag.AlertStocks = _db.Productstocks
            .Include(s => s.PidNavigation)
            .Where(s => (s.Quantity ?? 0) <= 5)
            .OrderBy(s => s.Quantity).Take(5).ToList();

        return View();
    }

    // MANAGEMENT
    public IActionResult Management()
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var users = _db.Users.Include(u => u.Role).Include(u => u.Userprofile).ToList();
        ViewBag.Roles = _db.Roles.ToList();
        return View(users);
    }

    public IActionResult DeleteUser(int id)
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var user = _db.Users.Find(id);
        if (user != null) { _db.Users.Remove(user); _db.SaveChanges(); }
        return RedirectToAction("Management");
    }

    [HttpPost]
    public IActionResult ChangeRole(int userId, int roleId)
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var user = _db.Users.Find(userId);
        if (user != null) { user.RoleId = roleId; _db.SaveChanges(); }
        return RedirectToAction("Management");
    }

    public IActionResult EditUser(int id)
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == id);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public IActionResult EditUser(User data)
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == data.Id);
        if (user == null) return NotFound();
        user.FullName = data.FullName ?? user.FullName;
        user.Email = data.Email ?? user.Email;
        if (user.Userprofile != null && data.Userprofile != null)
        {
            user.Userprofile.Tel = data.Userprofile.Tel;
            user.Userprofile.Address = data.Userprofile.Address;
            user.Userprofile.Gender = data.Userprofile.Gender;
            user.Userprofile.Birthday = data.Userprofile.Birthday;
        }
        _db.Update(user);
        _db.SaveChanges();
        return RedirectToAction("Management");
    }

    // ORDER MANAGEMENT
    public IActionResult OrderManagement(string? status)
    {
        if (SessionRoleId != 1 && SessionRoleId != 2 && SessionRoleId != 3) return RedirectToAction("ProductList");
        var query = _db.Orders.Include(o => o.User).Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);

        ViewBag.StatusFilter = status;
        ViewBag.AllCount = _db.Orders.Count();
        ViewBag.PendingCount = _db.Orders.Count(o => o.Status == "Pending");
        ViewBag.PaidCount = _db.Orders.Count(o => o.Status == "Paid");
        ViewBag.ShippedCount = _db.Orders.Count(o => o.Status == "Shipped");
        ViewBag.CancelCount = _db.Orders.Count(o => o.Status == "Cancelled");
        return View(query.OrderByDescending(o => o.OrderDate).ToList());
    }

    [HttpPost]
    public IActionResult UpdateOrderStatus(int orderId, string newStatus)
    {
        if (SessionRoleId != 1 && SessionRoleId != 2 && SessionRoleId != 3) return RedirectToAction("ProductList");
        var order = _db.Orders.Include(o => o.Orderdetails).FirstOrDefault(o => o.OrderId == orderId);
        if (order != null)
        {
            order.Status = newStatus;
            if (newStatus == "Shipped")
            {
                foreach (var detail in order.Orderdetails)
                {
                    var stock = _db.Productstocks.Find(detail.Pid);
                    if (stock != null) { stock.Quantity = Math.Max(0, (stock.Quantity ?? 0) - detail.Qty); stock.LastUpdate = DateTime.Now; }
                }
            }
            _db.SaveChanges();
            TempData["Success"] = $"อัปเดตสถานะออเดอร์ #{orderId} เป็น {newStatus} เรียบร้อย";
        }
        return RedirectToAction("OrderManagement");
    }

    // STOCK
    public IActionResult StockList()
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");
        var stocks = _db.Productstocks.Include(s => s.PidNavigation).ThenInclude(p => p.Cat)
            .Include(s => s.PidNavigation).ThenInclude(p => p.Brand).OrderBy(s => s.PidNavigation.Pname).ToList();
        return View(stocks);
    }

    [HttpPost]
    public IActionResult UpdateStock(int pid, int quantity)
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");

        var stock = _db.Productstocks.Find(pid);
        var product = _db.Products.Find(pid);

        if (stock != null)
        {
            int oldQty = stock.Quantity ?? 0;
            string note = quantity > oldQty ? $"เพิ่มสต็อก +{quantity - oldQty}"
                        : quantity < oldQty ? $"ลดสต็อก -{oldQty - quantity}"
                        : "ไม่มีการเปลี่ยนแปลง";

            _db.Stockhistories.Add(new Stockhistory
            {
                Pid = pid,
                OldQty = oldQty,
                NewQty = quantity,
                ChangedBy = SessionUser,
                ChangedAt = DateTime.Now,
                Note = note
            });

            stock.Quantity = quantity;
            stock.LastUpdate = DateTime.Now;
            _db.SaveChanges();

            TempData["Success"] = $"อัปเดตสต็อก {product?.Pname} ({oldQty} → {quantity}) เรียบร้อย";
        }
        return RedirectToAction("StockList");
    }

    public IActionResult StockHistory(int? pid)
    {
        if (SessionRoleId != 1) return RedirectToAction("ProductList");

        var query = _db.Stockhistories
            .Include(h => h.PidNavigation)
            .AsQueryable();

        if (pid.HasValue)
            query = query.Where(h => h.Pid == pid.Value);

        ViewBag.FilterPid = pid;
        ViewBag.Product = pid.HasValue ? _db.Products.Find(pid.Value) : null;
        ViewBag.Products = _db.Products.OrderBy(p => p.Pname).ToList();

        return View(query.OrderByDescending(h => h.ChangedAt).ToList());
    }

    // SALES REPORT
    public IActionResult SalesReport(string period = "monthly")
    {
        if (SessionRoleId != 1 && SessionRoleId != 2) return RedirectToAction("ProductList");
        DateTime start, end;
        var now = DateTime.Today;
        switch (period)
        {
            case "daily": start = now; end = now; break;
            case "yearly": start = new DateTime(now.Year, 1, 1); end = new DateTime(now.Year, 12, 31); break;
            default: start = new DateTime(now.Year, now.Month, 1); end = start.AddMonths(1).AddDays(-1); break;
        }

        var orders = _db.Orders.Include(o => o.User).Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation)
            .Where(o => o.OrderDate >= start && o.OrderDate <= end.AddDays(1) && o.Status != "Cancelled")
            .OrderByDescending(o => o.OrderDate).ToList();

        ViewBag.Period = period; ViewBag.Start = start; ViewBag.End = end;
        ViewBag.TotalRevenue = orders.Sum(o => o.TotalAmount);
        ViewBag.TotalOrders = orders.Count;
        ViewBag.TotalItems = orders.SelectMany(o => o.Orderdetails).Sum(d => d.Qty);
        ViewBag.TopProducts = orders.SelectMany(o => o.Orderdetails)
            .GroupBy(d => d.PidNavigation?.Pname ?? $"#{d.Pid}")
            .Select(g => new { Name = g.Key, Qty = g.Sum(d => d.Qty), Revenue = g.Sum(d => d.UnitPrice * d.Qty) })
            .OrderByDescending(x => x.Revenue).Take(5).ToList();
        return View(orders);
    }

    private Dictionary<int, int> GetCart()
    {
        var raw = HttpContext.Session.GetString("Cart");
        return string.IsNullOrEmpty(raw) ? new Dictionary<int, int>() : System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(raw)!;
    }

    private void SaveCart(Dictionary<int, int> cart) =>
        HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
