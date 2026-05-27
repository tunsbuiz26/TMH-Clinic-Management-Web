using Microsoft.AspNetCore.Mvc;
using TMH.Web.Services;

namespace TMH.Web.Controllers
{
    public class NewsController : Controller
    {
        private readonly ApiService _api;
        public NewsController(ApiService api) { _api = api; }

        // GET /News — trang danh sách tất cả bài viết
        public async Task<IActionResult> Index(string? category)
        {
            ViewBag.IsLoggedIn = HttpContext.Session.GetString("JwtToken") != null;
            ViewBag.UserName   = HttpContext.Session.GetString("UserName");
            ViewBag.UserRole   = HttpContext.Session.GetString("UserRole");

            var url = "api/article/published?limit=50";
            if (!string.IsNullOrEmpty(category)) url += $"&category={Uri.EscapeDataString(category)}";
            var raw = await _api.GetRawJsonAsync(url);
            ViewBag.ArticlesJson = (raw ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            ViewBag.Category = category ?? "";
            return View();
        }

        // GET /News/Detail/{id} — trang chi tiết bài viết
        public async Task<IActionResult> Detail(int id)
        {
            ViewBag.IsLoggedIn = HttpContext.Session.GetString("JwtToken") != null;
            ViewBag.UserName   = HttpContext.Session.GetString("UserName");
            ViewBag.UserRole   = HttpContext.Session.GetString("UserRole");

            var raw = await _api.GetRawJsonAsync($"api/article/{id}");
            if (raw == null) return NotFound();
            ViewBag.ArticleJson = (raw ?? "null").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);

            // Load thêm 3 bài liên quan
            var related = await _api.GetRawJsonAsync("api/article/published?limit=3");
            ViewBag.RelatedJson = (related ?? "[]").Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
            return View();
        }
    }
}
