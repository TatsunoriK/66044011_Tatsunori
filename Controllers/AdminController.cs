using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using _66044011_Tatsunori.Models;

namespace _66044011_Tatsunori.Controllers;

public class AdminController : Controller
{
    private readonly Csi402dbContext _db;

    public AdminController(Csi402dbContext db)
    {
        _db = db;
    }

    public IActionResult Management1()
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

        return RedirectToAction("Management1");
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
