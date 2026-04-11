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

    private string? SessionUser   => HttpContext.Session.GetString("Username");
    private int?    SessionUserId => HttpContext.Session.GetInt32("UserId");
    private int?    SessionRoleId => HttpContext.Session.GetInt32("RoleId");

    private bool IsAdmin    => SessionRoleId == 1;
    private bool IsManager  => SessionRoleId == 2;
    private bool IsStaff    => SessionRoleId == 3;
    private bool IsCustomer => SessionRoleId == 4;
    private bool CanManageOrder => IsAdmin || IsManager || IsStaff;
    private bool CanManageStock => IsAdmin || IsManager;
    private bool CanViewReport  => IsAdmin || IsManager;

    // ─── helpers ────────────────────────────────────────────────────────────
    private Dictionary<int, int> GetCart()
    {
        var raw = HttpContext.Session.GetString("Cart");
        return string.IsNullOrEmpty(raw) ? new Dictionary<int, int>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, int>>(raw)!;
    }
    private void SaveCart(Dictionary<int, int> cart) =>
        HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));

    private static string CalcMemberLevel(int points) => points switch
    {
        >= 5000 => "Gold",
        >= 1000 => "Silver",
        _       => "Bronze"
    };

    // effective price (flash sale หรือราคาปกติ)
    private decimal EffectivePrice(int pid, decimal originalPrice)
    {
        var now  = DateTime.Now;
        var sale = _db.Flashsales.FirstOrDefault(f =>
            f.Pid == pid && f.IsActive && f.StartTime <= now && f.EndTime >= now);
        return sale?.SalePrice ?? originalPrice;
    }

    // ─── LOGIN / LOGOUT / REGISTER ─────────────────────────────────────────
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
            HttpContext.Session.SetString("Username",    user.Username);
            HttpContext.Session.SetInt32 ("UserId",      user.Id);
            HttpContext.Session.SetInt32 ("RoleId",      user.RoleId ?? 4);
            HttpContext.Session.SetString("FullName",    user.FullName ?? user.Username);
            HttpContext.Session.SetString("MemberLevel", user.MemberLevel);
            HttpContext.Session.SetInt32 ("Points",      user.Points);
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
    public IActionResult Index()    => RedirectToAction("ProductList");

    public IActionResult Register()
    {
        if (SessionUser != null) return RedirectToAction("ProductList");
        return View();
    }

    [HttpPost]
    public IActionResult Register(User user, string Gender, DateOnly Birthday, string Address, string Phone)
    {
        if (_db.Users.Any(u => u.Username == user.Username)) { ViewBag.Error = "Username นี้ถูกใช้งานแล้ว"; return View(); }
        if (_db.Users.Any(u => u.Email    == user.Email))    { ViewBag.Error = "Email นี้ถูกใช้งานแล้ว";    return View(); }
        user.RoleId      = 4;
        user.CreatedAt   = DateTime.Now;
        user.Points      = 0;
        user.MemberLevel = "Bronze";
        _db.Users.Add(user);
        _db.SaveChanges();
        _db.Userprofiles.Add(new Userprofile { UserId = user.Id, Gender = Gender, Birthday = Birthday, Address = Address, Tel = Phone });
        _db.SaveChanges();
        return RedirectToAction("Login");
    }

    // ─── PRODUCT LIST ───────────────────────────────────────────────────────
    public IActionResult ProductList(string? search, int? catId, int? brandId,
        decimal? minPrice, decimal? maxPrice, string? sort)
    {
        var query = _db.Products
            .Include(p => p.Cat).Include(p => p.Brand).Include(p => p.Productstock)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Pname.Contains(search));
        if (catId.HasValue)    query = query.Where(p => p.CatId   == catId);
        if (brandId.HasValue)  query = query.Where(p => p.BrandId == brandId);
        if (minPrice.HasValue) query = query.Where(p => p.Price   >= minPrice);
        if (maxPrice.HasValue) query = query.Where(p => p.Price   <= maxPrice);

        query = sort switch
        {
            "price_asc"  => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            _            => query.OrderBy(p => p.Pid)
        };

        var now        = DateTime.Now;
        var flashPids  = _db.Flashsales
            .Where(f => f.IsActive && f.StartTime <= now && f.EndTime >= now)
            .Select(f => f.Pid).ToHashSet();

        ViewBag.Categories = _db.Categories.ToList();
        ViewBag.Brands     = _db.Brands.ToList();
        ViewBag.Search     = search; ViewBag.CatId = catId; ViewBag.BrandId = brandId;
        ViewBag.MinPrice   = minPrice; ViewBag.MaxPrice = maxPrice; ViewBag.Sort = sort;
        ViewBag.Username   = SessionUser; ViewBag.RoleId = SessionRoleId;
        ViewBag.FlashPids  = flashPids;
        ViewBag.Flashsales = _db.Flashsales
            .Where(f => f.IsActive && f.StartTime <= now && f.EndTime >= now)
            .ToDictionary(f => f.Pid, f => f.SalePrice);

        return View("Project1", query.ToList());
    }

    // ─── CART ───────────────────────────────────────────────────────────────
    public IActionResult CartPage()
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var cart     = GetCart();
        var pids     = cart.Keys.ToList();
        var products = _db.Products.Where(p => pids.Contains(p.Pid)).ToList();
        var now      = DateTime.Now;
        var flashMap = _db.Flashsales
            .Where(f => f.IsActive && f.StartTime <= now && f.EndTime >= now && pids.Contains(f.Pid))
            .ToDictionary(f => f.Pid, f => f.SalePrice);

        var items = products.Select(p => new CartItemViewModel
        {
            Pid      = p.Pid,
            Pname    = p.Pname,
            Price    = flashMap.ContainsKey(p.Pid) ? flashMap[p.Pid] : p.Price,
            Qty      = cart[p.Pid],
            Subtotal = (flashMap.ContainsKey(p.Pid) ? flashMap[p.Pid] : p.Price) * cart[p.Pid]
        }).ToList();

        var profile = _db.Userprofiles.FirstOrDefault(p => p.UserId == SessionUserId);
        var user    = _db.Users.Find(SessionUserId);
        ViewBag.SavedAddress = profile?.Address ?? "";
        ViewBag.UserPoints   = user?.Points ?? 0;
        ViewBag.MemberLevel  = user?.MemberLevel ?? "Bronze";
        return View(items);
    }

    [HttpPost]
    public IActionResult AddToCart(int pid, int qty = 1)
    {
        if (SessionUser == null) return RedirectToAction("Login");
        if (!IsCustomer)
        {
            TempData["CartMsg"] = "เฉพาะ Customer เท่านั้นที่สามารถซื้อสินค้าได้";
            return RedirectToAction("ProductList");
        }
        var stock   = _db.Productstocks.FirstOrDefault(s => s.Pid == pid);
        int inStock = stock?.Quantity ?? 0;
        var cart    = GetCart();
        int inCart  = cart.ContainsKey(pid) ? cart[pid] : 0;
        int total   = inCart + qty;
        if (inStock <= 0) { TempData["CartMsg"] = "สินค้าหมดสต็อก"; return RedirectToAction("ProductList"); }
        if (total > inStock) { TempData["CartMsg"] = $"สต็อกคงเหลือมีเพียง {inStock} ชิ้น"; return RedirectToAction("ProductList"); }
        cart[pid] = total;
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

    // ─── APPLY COUPON (AJAX-friendly redirect) ──────────────────────────────
    [HttpPost]
    public IActionResult ApplyCoupon(string couponCode)
    {
        TempData["CouponCode"] = couponCode?.Trim().ToUpper();
        return RedirectToAction("CartPage");
    }

    // ─── CHECKOUT / PLACE ORDER ─────────────────────────────────────────────
    [HttpPost]
    public IActionResult PlaceOrder(string? shippingAddress, string? couponCode, int pointsToRedeem = 0)
    {
        if (SessionUser == null) return RedirectToAction("Login");
        if (string.IsNullOrWhiteSpace(shippingAddress))
        {
            TempData["CartError"] = "กรุณากรอกที่อยู่จัดส่ง";
            return RedirectToAction("CartPage");
        }

        var cart = GetCart();
        if (!cart.Any()) return RedirectToAction("CartPage");

        var user = _db.Users.Find(SessionUserId);
        if (user == null) return RedirectToAction("Login");

        var now      = DateTime.Now;
        var flashMap = _db.Flashsales
            .Where(f => f.IsActive && f.StartTime <= now && f.EndTime >= now)
            .ToDictionary(f => f.Pid, f => f.SalePrice);

        // เช็คสต็อก
        var outOfStock = new List<string>();
        foreach (var item in cart)
        {
            var s = _db.Productstocks.FirstOrDefault(s => s.Pid == item.Key);
            if ((s?.Quantity ?? 0) < item.Value)
            {
                var p = _db.Products.Find(item.Key);
                outOfStock.Add(p?.Pname ?? $"#{item.Key}");
            }
        }
        if (outOfStock.Any())
        {
            TempData["CartError"] = "สต็อกไม่เพียงพอ: " + string.Join(", ", outOfStock);
            return RedirectToAction("CartPage");
        }

        var pids      = cart.Keys.ToList();
        var products  = _db.Products.Where(p => pids.Contains(p.Pid)).ToList();
        decimal subtotal = products.Sum(p => (flashMap.ContainsKey(p.Pid) ? flashMap[p.Pid] : p.Price) * cart[p.Pid]);

        // คำนวณส่วนลด member
        decimal memberDiscount = user.MemberLevel switch
        {
            "Silver" => Math.Round(subtotal * 0.03m, 2),
            "Gold"   => Math.Round(subtotal * 0.05m, 2),
            _        => 0m
        };

        // คำนวณ coupon
        decimal couponDiscount = 0m;
        Coupon? coupon = null;
        string? normalizedCode = couponCode?.Trim().ToUpper();
        if (!string.IsNullOrEmpty(normalizedCode))
        {
            coupon = _db.Coupons.FirstOrDefault(c =>
                c.Code == normalizedCode && c.IsActive &&
                (c.ExpireDate == null || c.ExpireDate >= DateOnly.FromDateTime(now)) &&
                c.UsedCount < c.UsageLimit);

            if (coupon == null)
            {
                TempData["CartError"] = "Coupon ไม่ถูกต้องหรือหมดอายุแล้ว";
                return RedirectToAction("CartPage");
            }
            if (subtotal < coupon.MinAmount)
            {
                TempData["CartError"] = $"ยอดซื้อขั้นต่ำสำหรับ Coupon นี้คือ ฿{coupon.MinAmount:N0}";
                return RedirectToAction("CartPage");
            }
            bool alreadyUsed = _db.Couponusages.Any(u => u.CouponId == coupon.CouponId && u.UserId == user.Id);
            if (alreadyUsed)
            {
                TempData["CartError"] = "คุณใช้ Coupon นี้ไปแล้ว";
                return RedirectToAction("CartPage");
            }
            couponDiscount = Math.Round(subtotal * (coupon.DiscountPct / 100m), 2);
        }

        // คำนวณแลกแต้ม (10 แต้ม = ฿1)
        int maxRedeemable = Math.Min(pointsToRedeem, user.Points);
        decimal pointDiscount = Math.Round(maxRedeemable / 10m, 2);

        decimal totalDiscount = memberDiscount + couponDiscount + pointDiscount;
        decimal finalTotal    = Math.Max(0, subtotal - totalDiscount);

        // สร้าง order
        var order = new Order
        {
            UserId          = user.Id,
            OrderDate       = now,
            TotalAmount     = finalTotal,
            Status          = "Pending",
            ShippingAddress = shippingAddress,
            DiscountAmount  = totalDiscount,
            PointsUsed      = maxRedeemable,
            CouponCode      = coupon?.Code
        };
        _db.Orders.Add(order);
        _db.SaveChanges();

        // order details
        foreach (var p in products)
        {
            decimal price = flashMap.ContainsKey(p.Pid) ? flashMap[p.Pid] : p.Price;
            _db.Orderdetails.Add(new Orderdetail { OrderId = order.OrderId, Pid = p.Pid, Qty = cart[p.Pid], UnitPrice = price });
        }

        // หัก stock ทันทีตอนสั่ง (hold)
        foreach (var p in products)
        {
            var stock = _db.Productstocks.Find(p.Pid);
            if (stock != null)
            {
                stock.Quantity   = Math.Max(0, (stock.Quantity ?? 0) - cart[p.Pid]);
                stock.LastUpdate = DateTime.Now;
                _db.Stockhistories.Add(new Stockhistory
                {
                    Pid       = p.Pid,
                    OldQty    = (stock.Quantity ?? 0) + cart[p.Pid],
                    NewQty    = stock.Quantity ?? 0,
                    ChangedBy = SessionUser,
                    ChangedAt = DateTime.Now,
                    Note      = $"Hold สำหรับออเดอร์ #{order.OrderId}"
                });
            }
        }

        // อัปเดต coupon usage
        if (coupon != null)
        {
            _db.Couponusages.Add(new Couponusage { CouponId = coupon.CouponId, UserId = user.Id, OrderId = order.OrderId });
            coupon.UsedCount++;
        }

        // แต้มที่ใช้ (redeem)
        if (maxRedeemable > 0)
        {
            user.Points -= maxRedeemable;
            _db.Pointhistories.Add(new Pointhistory { UserId = user.Id, OrderId = order.OrderId, Points = -maxRedeemable, Type = "redeem", CreatedAt = now });
        }

        // แต้มที่ได้รับ (1 แต้มต่อ ฿10)
        int earnedPoints = (int)(finalTotal / 10);
        if (earnedPoints > 0)
        {
            user.Points += earnedPoints;
            _db.Pointhistories.Add(new Pointhistory { UserId = user.Id, OrderId = order.OrderId, Points = earnedPoints, Type = "earn", CreatedAt = now });
        }

        // อัปเดต member level
        user.MemberLevel = CalcMemberLevel(user.Points);

        // อัปเดต address
        var prof = _db.Userprofiles.FirstOrDefault(p => p.UserId == user.Id);
        if (prof != null) prof.Address = shippingAddress;

        _db.SaveChanges();

        // อัปเดต session
        HttpContext.Session.SetString("MemberLevel", user.MemberLevel);
        HttpContext.Session.SetInt32 ("Points",      user.Points);
        HttpContext.Session.Remove("Cart");

        TempData["OrderSuccess"] = $"สั่งซื้อสำเร็จ! ออเดอร์ #{order.OrderId} | ได้รับ {earnedPoints} แต้ม";
        return RedirectToAction("OrderHistory");
    }

    public IActionResult OrderHistory()
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var orders = _db.Orders
            .Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation)
            .Where(o => o.UserId == SessionUserId)
            .OrderByDescending(o => o.OrderDate).ToList();
        return View(orders);
    }

    // ─── MY PROFILE ─────────────────────────────────────────────────────────
    public IActionResult MyProfile()
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == SessionUserId);
        if (user == null) return RedirectToAction("Login");
        ViewBag.PointHistory = _db.Pointhistories
            .Where(p => p.UserId == SessionUserId)
            .OrderByDescending(p => p.CreatedAt).Take(10).ToList();
        return View(user);
    }

    [HttpPost]
    public IActionResult MyProfile(User data)
    {
        if (SessionUser == null) return RedirectToAction("Login");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == SessionUserId);
        if (user == null) return RedirectToAction("Login");
        user.FullName = data.FullName ?? user.FullName;
        if (user.Userprofile != null && data.Userprofile != null)
        {
            user.Userprofile.Gender   = data.Userprofile.Gender;
            user.Userprofile.Birthday = data.Userprofile.Birthday;
            user.Userprofile.Tel      = data.Userprofile.Tel;
            user.Userprofile.Address  = data.Userprofile.Address;
        }
        _db.Update(user);
        _db.SaveChanges();
        HttpContext.Session.SetString("FullName", user.FullName ?? user.Username);
        TempData["ProfileSuccess"] = "อัปเดตโปรไฟล์เรียบร้อยแล้ว";
        return RedirectToAction("MyProfile");
    }

    // ─── DASHBOARD ──────────────────────────────────────────────────────────
    public IActionResult Dashboard()
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var now   = DateTime.Today;
        var month = new DateTime(now.Year, now.Month, 1);
        ViewBag.TotalUsers    = _db.Users.Count();
        ViewBag.TotalProducts = _db.Products.Count();
        ViewBag.TotalOrders   = _db.Orders.Count();
        ViewBag.PendingOrders = _db.Orders.Count(o => o.Status == "Pending");
        ViewBag.TotalRevenue  = _db.Orders.Where(o => o.Status != "Cancelled").Sum(o => (decimal?)o.TotalAmount) ?? 0;
        ViewBag.MonthRevenue  = _db.Orders.Where(o => o.Status != "Cancelled" && o.OrderDate >= month).Sum(o => (decimal?)o.TotalAmount) ?? 0;
        ViewBag.LowStock      = _db.Productstocks.Count(s => (s.Quantity ?? 0) > 0 && (s.Quantity ?? 0) <= 5);
        ViewBag.OutOfStock    = _db.Productstocks.Count(s => (s.Quantity ?? 0) <= 0);
        ViewBag.RecentOrders  = _db.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate).Take(5).ToList();
        ViewBag.TopProducts   = _db.Orderdetails.Include(d => d.PidNavigation)
            .GroupBy(d => d.PidNavigation!.Pname)
            .Select(g => new { Name = g.Key, Qty = g.Sum(d => d.Qty) })
            .OrderByDescending(x => x.Qty).Take(5).ToList();
        ViewBag.AlertStocks   = _db.Productstocks.Include(s => s.PidNavigation)
            .Where(s => (s.Quantity ?? 0) <= 5).OrderBy(s => s.Quantity).Take(5).ToList();
        ViewBag.GoldMembers   = _db.Users.Count(u => u.MemberLevel == "Gold");
        ViewBag.SilverMembers = _db.Users.Count(u => u.MemberLevel == "Silver");
        ViewBag.ActiveFlash   = _db.Flashsales.Count(f => f.IsActive && f.StartTime <= now && f.EndTime >= now);
        ViewBag.ActiveCoupons = _db.Coupons.Count(c => c.IsActive && c.UsedCount < c.UsageLimit);
        return View();
    }

    // ─── MANAGEMENT ─────────────────────────────────────────────────────────
    public IActionResult Management()
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var users = _db.Users.Include(u => u.Role).Include(u => u.Userprofile).ToList();
        ViewBag.CurrentId = SessionUserId;
        return View(users);
    }

    public IActionResult DeleteUser(int id)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        if (id == SessionUserId) { TempData["Error"] = "ไม่สามารถลบบัญชีตัวเองได้"; return RedirectToAction("Management"); }
        var user = _db.Users.Find(id);
        if (user == null) return RedirectToAction("Management");
        if (_db.Orders.Any(o => o.UserId == id))
        {
            TempData["Error"] = $"ไม่สามารถลบ '{user.FullName ?? user.Username}' เนื่องจากมีประวัติออเดอร์";
            return RedirectToAction("Management");
        }
        var profile = _db.Userprofiles.FirstOrDefault(p => p.UserId == id);
        if (profile != null) _db.Userprofiles.Remove(profile);
        _db.Users.Remove(user);
        _db.SaveChanges();
        TempData["Success"] = $"ลบผู้ใช้ '{user.FullName ?? user.Username}' เรียบร้อยแล้ว";
        return RedirectToAction("Management");
    }

    [HttpPost]
    public IActionResult ChangeRole(int userId, int roleId)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        if (userId == SessionUserId) { TempData["Error"] = "ไม่สามารถเปลี่ยน Role ตัวเองได้"; return RedirectToAction("Management"); }
        if (roleId < 1 || roleId > 4) return RedirectToAction("Management");
        var user = _db.Users.Find(userId);
        if (user != null) { user.RoleId = roleId; _db.SaveChanges(); }
        return RedirectToAction("Management");
    }

    public IActionResult EditUser(int id)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var user = _db.Users.Include(u => u.Userprofile).FirstOrDefault(u => u.Id == id);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    public IActionResult EditUser(User data)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
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

    [HttpPost]
    public IActionResult AddStaff(User user, string Gender, DateOnly? Birthday, string? Phone, int RoleId)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        if (_db.Users.Any(u => u.Username == user.Username)) { TempData["AddStaffError"] = "Username นี้ถูกใช้งานแล้ว"; return RedirectToAction("Management"); }
        if (_db.Users.Any(u => u.Email    == user.Email))    { TempData["AddStaffError"] = "Email นี้ถูกใช้งานแล้ว";    return RedirectToAction("Management"); }
        user.RoleId = RoleId; user.CreatedAt = DateTime.Now; user.Points = 0; user.MemberLevel = "Bronze";
        _db.Users.Add(user);
        _db.SaveChanges();
        _db.Userprofiles.Add(new Userprofile { UserId = user.Id, Gender = Gender, Birthday = Birthday, Tel = Phone });
        _db.SaveChanges();
        TempData["Success"] = $"เพิ่มพนักงาน '{user.FullName ?? user.Username}' เรียบร้อยแล้ว";
        return RedirectToAction("Management");
    }

    // ─── ORDER MANAGEMENT ───────────────────────────────────────────────────
    public IActionResult OrderManagement(string? status)
    {
        if (!CanManageOrder) return RedirectToAction("ProductList");
        var query = _db.Orders.Include(o => o.User)
            .Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation).AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);
        ViewBag.StatusFilter = status;
        ViewBag.AllCount     = _db.Orders.Count();
        ViewBag.PendingCount = _db.Orders.Count(o => o.Status == "Pending");
        ViewBag.PaidCount    = _db.Orders.Count(o => o.Status == "Paid");
        ViewBag.ShippedCount = _db.Orders.Count(o => o.Status == "Shipped");
        ViewBag.CancelCount  = _db.Orders.Count(o => o.Status == "Cancelled");
        return View(query.OrderByDescending(o => o.OrderDate).ToList());
    }

    [HttpPost]
    public IActionResult UpdateOrderStatus(int orderId, string newStatus)
    {
        if (!CanManageOrder) return RedirectToAction("ProductList");
        var order = _db.Orders.Include(o => o.Orderdetails).FirstOrDefault(o => o.OrderId == orderId);
        if (order != null)
        {
            var prevStatus = order.Status;
            order.Status   = newStatus;

            // ถ้า Cancel — คืน stock กลับ
            if (newStatus == "Cancelled" && prevStatus != "Cancelled")
            {
                foreach (var detail in order.Orderdetails)
                {
                    var stock = _db.Productstocks.Find(detail.Pid);
                    if (stock != null)
                    {
                        int before    = Convert.ToInt32(stock.Quantity);
                        int detailQty = Convert.ToInt32(detail.Qty);
                        stock.Quantity   = before + detailQty;
                        stock.LastUpdate = DateTime.Now;
                        _db.Stockhistories.Add(new Stockhistory
                        {
                            Pid = detail.Pid ?? 0,
                            OldQty    = before,
                            NewQty    = Convert.ToInt32(stock.Quantity),
                            ChangedBy = SessionUser,
                            ChangedAt = DateTime.Now,
                            Note      = $"คืน stock จากออเดอร์ #{order.OrderId} (Cancelled)"
                        });
                    }
                }
            }
            _db.SaveChanges();
            TempData["Success"] = $"อัปเดตสถานะออเดอร์ #{orderId} เป็น {newStatus} เรียบร้อย";
        }
        return RedirectToAction("OrderManagement");
    }

    // ─── STOCK ──────────────────────────────────────────────────────────────
    public IActionResult StockList()
    {
        if (!CanManageStock) return RedirectToAction("ProductList");
        var stocks = _db.Productstocks
            .Include(s => s.PidNavigation).ThenInclude(p => p.Cat)
            .Include(s => s.PidNavigation).ThenInclude(p => p.Brand)
            .OrderBy(s => s.PidNavigation.Pname).ToList();
        ViewBag.Categories = _db.Categories.ToList();
        ViewBag.Brands     = _db.Brands.ToList();
        return View(stocks);
    }

    [HttpPost]
    public IActionResult UpdateStock(int pid, int quantity)
    {
        if (!CanManageStock) return RedirectToAction("ProductList");
        var stock   = _db.Productstocks.Find(pid);
        var product = _db.Products.Find(pid);
        if (stock != null)
        {
            int oldQty = stock.Quantity ?? 0;
            string note = quantity > oldQty ? $"เพิ่มสต็อก +{quantity - oldQty}"
                        : quantity < oldQty ? $"ลดสต็อก -{oldQty - quantity}" : "ไม่มีการเปลี่ยนแปลง";
            _db.Stockhistories.Add(new Stockhistory { Pid = pid, OldQty = oldQty, NewQty = quantity, ChangedBy = SessionUser, ChangedAt = DateTime.Now, Note = note });
            stock.Quantity = quantity; stock.LastUpdate = DateTime.Now;
            _db.SaveChanges();
            TempData["Success"] = $"อัปเดตสต็อก {product?.Pname} ({oldQty} → {quantity}) เรียบร้อย";
        }
        return RedirectToAction("StockList");
    }

    // เพิ่มสินค้าใหม่
    [HttpPost]
    public async Task<IActionResult> AddProduct(string pname, decimal price, string? description,
        int? catId, string? newCatName, int? brandId, string? newBrandName, int quantity, IFormFile? image)
    {
        if (!CanManageStock) return RedirectToAction("ProductList");

        // สร้าง category ใหม่ถ้า user พิมพ์เอง
        if (catId == null && !string.IsNullOrWhiteSpace(newCatName))
        {
            newCatName = newCatName.Trim();
            var existing = _db.Categories.FirstOrDefault(c => c.CatName == newCatName);
            if (existing != null) catId = existing.CatId;
            else { var nc = new Category { CatName = newCatName }; _db.Categories.Add(nc); _db.SaveChanges(); catId = nc.CatId; }
        }

        // สร้าง brand ใหม่ถ้า user พิมพ์เอง
        if (brandId == null && !string.IsNullOrWhiteSpace(newBrandName))
        {
            newBrandName = newBrandName.Trim();
            var existing = _db.Brands.FirstOrDefault(b => b.BrandName == newBrandName);
            if (existing != null) brandId = existing.BrandId;
            else { var nb = new Brand { BrandName = newBrandName }; _db.Brands.Add(nb); _db.SaveChanges(); brandId = nb.BrandId; }
        }

        string? imagePath = null;
        if (image != null && image.Length > 0)
        {
            var ext     = Path.GetExtension(image.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (allowed.Contains(ext))
            {
                var fileName = $"product_{Guid.NewGuid():N}{ext}";
                var dir      = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await image.CopyToAsync(stream);
                imagePath = $"/images/products/{fileName}";
            }
        }

        var product = new Product { Pname = pname, Price = price, Description = description, CatId = catId, BrandId = brandId, ImagePath = imagePath };
        _db.Products.Add(product);
        _db.SaveChanges();
        _db.Productstocks.Add(new Productstock { Pid = product.Pid, Quantity = quantity, LastUpdate = DateTime.Now });
        _db.Stockhistories.Add(new Stockhistory { Pid = product.Pid, OldQty = 0, NewQty = quantity, ChangedBy = SessionUser, ChangedAt = DateTime.Now, Note = "เพิ่มสินค้าใหม่" });
        _db.SaveChanges();
        TempData["Success"] = $"เพิ่มสินค้า '{pname}' เรียบร้อยแล้ว";
        return RedirectToAction("StockList");
    }


    // ─── ADD CATEGORY / BRAND ───────────────────────────────────────────────
    [HttpPost]
    public IActionResult CreateCategory(string catName)
    {
        if (!CanManageStock) return RedirectToAction("ProductList");
        catName = catName?.Trim() ?? "";
        if (string.IsNullOrEmpty(catName)) return RedirectToAction("StockList");
        if (!_db.Categories.Any(c => c.CatName == catName))
        {
            _db.Categories.Add(new Category { CatName = catName });
            _db.SaveChanges();
        }
        return RedirectToAction("StockList");
    }

    [HttpPost]
    public IActionResult CreateBrand(string brandName)
    {
        if (!CanManageStock) return RedirectToAction("ProductList");
        brandName = brandName?.Trim() ?? "";
        if (string.IsNullOrEmpty(brandName)) return RedirectToAction("StockList");
        if (!_db.Brands.Any(b => b.BrandName == brandName))
        {
            _db.Brands.Add(new Brand { BrandName = brandName });
            _db.SaveChanges();
        }
        return RedirectToAction("StockList");
    }

    public IActionResult StockHistory(int? pid)
    {
        if (!CanManageStock) return RedirectToAction("ProductList");
        var query = _db.Stockhistories.Include(h => h.PidNavigation).AsQueryable();
        if (pid.HasValue) query = query.Where(h => h.Pid == pid.Value);
        ViewBag.FilterPid = pid;
        ViewBag.Product   = pid.HasValue ? _db.Products.Find(pid.Value) : null;
        ViewBag.Products  = _db.Products.OrderBy(p => p.Pname).ToList();
        return View(query.OrderByDescending(h => h.ChangedAt).ToList());
    }

    // ─── SALES REPORT ───────────────────────────────────────────────────────
    public IActionResult SalesReport(string period = "monthly")
    {
        if (!CanViewReport) return RedirectToAction("ProductList");
        DateTime start, end;
        var now = DateTime.Today;
        switch (period)
        {
            case "daily":  start = now; end = now; break;
            case "yearly": start = new DateTime(now.Year, 1, 1); end = new DateTime(now.Year, 12, 31); break;
            default:       start = new DateTime(now.Year, now.Month, 1); end = start.AddMonths(1).AddDays(-1); break;
        }
        var orders = _db.Orders
            .Include(o => o.User).Include(o => o.Orderdetails).ThenInclude(d => d.PidNavigation)
            .Where(o => o.OrderDate >= start && o.OrderDate <= end.AddDays(1) && o.Status != "Cancelled")
            .OrderByDescending(o => o.OrderDate).ToList();
        ViewBag.Period       = period; ViewBag.Start = start; ViewBag.End = end;
        ViewBag.TotalRevenue = orders.Sum(o => o.TotalAmount);
        ViewBag.TotalDiscount= orders.Sum(o => o.DiscountAmount);
        ViewBag.TotalOrders  = orders.Count;
        ViewBag.TotalItems   = orders.SelectMany(o => o.Orderdetails).Sum(d => d.Qty);
        ViewBag.TopProducts  = orders.SelectMany(o => o.Orderdetails)
            .GroupBy(d => d.PidNavigation?.Pname ?? $"#{d.Pid}")
            .Select(g => new { Name = g.Key, Qty = g.Sum(d => d.Qty), Revenue = g.Sum(d => d.UnitPrice * d.Qty) })
            .OrderByDescending(x => x.Revenue).Take(5).ToList();
        return View(orders);
    }

    // ─── COUPON MANAGEMENT (Admin) ───────────────────────────────────────────
    public IActionResult CouponManagement()
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var coupons = _db.Coupons.OrderByDescending(c => c.CreatedAt).ToList();
        return View(coupons);
    }

    [HttpPost]
    public IActionResult CreateCoupon(string code, decimal discountPct, decimal minAmount, int usageLimit, DateOnly? expireDate)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        code = code.Trim().ToUpper();
        if (_db.Coupons.Any(c => c.Code == code))
        {
            TempData["CouponError"] = "Code นี้มีอยู่แล้ว";
            return RedirectToAction("CouponManagement");
        }
        _db.Coupons.Add(new Coupon { Code = code, DiscountPct = discountPct, MinAmount = minAmount, UsageLimit = usageLimit, ExpireDate = expireDate, IsActive = true, CreatedAt = DateTime.Now });
        _db.SaveChanges();
        TempData["CouponSuccess"] = $"สร้าง Coupon '{code}' เรียบร้อยแล้ว";
        return RedirectToAction("CouponManagement");
    }

    [HttpPost]
    public IActionResult ToggleCoupon(int couponId)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var c = _db.Coupons.Find(couponId);
        if (c != null) { c.IsActive = !c.IsActive; _db.SaveChanges(); }
        return RedirectToAction("CouponManagement");
    }

    // ─── FLASH SALE MANAGEMENT (Admin) ──────────────────────────────────────
    public IActionResult FlashSaleManagement()
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var now   = DateTime.Now;
        var sales = _db.Flashsales.Include(f => f.PidNavigation).OrderByDescending(f => f.StartTime).ToList();
        ViewBag.Products = _db.Products.OrderBy(p => p.Pname).ToList();
        ViewBag.Now      = now;
        return View(sales);
    }

    [HttpPost]
    public IActionResult CreateFlashSale(int pid, decimal salePrice, DateTime startTime, DateTime endTime)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        _db.Flashsales.Add(new Flashsale { Pid = pid, SalePrice = salePrice, StartTime = startTime, EndTime = endTime, IsActive = true });
        _db.SaveChanges();
        TempData["FlashSuccess"] = "สร้าง Flash Sale เรียบร้อยแล้ว";
        return RedirectToAction("FlashSaleManagement");
    }

    [HttpPost]
    public IActionResult ToggleFlashSale(int flashSaleId)
    {
        if (!IsAdmin) return RedirectToAction("ProductList");
        var f = _db.Flashsales.Find(flashSaleId);
        if (f != null) { f.IsActive = !f.IsActive; _db.SaveChanges(); }
        return RedirectToAction("FlashSaleManagement");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
