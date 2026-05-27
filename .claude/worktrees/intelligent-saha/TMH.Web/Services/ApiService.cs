using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TMH.Shared.DTOs;

namespace TMH.Web.Services
{
    /// <summary>
    /// ApiService là lớp trung gian duy nhất giữa Web App và API.
    /// Mọi request HTTP đều đi qua đây — không viết HttpClient trực tiếp trong Controller.
    ///
    /// Lý do tập trung vào một lớp:
    ///   - Dễ thêm retry logic, logging, error handling mà không sửa nhiều chỗ.
    ///   - Controller Web App đọc gần giống Controller API: gọi service, nhận DTO, render View.
    ///   - Khi URL API thay đổi, chỉ sửa một nơi duy nhất.
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _http;
        private readonly IHttpContextAccessor _ctx;

        // JsonSerializerOptions tái sử dụng để tiết kiệm memory và đảm nhất quán
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true   // "token" và "Token" đều được nhận
        };

        public ApiService(HttpClient http, IHttpContextAccessor ctx)
        {
            _http = http;
            _ctx = ctx;
        }

        // =====================================================================
        // ĐĂNG KÝ — gọi POST /api/auth/register
        // =====================================================================
        public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
            => await PostAsync<AuthResponseDto>("api/auth/register", dto);

        // =====================================================================
        // ĐĂNG NHẬP — gọi POST /api/auth/login
        // =====================================================================
        public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
            => await PostAsync<AuthResponseDto>("api/auth/login", dto);

        // =====================================================================
        // HELPER: POST chung — serialize DTO → gọi API → deserialize response
        // =====================================================================
        private async Task<T?> PostAsync<T>(string endpoint, object body)
        {
            // Đính token vào header nếu người dùng đã đăng nhập
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
                // API không chạy hoặc mất kết nối → trả null, Controller sẽ hiển thị lỗi
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

        // =====================================================================
        // HELPER: GET chung — dùng cho các endpoint lấy dữ liệu
        // =====================================================================
        // Trả về raw JSON string — dùng cho Admin/Doctor forward, tránh mất key name khi re-serialize
        public async Task<string?> GetRawJsonAsync(string endpoint)
        {
            AttachToken();
            try
            {
                var response = await _http.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

                // POST và trả về raw JSON string — tránh mất dữ liệu khi re-serialize
        public async Task<string?> PostRawJsonAsync(string endpoint, object body)
        {
            AttachToken();
            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PostAsync(endpoint, content);
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] PostRawJsonAsync lỗi: {ex.Message}");
                return null;
            }
        }

        // PUT và trả về raw JSON string
        public async Task<string?> PutRawJsonAsync(string endpoint, object body)
        {
            AttachToken();
            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PutAsync(endpoint, content);
                return await response.Content.ReadAsStringAsync();
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
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[ApiService] DeleteRawJsonAsync lỗi: {ex.Message}");
                return null;
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

        // =====================================================================
        // Đính JWT token từ Session vào Authorization header
        // Session lưu token sau khi đăng nhập thành công
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
        // APPOINTMENT
        // =====================================================================
        public async Task<List<DoctorScheduleDto>?> GetAvailableDoctorsAsync(DateTime? date = null)
        {
            var url = date.HasValue
                ? $"api/appointment/available-doctors?date={date.Value:yyyy-MM-dd}"
                : "api/appointment/available-doctors";
            return await GetAsync<List<DoctorScheduleDto>>(url);
        }
        // Gọi API tạo URL thanh toán VNPay
        public async Task<string?> CreateVnPayUrlAsync(VnPaymentRequestDto dto)
        {
            // Dùng _ctx thay vì _httpContextAccessor
            var token = _ctx.HttpContext?.Session.GetString("JwtToken");
            if (token == null) return null;

            // Dùng _http thay vì _httpClient
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.PostAsJsonAsync("api/payment/create-payment-url", dto);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("url").GetString();
        }

        // Xử lý kết quả trả về từ VNPay
        public async Task<VnPaymentResponseDto?> GetPaymentResultAsync(string queryString)
        {
            // Dùng _http thay vì _httpClient
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
        // STAFF — Lễ tân: xem lịch theo ngày + cập nhật trạng thái
        // =====================================================================

        /// <summary>Lấy danh sách lịch khám theo ngày (dùng cho lễ tân và bác sĩ)</summary>
        public async Task<List<AppointmentDetailDto>?> GetAppointmentsByDateAsync(DateTime date, int? doctorId = null)
        {
            var url = $"api/appointment/by-date?date={date:yyyy-MM-dd}";
            if (doctorId.HasValue) url += $"&doctorId={doctorId.Value}";
            return await GetAsync<List<AppointmentDetailDto>>(url);
        }

        /// <summary>Cập nhật trạng thái lịch khám (DaDen, DangKham, HoanThanh, VangMat...)</summary>
        public async Task<AppointmentResponseDto?> UpdateAppointmentStatusAsync(UpdateAppointmentStatusDto dto)
            => await PutAsync<AppointmentResponseDto>("api/appointment/update-status", dto);
        // =====================================================================
        // PATIENT — Quản lý hồ sơ bệnh nhân
        // =====================================================================

        /// <summary>
        /// Lấy danh sách hồ sơ của user đang đăng nhập.
        /// Dùng để populate dropdown "Chọn hồ sơ" trên form đặt lịch.
        /// </summary>
        public async Task<List<PatientDto>?> GetMyPatientsAsync()
            => await GetAsync<List<PatientDto>>("api/patient/my-profiles");

        /// <summary>Tạo hồ sơ mới (bản thân hoặc người thân).</summary>
        public async Task<PatientResponseDto?> CreatePatientAsync(PatientUpsertDto dto)
            => await PostAsync<PatientResponseDto>("api/patient", dto);

        /// <summary>Cập nhật thông tin hồ sơ (dto.Id > 0).</summary>
        public async Task<PatientResponseDto?> UpdatePatientAsync(PatientUpsertDto dto)
            => await PutAsync<PatientResponseDto>("api/patient", dto);

        /// <summary>Xóa hồ sơ — chỉ xóa được nếu không còn lịch khám đang hoạt động.</summary>
        public async Task<PatientResponseDto?> DeletePatientAsync(int patientId)
            => await DeleteAsync<PatientResponseDto>($"api/patient/{patientId}");

        // =====================================================================
        // HELPER PUT / DELETE (bổ sung vào ApiService nếu chưa có)
        // =====================================================================

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

        // PostPublicAsync — dùng khi cần gọi POST từ Admin (không phải Patient)
        public async Task<T?> PostPublicAsync<T>(string endpoint, object body)
            => await PostAsync<T>(endpoint, body);

       
        

                // =====================================================================
        // CHAT — Gọi chatbot AI qua TMH.API
        // =====================================================================
        public async Task<string> AskChatAsync(string message, IEnumerable<object>? history = null)
        {
            AttachToken();
            var body = new { message, history };
            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            try
            {
                var response = await _http.PostAsync("api/chat", content);
                var raw      = await response.Content.ReadAsStringAsync();
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
