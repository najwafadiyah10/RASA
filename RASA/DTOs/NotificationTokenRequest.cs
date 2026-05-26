using Microsoft.AspNetCore.Mvc;

namespace RASA.DTOs
{
    public class NotificationTokenRequest : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
