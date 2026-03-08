using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using _66044011_Tatsunori.Models;
using _66044011_Tatsunori.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace _66044011_Tatsunori.Controllers;

public class ProjectController : Controller
{
    private readonly Csi402dbContext _db;
    public ProjectController(Csi402dbContext db)
    {
        _db = db;
    }

    public IActionResult Project1(string UserId, string Password)
    {
        var model = new LoginViewModels
        {
            UserId = UserId,
            Password = Password
        };
        return View(model);
    }
    public IActionResult Index()
    {
        return RedirectToAction("Login");
    }

    // =========================
    // LOGIN PAGE
    // =========================
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Login(LoginViewModels data)
    {
        var user = _db.Users
            .FirstOrDefault(u => u.Username == data.UserId && u.Password == data.Password);

        if (user != null)
        {
            HttpContext.Session.SetString("User", user.Username);
            return RedirectToAction("ProductList");
        }

        ViewBag.Error = "Username or Password incorrect";
        return View();
    }

    // =========================
    // REGISTER PAGE
    // =========================
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Register(User user, string Gender, DateOnly Birthday, string Address, string Phone)
    {
        // สมัครใหม่ = Guest
        user.RoleId = 5;
        user.CreatedAt = DateTime.Now;

        // บันทึก Users
        _db.Users.Add(user);
        _db.SaveChanges();

        // สร้าง UserProfile
        Userprofile profile = new Userprofile
        {
            UserId = user.Id,
            Gender = Gender,
            Birthday = Birthday,
            Address = Address,
            Tel = Phone
        };

        _db.Userprofiles.Add(profile);
        _db.SaveChanges();

        return RedirectToAction("Login");
    }


    public IActionResult CartPage()
    {
        return View();
    }

    public IActionResult Management()
    {
        var users = _db.Users
            .Include(u => u.Role)
            .Include(u => u.Userprofile)
            .ToList();

        return View(users);
    }

    public IActionResult DeleteUser(int id)
    {
        var user = _db.Users.Find(id);

        if (user != null)
        {
            _db.Users.Remove(user);
            _db.SaveChanges();
        }

        return RedirectToAction("Management");
    }

    [HttpPost]
    public IActionResult ChangeRole(int userId, int roleId)
    {
        var user = _db.Users.Find(userId);

        if (user != null)
        {
            user.RoleId = roleId;
            _db.SaveChanges();
        }

        return RedirectToAction("Management");
    }

    public IActionResult EditUser(int id)
    {
        var user = _db.Users
            .Include(u => u.Userprofile)
            .FirstOrDefault(u => u.Id == id);

        return View(user);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
