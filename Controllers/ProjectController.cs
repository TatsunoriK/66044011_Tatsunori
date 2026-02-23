using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using _66044011_Tatsunori.Models;
using _66044011_Tatsunori.ViewModels;


namespace _66044011_Tatsunori.Controllers;

public class ProjectController : Controller
{
    public IActionResult Login()
    {
        return View();
    }
    [HttpPost]
    public IActionResult Login(_66044011_Tatsunori.ViewModels.LoginViewModels data)
    {
        string A, B;
        A = data.UserId;
        B = data.Password;
        ViewData["UserId"] = A;
        ViewData["Password"] = B;
        return RedirectToAction("Project1", "Project", new { UserId = A, Password = B });
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

    public IActionResult Register()
    {
        return View();
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
