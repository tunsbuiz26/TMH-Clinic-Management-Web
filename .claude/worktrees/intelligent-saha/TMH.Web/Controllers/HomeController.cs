using Microsoft.AspNetCore.Mvc;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApiService _api;
        public HomeController(ApiService api) { _api = api; }

        public async Task<IActionResult> Index()
        {
            ViewBag.IsLoggedIn = HttpContext.Session.GetString("JwtToken") != null;
            ViewBag.UserName   = HttpContext.Session.GetString("UserName");
            ViewBag.UserRole   = HttpContext.Session.GetString("UserRole");

            // Load 3 bài mới nhất cho trang chủ
            var articles = await _api.GetRawJsonAsync("api/article/published?limit=3");
            ViewBag.ArticlesJson = (articles ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            return View();
        }

        public IActionResult About()   => View();
        public IActionResult Contact() => View();
        public IActionResult HuongDan() => View();
    }
}
