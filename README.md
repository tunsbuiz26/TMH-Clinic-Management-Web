# TMH Solution — Hệ thống Quản lý Phòng khám Tai Mũi Họng

Ứng dụng web fullstack phục vụ quy trình vận hành của một phòng khám chuyên khoa Tai Mũi Họng: từ đặt lịch hẹn trực tuyến, quản lý ca khám, kê đơn thuốc, đến thanh toán và nhắn tin nội bộ — tất cả trong một hệ thống liền mạch.

---

## Kiến trúc tổng quan

Dự án được tổ chức theo mô hình **multi-project .NET Solution** gồm 3 layer tách biệt:

```
TMH_Solution/
├── TMH.API/        # Backend REST API (ASP.NET Core 8)
├── TMH.Web/        # Frontend MVC (ASP.NET Core 8 + Razor Views)
└── TMH.Shared/     # Models, DTOs, Enums dùng chung
```

`TMH.Web` đóng vai trò client — gọi `TMH.API` qua HTTP và proxy các yêu cầu real-time qua SignalR. Hai project không dùng chung DbContext mà giao tiếp thuần qua API, giúp tách biệt rõ trách nhiệm và dễ scale độc lập.

---

## Tech Stack

| Layer | Công nghệ |
|---|---|
| Backend API | ASP.NET Core 8, Entity Framework Core 8, SQL Server |
| Frontend | ASP.NET Core MVC, Razor Views, JavaScript, Bootstrap |
| Xác thực | JWT Bearer Authentication, BCrypt password hashing |
| Real-time | SignalR (WebSocket) |
| Email | MailKit (SMTP) |
| Thanh toán | VNPay Payment Gateway |
| AI | Anthropic Claude API |
| Tài liệu API | Swagger / OpenAPI |

---

## Tính năng chính

### Dành cho Bệnh nhân
- Đăng ký tài khoản, đăng nhập, quản lý hồ sơ cá nhân (nhóm máu, CCCD, địa chỉ...)
- **Đặt lịch khám trực tuyến**: chọn bác sĩ → chọn khung giờ còn trống → xác nhận
- Xem lịch sử khám bệnh, kết quả khám, đơn thuốc chi tiết (tên thuốc, liều lượng, tần suất, hướng dẫn dùng)
- Đánh giá bác sĩ sau khi hoàn thành ca khám (thang 1–5 sao + bình luận)
- Nhận thông báo nhắc lịch qua email tự động mỗi ngày lúc 08:00
- Thanh toán viện phí online qua **VNPay**
- Nhắn tin với nhân viên phòng khám (chat real-time)
- **Gợi ý bác sĩ bằng AI**: mô tả triệu chứng → hệ thống tự động gợi ý bác sĩ phù hợp nhất dựa trên điểm đánh giá, kinh nghiệm và lịch còn trống

### Dành cho Bác sĩ
- Dashboard quản lý lịch khám trong ngày và lịch sắp tới
- Cập nhật trạng thái ca khám (chờ khám → đang khám → hoàn thành / hủy)
- Kê đơn thuốc trực tiếp trên hệ thống (nhiều loại thuốc/đơn, ghi chú tái khám)
- Xem hồ sơ bệnh nhân và lịch sử các lần khám trước
- Quản lý lịch làm việc cá nhân (WorkSchedule)

### Dành cho Nhân viên (Staff)
- Phân công bác sĩ và khung giờ cho các lịch hẹn đang chờ
- Quản lý hồ sơ bệnh nhân toàn bộ phòng khám
- Chat real-time với bệnh nhân để hỗ trợ tư vấn

### Dành cho Admin
- Quản lý toàn bộ tài khoản người dùng (bác sĩ, nhân viên, bệnh nhân)
- Dashboard thống kê tổng quan hoạt động phòng khám
- Quản lý bài viết / tin tức y tế đăng lên website

---

## Điểm nổi bật kỹ thuật

**Tích hợp AI (Anthropic Claude API)** — Tính năng gợi ý bác sĩ tự động sử dụng Claude để phân tích triệu chứng và đối chiếu với hồ sơ bác sĩ theo 4 tiêu chí có trọng số (điểm đánh giá, bằng cấp, lịch trống, mô tả chuyên môn). Có fallback scoring nội bộ nếu API không khả dụng.

**Real-time Chat qua SignalR** — Hệ thống nhắn tin hai chiều giữa bệnh nhân và nhân viên, với JWT authentication được truyền qua query string cho WebSocket connection.

**Background Services** — Hai hosted service chạy nền: nhắc lịch khám qua email hàng ngày lúc 08:00 và tự động reset trạng thái tin nhắn lúc 12:00.

**Phân quyền 4 cấp** — Admin / Doctor / Staff / Patient với JWT Claims-based authorization, mỗi role có dashboard và luồng nghiệp vụ riêng.

**Kiến trúc Shared Project** — Models, DTOs, Enums được đặt trong `TMH.Shared` — dùng chung giữa API và Web, tránh duplicate code và đảm bảo type-safety xuyên suốt solution.

---

## Cơ sở dữ liệu

SQL Server với EF Core Code-First Migrations. Các entity chính:

`Users` → `Doctors` / `Patients` → `Appointments` → `Prescriptions` / `Reviews` / `Payments`

`WorkSchedules` (lịch làm việc bác sĩ) · `Conversations` + `Messages` (chat) · `Notifications` · `Articles`
