using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using _66044011_Tatsunori.Models;


namespace _66044011_Tatsunori.Controllers;

public class AdminController : Controller
{
    
    public IActionResult Management()
    {
        return View();
    }

    

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
