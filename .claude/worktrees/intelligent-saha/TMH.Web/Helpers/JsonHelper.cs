namespace TMH.Web.Helpers
{
    /// <summary>
    /// Helper để escape JSON string trước khi nhúng vào HTML.
    /// Ngăn HTML parser đóng thẻ &lt;script&gt; sớm khi JSON chứa chuỗi "&lt;/script&gt;".
    /// </summary>
    public static class JsonHelper
    {
        public static string SafeJson(string? raw, string fallback = "[]")
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            return raw
                .Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase)
                .Replace("<!--", "<\\!--", StringComparison.Ordinal);
        }
    }
}
