using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMH.API.Data;
using TMH.Shared.Models;

namespace TMH.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArticleController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public ArticleController(AppDbContext db, IWebHostEnvironment env)
        {
            _db  = db;
            _env = env;
        }

        // =====================================================================
        // PUBLIC — Trang chủ / Tin tức (không cần đăng nhập)
        // GET /api/article/published?category=&limit=6
        // =====================================================================
        [HttpGet("published")]
        public async Task<IActionResult> GetPublished(
            [FromQuery] string? category,
            [FromQuery] int limit = 6)
        {
            var query = _db.Articles
                .Include(a => a.Doctor)
                .Where(a => a.Status == ArticleStatus.Published)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(a => a.Category == category);

            var result = await query
                .OrderByDescending(a => a.PublishedAt)
                .Take(limit)
                .Select(a => new
                {
                    a.Id, a.Title, a.Slug, a.Summary,
                    a.Category, a.Thumbnail, a.PublishedAt,
                    AuthorName = a.Doctor.FullName,
                    AuthorDegree = a.Doctor.Degree
                })
                .ToListAsync();

            return Ok(result);
        }

        // GET /api/article/{id} — chi tiết bài viết
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var article = await _db.Articles
                .Include(a => a.Doctor)
                .Where(a => a.Id == id && a.Status == ArticleStatus.Published)
                .Select(a => new
                {
                    a.Id, a.Title, a.Slug, a.Summary, a.Content,
                    a.Category, a.Thumbnail, a.PublishedAt,
                    AuthorName = a.Doctor.FullName,
                    AuthorDegree = a.Doctor.Degree,
                    AuthorSpecialty = a.Doctor.Specialty
                })
                .FirstOrDefaultAsync();

            if (article == null) return NotFound(new { message = "Không tìm thấy bài viết." });
            return Ok(article);
        }

        // =====================================================================
        // DOCTOR — Viết và quản lý bài của mình
        // =====================================================================

        // GET /api/article/my — bài viết của bác sĩ đang đăng nhập
        [HttpGet("my")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyArticles()
        {
            var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "0");
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return NotFound();

            var articles = await _db.Articles
                .Where(a => a.DoctorId == doctor.Id)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id, a.Title, a.Category, a.Status,
                    StatusDisplay = a.Status == ArticleStatus.Published ? "Đã đăng" : "Nháp",
                    a.CreatedAt, a.PublishedAt
                })
                .ToListAsync();

            return Ok(articles);
        }

        // POST /api/article — tạo bài mới
        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create([FromBody] ArticleUpsertDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "0");
            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            if (doctor == null) return Forbid();

            var article = new Article
            {
                DoctorId  = doctor.Id,
                Title     = dto.Title,
                Slug      = GenerateSlug(dto.Title),
                Summary   = dto.Summary,
                Content   = dto.Content,
                Category  = dto.Category,
                Thumbnail = dto.Thumbnail,
                Status    = dto.Publish ? ArticleStatus.Published : ArticleStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                PublishedAt = dto.Publish ? DateTime.UtcNow : null
            };

            _db.Articles.Add(article);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Đăng bài thành công.", id = article.Id });
        }

        // PUT /api/article/{id} — cập nhật bài
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] ArticleUpsertDto dto)
        {
            var article = await _db.Articles.Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == id);
            if (article == null) return NotFound(new { success = false, message = "Không tìm thấy bài viết." });

            // Bác sĩ chỉ sửa bài của mình
            if (User.IsInRole("Doctor"))
            {
                var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "0");
                if (article.Doctor.UserId != userId)
                    return Forbid();
            }

            article.Title     = dto.Title;
            article.Slug      = GenerateSlug(dto.Title);
            article.Summary   = dto.Summary;
            article.Content   = dto.Content;
            article.Category  = dto.Category;
            article.Thumbnail = dto.Thumbnail;
            article.UpdatedAt = DateTime.UtcNow;

            if (dto.Publish && article.Status == ArticleStatus.Draft)
            {
                article.Status      = ArticleStatus.Published;
                article.PublishedAt = DateTime.UtcNow;
            }
            else if (!dto.Publish)
            {
                article.Status = ArticleStatus.Draft;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật bài viết thành công." });
        }

        // DELETE /api/article/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _db.Articles.Include(a => a.Doctor).FirstOrDefaultAsync(a => a.Id == id);
            if (article == null) return NotFound(new { success = false, message = "Không tìm thấy." });

            if (User.IsInRole("Doctor"))
            {
                var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "0");
                if (article.Doctor.UserId != userId) return Forbid();
            }

            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã xóa bài viết." });
        }

        // =====================================================================
        // ADMIN — Xem tất cả bài, duyệt / gỡ
        // =====================================================================

        // GET /api/article/all — admin xem tất cả bài
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var articles = await _db.Articles
                .Include(a => a.Doctor)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id, a.Title, a.Category,
                    a.Status,
                    StatusDisplay = a.Status == ArticleStatus.Published ? "Đã đăng" : "Nháp",
                    AuthorName  = a.Doctor.FullName,
                    a.CreatedAt, a.PublishedAt
                })
                .ToListAsync();

            return Ok(articles);
        }

        // PUT /api/article/{id}/toggle-status — admin duyệt / gỡ bài
        [HttpPut("{id:int}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var article = await _db.Articles.FindAsync(id);
            if (article == null) return NotFound(new { success = false, message = "Không tìm thấy." });

            if (article.Status == ArticleStatus.Draft)
            {
                article.Status      = ArticleStatus.Published;
                article.PublishedAt = DateTime.UtcNow;
            }
            else
            {
                article.Status = ArticleStatus.Draft;
            }

            await _db.SaveChangesAsync();
            var msg = article.Status == ArticleStatus.Published ? "Đã duyệt đăng bài." : "Đã gỡ bài viết.";
            return Ok(new { success = true, message = msg, status = article.Status.ToString() });
        }


        // =====================================================================
        // UPLOAD ẢNH THUMBNAIL
        // POST /api/article/upload-image
        // =====================================================================
        [HttpPost("upload-image")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Không có file được chọn." });

            // Chỉ chấp nhận ảnh
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { success = false, message = "Chỉ chấp nhận file ảnh (jpg, png, webp, gif)." });

            // Giới hạn 5MB
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { success = false, message = "Ảnh không được vượt quá 5MB." });

            // Tạo thư mục uploads nếu chưa có
            var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "articles");
            Directory.CreateDirectory(uploadsDir);

            // Tạo tên file unique
            var ext      = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var url = $"/uploads/articles/{fileName}";
            return Ok(new { success = true, url });
        }

        // ── Helper ───────────────────────────────────────────────────
        private static string GenerateSlug(string title)
        {
            var slug = title.ToLowerInvariant()
                .Replace("à","a").Replace("á","a").Replace("â","a").Replace("ã","a")
                .Replace("è","e").Replace("é","e").Replace("ê","e")
                .Replace("ì","i").Replace("í","i")
                .Replace("ò","o").Replace("ó","o").Replace("ô","o").Replace("õ","o")
                .Replace("ù","u").Replace("ú","u").Replace("ư","u")
                .Replace("ý","y").Replace("đ","d");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            return slug.Trim('-');
        }
    }

    public class ArticleUpsertDto
    {
        public string Title     { get; set; } = string.Empty;
        public string Summary   { get; set; } = string.Empty;
        public string Content   { get; set; } = string.Empty;
        public string Category  { get; set; } = string.Empty;
        public string? Thumbnail { get; set; }
        public bool Publish     { get; set; } = false;
    }
}
