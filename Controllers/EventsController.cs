using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    public class EventsController : Controller
    {
        // =========================
        // EVENTS MAIN PAGE
        // =========================
        public IActionResult Index()
        {
            return View();
        }

        // =========================
        // CREATE EVENT - GET
        // =========================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // =========================
        // CREATE EVENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Event model)
        {
            if (ModelState.IsValid)
            {
                // Database saving removed

                return RedirectToAction("Index");
            }

            return View(model);
        }

        // =========================
        // CLIENT EVENTS PAGE
        // =========================
        public IActionResult Client()
        {
            return View();
        }

        // =========================
        // CLIENT VIEW EVENTS
        // =========================
        public IActionResult ViewClient()
        {
            return View();
        }

        // =========================
        // STAFF VIEW EVENTS
        // =========================
        public IActionResult ViewStaff()
        {
            return View();
        }

        // =========================
        // EVENT DETAILS
        // =========================
        public IActionResult Details(int id)
        {
            return View();
        }

        // =========================
        // EDIT EVENT - GET
        // =========================
        [HttpGet]
        public IActionResult Edit(int id)
        {
            return View();
        }

        // =========================
        // EDIT EVENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Event model)
        {
            if (ModelState.IsValid)
            {
                // Database updating removed

                return RedirectToAction("ViewStaff");
            }

            return View(model);
        }

        // =========================
        // DELETE EVENT
        // =========================
        public IActionResult Delete(int id)
        {
            // Database deletion removed

            return RedirectToAction("ViewStaff");
        }

        // =========================
        // UPLOAD DESIGN
        // =========================
        public IActionResult Upload(int id)
        {
            return View();
        }
    }
}