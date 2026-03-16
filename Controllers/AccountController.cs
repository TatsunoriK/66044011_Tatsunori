using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using _66044011_Tatsunori.Models;
using _66044011_Tatsunori.ViewModels;
using Microsoft.Extensions.Configuration.UserSecrets;


namespace _66044011_Tatsunori.Controllers;

public class AccountController : Controller
{

    private readonly Csi402dbContext _db;
    public AccountController(Csi402dbContext db)
    {
        _db = db;
    }

    public IActionResult Lab09()
    {
        return View();
    }
    [HttpPost]
    public IActionResult Lab09(LabStudentViewModels data)
    {
        var u = new LabStudent();
        u.StdID = data.StdID;
        u.StdPASSWORD = data.StdPASSWORD;
        u.StdName = data.StdName;
        u.StdLastname = data.StdLastname;
        _db.LabStudents.Add(u);
        _db.SaveChanges();
        return RedirectToAction("Lab09List", "Account");
    }

    public IActionResult Lab09List()
    {
        var user = (from u in _db.LabStudents
                    select new LabStudentViewModels
                    {
                        StdID = u.StdID,
                        StdPASSWORD = u.StdPASSWORD,
                        StdName = u.StdName,
                        StdLastname = u.StdLastname
                    }).ToList();
        return View(user);
    }



    public IActionResult Lab10(string UID)
    {
        var check = (from us in _db.LabStudents
                     where us.StdID == UID
                     select new LabStudentViewModels
                     {
                         StdID = us.StdID,
                         StdPASSWORD = us.StdPASSWORD,
                         StdName = us.StdName,
                         StdLastname = us.StdLastname
                     }).FirstOrDefault();
        return View(check);
    }
    [HttpPost]
    public IActionResult Lab10(LabStudentViewModels data)
    {
        var user = (from u in _db.LabStudents where u.StdID == data.StdID select u).FirstOrDefault();

        user.StdPASSWORD = data.StdPASSWORD;
        user.StdName = data.StdName;
        user.StdLastname = data.StdLastname;
        _db.Update(user);
        _db.SaveChanges();

        return RedirectToAction("Lab09List", "Account");
    }

    public IActionResult Lab10D(string UID)
    {
        var user = (from u in _db.LabStudents where u.StdID == UID select u).FirstOrDefault();

            _db.LabStudents.Remove(user);
            _db.SaveChanges();
            return RedirectToAction("Lab09List", "Account");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
