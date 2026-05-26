using Microsoft.AspNetCore.Mvc;

namespace RASA.Models
{
    public class NotificationToken : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
