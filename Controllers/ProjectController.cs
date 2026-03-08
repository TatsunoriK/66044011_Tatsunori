using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using _66044011_Tatsunori.Models;
using _66044011_Tatsunori.ViewModels;

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
    public IActionResult Register(User user)
    {
        var exist = _db.Users.FirstOrDefault(u => u.Username == user.Username);

        if (exist != null)
        {
            ViewBag.Error = "Username already exists";
            return View();
        }

        _db.Users.Add(user);
        _db.SaveChanges();

        return RedirectToAction("Login");
    }


    public IActionResult CartPage()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
