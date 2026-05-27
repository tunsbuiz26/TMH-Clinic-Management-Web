using System.Text.Json;
using System.Text.Json.Serialization;

namespace TMH.API.Helpers
{
    /// <summary>
    /// Đảm bảo mọi DateTime được serialize ra JSON đều có suffix 'Z' (UTC ISO 8601).
    /// Mặc định .NET serialize DateTime.UtcNow thành "2026-05-09T05:22:00" — không có 'Z'
    /// khiến JavaScript parse sai múi giờ (browser coi là local time thay vì UTC).
    /// </summary>
    public class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            // Khi đọc vào, đảm bảo Kind là UTC
            return value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // Luôn output dạng "yyyy-MM-ddTHH:mm:ss.fffZ"
            var utc = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
    }
}
