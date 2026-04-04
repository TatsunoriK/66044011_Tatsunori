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

    // ── Helpers ──────────────────────────────────────────────
    private string? SessionUser => HttpContext.Session.GetString("Username");
    private int? SessionUserId => HttpContext.Session.GetInt32("UserId");
    private int? SessionRoleId => HttpContext.Session.GetInt32("RoleId");

    // =========================
    // LOGIN
    // =========================
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

    // ── รองรับ URL เดิมที่เคยใช้ Project1 ──────────────────
    public IActionResult Project1() => RedirectToAction("ProductList");
    public IActionResult Index()    => RedirectToAction("ProductList");

    // =========================
    // REGISTER
    // =========================
    public IActionResult Register()
    {
        if (SessionUser != null) return RedirectToAction("ProductList");
        return View();
    }

    [HttpPost]
    public IActionResult Register(User user, string Gender, DateOnly Birthday, string Address, string Phone)
    {
        if (_db.Users.Any(u => u.Username == user.Username))
        {
            ViewBag.Error = "Username นี้ถูกใช้งานแล้ว";
            return View();
        }
        if (_db.Users.Any(u => u.Email == user.Email))
        {
            ViewBag.Error = "Email นี้ถูกใช้งานแล้ว";
            return View();
        }

        user.RoleId = 4; // Customer
        user.CreatedAt = DateTime.Now;
        _db.Users.Add(user);
        _db.SaveChanges();

        _db.Userprofiles.Add(new Userprofile
        {
            UserId = user.Id,
            Gender = Gender,
            Birthday = Birthday,
            Address = Address,
            Tel = Phone
        });
        _db.SaveChanges();

        return RedirectToAction("Login");
    }

    // =========================
    // PRODUCT LIST (หน้าหลัก)
    // =========================
    public IActionResult ProductList(string? search, int? catId, int? brandId,
        decimal? minPrice, decimal? maxPrice, string? sort)
    {
        var query = _db.Products
            .Include(p => p.Cat)
            .Include(p => p.Brand)
            .Include(p => p.Productstock)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Pname.Contains(search));
        if (catId.HasValue)
            query = query.Where(p => p.CatId == catId);
        if (brandId.HasValue)
            query = query.Where(p => p.BrandId == brandId);
        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice);
        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice);

        query = sort switch
        {
            "price_asc"  => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            _            => query.OrderBy(p => p.Pid)
        };

        ViewBag.Categories = _db.Categories.ToList();
        ViewBag.Brands      = _db.Brands.ToList();
        ViewBag.Search      = search;
        ViewBag.CatId       = catId;
        ViewBag.BrandId     = brandId;
        ViewBag.MinPrice    = minPrice;
        ViewBag.MaxPrice    = maxPrice;
        ViewBag.Sort        = sort;
        ViewBag.Username    = SessionUser;
        ViewBag.RoleId      = SessionRoleId;

        return View(query.ToList());
    }

    // =========================
    // CART
    // =========================
    public IActionResult CartPage()
    {
        if (SessionUser == null) return RedirectToAction("Login");

        var cart = GetCart();
        if (!cart.Any()) return View(new List<CartItemViewModel>());

        var pids     = cart.Keys.ToList();
        var products = _db.Products.Where(p => pids.Contains(p.Pid)).ToList();

        var items = products.Select(p => new CartItemViewModel
        {
            Pid      = p.Pid,
            Pname    = p.Pname,
            Price    = p.Price,
            Qty      = cart[p.Pid],
            Subtotal = p.Price * cart[p.Pid]
        }).ToList();

        ViewBag.Username = SessionUser;
        return View(items);
    }

    [HttpPost]
    public IActionResult AddToCart(int pid, int qty = 1)
    {
        if (SessionUser == null) return RedirectToAction("Login");
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
        if (qty <= 0) cart.Remove(pid);
        else          cart[pid] = qty;
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

    // =========================
    // CHECKOUT / PLACE ORDER
    // =========================
    [HttpPost]
    public IActionResult PlaceOrder()
    {
        if (SessionUser == null) return RedirectToAction("Login");

        var cart = GetCart();
        if (!cart.Any()) return RedirectToAction("CartPage");

        var pids     = cart.Keys.ToList();
        var products = _db.Products.Where(p => pids.Contains(p.Pid)).ToList();
        decimal total = products.Sum(p => p.Price * cart[p.Pid]);

        var order = new Order
        {
            UserId      = SessionUserId,
            OrderDate   = DateTime.Now,
            TotalAmount = total,
            Status      = "Pending"
        };
        _db.Orders.Add(order);
        _db.SaveChanges();

        foreach (var p in products)
        {
            _db.Orderdetails.Add(new Orderdetail
            {
                OrderId   = order.OrderId,
                Pid       = p.Pid,
                Qty       = cart[p.Pid],
                UnitPrice = p.Price
            });
        }
        _db.SaveChanges();

        HttpContext.Session.Remove("Cart");
        TempData["OrderSuccess"] = $"สั่งซื้อสำเร็จ! หมายเลขออเดอร์: #{order.OrderId}";
        return RedirectToAction("OrderHistory");
    }

    // =========================
    // ORDER HISTORY
    // =========================
    public IActionResult OrderHistory()
    {
        if (SessionUser == null) return RedirectToAction("Login");

        var orders = _db.Orders
            .Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation)
            .Where(o => o.UserId == SessionUserId)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        ViewBag.Username = SessionUser;
        return View(orders);
    }

    // =========================
    // MANAGEMENT (Admin/Manager)
    // =========================
    public IActionResult Management()
    {
        if (SessionRoleId > 2) return RedirectToAction("ProductList");

        var users = _db.Users
            .Include(u => u.Role)
            .Include(u => u.Userprofile)
            .ToList();
        ViewBag.Roles    = _db.Roles.ToList();
        ViewBag.Username = SessionUser;
        return View(users);
    }

    public IActionResult DeleteUser(int id)
    {
        if (SessionRoleId > 2) return RedirectToAction("ProductList");
        var user = _db.Users.Find(id);
        if (user != null) { _db.Users.Remove(user); _db.SaveChanges(); }
        return RedirectToAction("Management");
    }

    [HttpPost]
    public IActionResult ChangeRole(int userId, int roleId)
    {
        if (SessionRoleId > 2) return RedirectToAction("ProductList");
        var user = _db.Users.Find(userId);
        if (user != null) { user.RoleId = roleId; _db.SaveChanges(); }
        return RedirectToAction("Management");
    }

    public IActionResult EditUser(int id)
    {
        if (SessionRoleId > 2) return RedirectToAction("ProductList");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == id);
        if (user == null) return NotFound();
        ViewBag.Username = SessionUser;
        return View(user);
    }

    [HttpPost]
    public IActionResult EditUser(User data)
    {
        if (SessionRoleId > 2) return RedirectToAction("ProductList");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == data.Id);
        if (user == null) return NotFound();

        user.FullName = data.FullName ?? user.FullName;
        user.Email    = data.Email    ?? user.Email;

        if (user.Userprofile != null && data.Userprofile != null)
        {
            user.Userprofile.Tel      = data.Userprofile.Tel;
            user.Userprofile.Address  = data.Userprofile.Address;
            user.Userprofile.Gender   = data.Userprofile.Gender;
            user.Userprofile.Birthday = data.Userprofile.Birthday;
        }
        _db.Update(user);
        _db.SaveChanges();
        return RedirectToAction("Management");
    }

    // =========================
    // HELPERS - Cart Session
    // =========================
    private Dictionary<int, int> GetCart()
    {
        var raw = HttpContext.Session.GetString("Cart");
        return string.IsNullOrEmpty(raw)
            ? new Dictionary<int, int>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(raw)!;
    }

    private void SaveCart(Dictionary<int, int> cart) =>
        HttpContext.Session.SetString("Cart",
            System.Text.Json.JsonSerializer.Serialize(cart));

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}