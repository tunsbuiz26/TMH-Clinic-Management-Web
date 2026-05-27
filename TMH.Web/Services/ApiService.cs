using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMH.Shared.DTOs;

namespace TMH.Web.Services
{
    /// <summary>
    /// ApiService là lớp trung gian duy nhất giữa Web App và API.
    /// Mọi request HTTP đều đi qua đây — không viết HttpClient trực tiếp trong Controller.
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _http;
        private readonly IHttpContextAccessor _ctx;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiService(HttpClient http, IHttpContextAccessor ctx)
        {
            _http = http;
            _ctx = ctx;
        }

        // =====================================================================
        // ĐĂNG KÝ / ĐĂNG NHẬP
        // =====================================================================
        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
            => await PostAsync<AuthResponseDto>("api/auth/register", dto);

        public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
            => await PostAsync<AuthResponseDto>("api/auth/login", dto);

        // =====================================================================
        // HELPER: CÁC HÀM CƠ BẢN (TRẢ VỀ DTO)
        // =====================================================================
        private async Task<T?> PostAsync<T>(string endpoint, object body)
        {
            AttachToken();
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(endpoint, content);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] Không kết nối được API: {ex.Message}");
                return default;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseJson)) return default;
            try
            {
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOpts);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"[ApiService] PostAsync deserialize lỗi: {ex.Message} | Response: {responseJson}");
                return default;
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            AttachToken();
            try
            {
                var response = await _http.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode) return default;
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOpts);
            }
            catch (HttpRequestException)
            {
                return default;
            }
        }

        public async Task<T?> PutAsync<T>(string endpoint, object body)
        {
            AttachToken();
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PutAsync(endpoint, content);
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOpts);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] PutAsync lỗi: {ex.Message}");
                return default;
            }
        }

        public async Task<T?> DeleteAsync<T>(string endpoint)
        {
            AttachToken();
            try
            {
                var response = await _http.DeleteAsync(endpoint);
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(responseJson, _jsonOpts);
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] DeleteAsync lỗi: {ex.Message}");
                return default;
            }
        }

        // =====================================================================
        // RAW JSON FORWARDING (DÙNG CHO CONTROLLERS TRẢ VỀ CONTENT)
        // ĐÃ CẬP NHẬT: Xử lý bẫy lỗi HTML Response
        // =====================================================================
        // Cập nhật lại trong ApiService.cs
        public async Task<string?> GetRawJsonAsync(string endpoint)
        {
            AttachToken();
            try
            {
                var response = await _http.GetAsync(endpoint);
                var body = await response.Content.ReadAsStringAsync();

                // Bẫy lỗi: Nếu không phải JSON hợp lệ (thường là HTML lỗi), trả JSON lỗi chuẩn
                if (!response.IsSuccessStatusCode)
                {
                    try { JsonDocument.Parse(body); }
                    catch
                    {
                        // Trả về null để PatientController sử dụng chuỗi mặc định "Chưa có đơn thuốc"
                        // Hoặc trả về message lỗi chi tiết để debug
                        return null;
                    }
                }
                return string.IsNullOrWhiteSpace(body) ? null : body;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] GetRawJsonAsync lỗi: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> PostRawJsonAsync(string endpoint, object body)
        {
            AttachToken();
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PostAsync(endpoint, content);
                var raw = await response.Content.ReadAsStringAsync();

                // Nếu API trả về lỗi (401, 403, 500...) mà body không phải JSON (thường là HTML)
                if (!response.IsSuccessStatusCode)
                {
                    try { JsonDocument.Parse(raw); } // Kiểm tra xem có phải JSON không
                    catch
                    {
                        // Nếu là HTML, trả về chuỗi JSON lỗi để frontend xử lý được
                        return $"{{\"success\":false,\"message\":\"Lỗi {(int)response.StatusCode}: {response.ReasonPhrase}\"}}";
                    }
                }
                return raw;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] PostRawJsonAsync lỗi: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> PutRawJsonAsync(string endpoint, object body)
        {
            AttachToken();
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PutAsync(endpoint, content);
                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    try { JsonDocument.Parse(raw); }
                    catch
                    {
                        return $"{{\"success\":false,\"message\":\"Lỗi {(int)response.StatusCode}: {response.ReasonPhrase}\"}}";
                    }
                }
                return raw;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] PutRawJsonAsync lỗi: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> DeleteRawJsonAsync(string endpoint)
        {
            AttachToken();
            try
            {
                var response = await _http.DeleteAsync(endpoint);
                var raw = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    try { JsonDocument.Parse(raw); }
                    catch
                    {
                        return $"{{\"success\":false,\"message\":\"Lỗi {(int)response.StatusCode}: {response.ReasonPhrase}\"}}";
                    }
                }
                return raw;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] DeleteRawJsonAsync lỗi: {ex.Message}");
                return null;
            }
        }

        // =====================================================================
        // AUTHENTICATION & TOKEN
        // =====================================================================
        private void AttachToken()
        {
            var token = _ctx.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            else
                _http.DefaultRequestHeaders.Authorization = null;
        }

        // =====================================================================
        // ĐẶT LỊCH KHÁM & THANH TOÁN
        // =====================================================================
        public async Task<List<DoctorScheduleDto>?> GetAvailableDoctorsAsync(DateTime? date = null)
        {
            var url = date.HasValue
                ? $"api/appointment/available-doctors?date={date.Value:yyyy-MM-dd}"
                : "api/appointment/available-doctors";
            return await GetAsync<List<DoctorScheduleDto>>(url);
        }

        public async Task<string?> CreateVnPayUrlAsync(VnPaymentRequestDto dto)
        {
            AttachToken();
            var response = await _http.PostAsJsonAsync("api/payment/create-payment-url", dto);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("url").GetString();
        }

        public async Task<VnPaymentResponseDto?> GetPaymentResultAsync(string queryString)
        {
            var response = await _http.GetAsync($"api/payment/payment-return{queryString}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<VnPaymentResponseDto>();
        }

        public async Task<AppointmentResponseDto?> BookAppointmentAsync(BookAppointmentDto dto)
            => await PostAsync<AppointmentResponseDto>("api/appointment/book", dto);

        public async Task<List<AppointmentDetailDto>?> GetMyAppointmentsAsync()
            => await GetAsync<List<AppointmentDetailDto>>("api/appointment/my-appointments");

        public async Task<AppointmentResponseDto?> CancelAppointmentAsync(int id)
            => await PostAsync<AppointmentResponseDto>($"api/appointment/cancel/{id}", new { });

        // =====================================================================
        // STAFF & DOCTOR
        // =====================================================================
        public async Task<List<AppointmentDetailDto>?> GetAppointmentsByDateAsync(DateTime date, int? doctorId = null)
        {
            var url = $"api/appointment/by-date?date={date:yyyy-MM-dd}";
            if (doctorId.HasValue) url += $"&doctorId={doctorId.Value}";
            return await GetAsync<List<AppointmentDetailDto>>(url);
        }

        public async Task<AppointmentResponseDto?> UpdateAppointmentStatusAsync(UpdateAppointmentStatusDto dto)
            => await PutAsync<AppointmentResponseDto>("api/appointment/update-status", dto);

        public async Task<string?> GetPendingAssignmentsRawAsync(string? keyword = null, string? fromDate = null, string? toDate = null)
        {
            var url = "api/appointment/pending-assignment";
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(keyword)) qs.Add($"keyword={Uri.EscapeDataString(keyword)}");
            if (!string.IsNullOrWhiteSpace(fromDate)) qs.Add($"fromDate={fromDate}");
            if (!string.IsNullOrWhiteSpace(toDate)) qs.Add($"toDate={toDate}");
            if (qs.Count > 0) url += "?" + string.Join("&", qs);
            return await GetRawJsonAsync(url);
        }
        public async Task<string?> SearchPatientsRawAsync(string query)
        {
            var url = $"api/appointment/search-patients?q={Uri.EscapeDataString(query)}";
            return await GetRawJsonAsync(url);
        }

        public async Task<AppointmentResponseDto?> BookWalkInAsync(BookAppointmentDto dto)
        {
            return await PostAsync<AppointmentResponseDto>("api/appointment/walk-in", dto);
        }
        public async Task<AppointmentResponseDto?> AssignScheduleAsync(AssignScheduleDto dto)
            => await PutAsync<AppointmentResponseDto>("api/appointment/assign", dto);

        // =====================================================================
        // PATIENT PROFILES
        // =====================================================================
        public async Task<List<PatientDto>?> GetMyPatientsAsync()
            => await GetAsync<List<PatientDto>>("api/patient/my-profiles");

        public async Task<PatientResponseDto?> CreatePatientAsync(PatientUpsertDto dto)
            => await PostAsync<PatientResponseDto>("api/patient", dto);

        public async Task<PatientResponseDto?> UpdatePatientAsync(PatientUpsertDto dto)
            => await PutAsync<PatientResponseDto>("api/patient", dto);

        public async Task<PatientResponseDto?> DeletePatientAsync(int patientId)
            => await DeleteAsync<PatientResponseDto>($"api/patient/{patientId}");

        // =====================================================================
        // REVIEW & RECOMMENDATION
        // =====================================================================
        public async Task<ReviewResponseDto?> SubmitReviewAsync(SubmitReviewDto dto)
            => await PostAsync<ReviewResponseDto>("api/review", dto);

        public async Task<List<DoctorRatingSummaryDto>?> GetReviewSummaryAsync()
            => await GetAsync<List<DoctorRatingSummaryDto>>("api/review/summary");

        public async Task<DoctorRecommendationResponseDto?> GetDoctorRecommendationAsync(string symptoms)
            => await PostAsync<DoctorRecommendationResponseDto>(
                "api/recommendation/doctor",
                new DoctorRecommendationRequestDto { Symptoms = symptoms });

        // =====================================================================
        // FORGOT PASSWORD
        // =====================================================================
        public async Task<AuthResponseDto?> ForgotPasswordAsync(ForgotPasswordDto dto)
            => await PostAsync<AuthResponseDto>("api/auth/forgot-password", dto);

        public async Task<AuthResponseDto?> ResetPasswordAsync(ResetPasswordDto dto)
            => await PostAsync<AuthResponseDto>("api/auth/reset-password", dto);

        // =====================================================================
        // CHAT AI
        // =====================================================================
        public async Task<string> AskChatAsync(string message, IEnumerable<object>? history = null)
        {
            AttachToken();
            var body = new { message, history };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PostAsync("api/chat", content);
                var raw = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(raw);
                return doc.RootElement.TryGetProperty("reply", out var r) ? r.GetString() ?? "" : "";
            }
            catch
            {
                return "Không kết nối được đến máy chủ.";
            }
        }
    }
}