using Microsoft.AspNetCore.Mvc;

namespace RASA.Controllers
{
    public class NotificationsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
