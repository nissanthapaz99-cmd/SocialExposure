using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;

namespace SocialExposure.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    public IActionResult About()
{
    return View();
}

public IActionResult Contact()
{
    return View();
}

public IActionResult Help()
{
    return View();
}
}
