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
                    StatusDisplay = a.Status == ArticleStatus.Published ? "Đã đăng"
                                 : a.Status == ArticleStatus.PendingReview ? "Chờ duyệt" : "Nháp",
                    a.CreatedAt, a.PublishedAt
                })
                .ToListAsync();

            return Ok(articles);
        }

        // POST /api/article — tạo bài mới (Doctor tự đăng, Admin chọn tác giả qua dto.DoctorId)
        [HttpPost]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Create([FromBody] ArticleUpsertDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            Doctor? doctor;
            if (User.IsInRole("Admin"))
            {
                if (dto.DoctorId != null)
                    doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == dto.DoctorId.Value);
                else
                    // Admin không chọn tác giả → dùng bác sĩ đầu tiên làm placeholder (tác giả = "Quản trị viên")
                    doctor = await _db.Doctors.OrderBy(d => d.Id).FirstOrDefaultAsync();
            }
            else
            {
                var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "0");
                doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
            }
            if (doctor == null) return Forbid();

            // Doctor gửi duyệt → PendingReview; Admin tạo thì Published thẳng
            ArticleStatus newStatus;
            DateTime? publishedAt = null;
            if (dto.Publish)
            {
                if (User.IsInRole("Admin"))
                {
                    newStatus   = ArticleStatus.Published;
                    publishedAt = DateTime.UtcNow;
                }
                else
                {
                    newStatus = ArticleStatus.PendingReview;
                }
            }
            else
            {
                newStatus = ArticleStatus.Draft;
            }

            var article = new Article
            {
                DoctorId    = doctor.Id,
                Title       = dto.Title,
                Slug        = GenerateSlug(dto.Title),
                Summary     = dto.Summary,
                Content     = dto.Content,
                Category    = dto.Category,
                Thumbnail   = dto.Thumbnail,
                Status      = newStatus,
                CreatedAt   = DateTime.UtcNow,
                PublishedAt = publishedAt
            };

            _db.Articles.Add(article);
            await _db.SaveChangesAsync();

            var msg = newStatus == ArticleStatus.PendingReview
                ? "Bài viết đã gửi chờ duyệt."
                : "Đăng bài thành công.";
            return Ok(new { success = true, message = msg, id = article.Id });
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

            if (dto.Publish)
            {
                if (User.IsInRole("Admin"))
                {
                    article.Status      = ArticleStatus.Published;
                    article.PublishedAt ??= DateTime.UtcNow;
                }
                else if (article.Status != ArticleStatus.Published)
                {
                    // Doctor gửi lại duyệt
                    article.Status = ArticleStatus.PendingReview;
                }
            }
            else
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

        // GET /api/article/admin/{id} — admin lấy chi tiết bất kỳ bài (kể cả nháp)
        [HttpGet("admin/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminGetDetail(int id)
        {
            var article = await _db.Articles
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (article == null) return NotFound(new { success = false, message = "Không tìm thấy bài viết." });
            return Ok(new {
                article.Id, article.Title, article.Summary, article.Content,
                article.Category, article.Thumbnail,
                article.DoctorId,
                AuthorName  = article.Doctor.FullName,
                Publish     = article.Status == ArticleStatus.Published,
                StatusDisplay = article.Status == ArticleStatus.Published ? "Đã đăng"
                              : article.Status == ArticleStatus.PendingReview ? "Chờ duyệt" : "Nháp"
            });
        }

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
                    StatusDisplay = a.Status == ArticleStatus.Published ? "Đã đăng"
                                 : a.Status == ArticleStatus.PendingReview ? "Chờ duyệt" : "Nháp",
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

            // Draft / PendingReview → Admin duyệt → Published
            // Published → Admin gỡ → Draft
            if (article.Status == ArticleStatus.Published)
            {
                article.Status = ArticleStatus.Draft;
            }
            else
            {
                article.Status      = ArticleStatus.Published;
                article.PublishedAt ??= DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            var msg = article.Status == ArticleStatus.Published ? "Đã duyệt đăng bài." : "Đã gỡ bài viết về nháp.";
            return Ok(new { success = true, message = msg, status = article.Status.ToString() });
        }


        // =====================================================================
        // UPLOAD ẢNH THUMBNAIL
        // POST /api/article/upload-image
        // =====================================================================
        [HttpPost("upload-image")]
        [Authorize(Roles = "Doctor,Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
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
        // Chỉ dùng khi Admin tạo bài — chọn tác giả
        public int? DoctorId    { get; set; }
    }
}
