
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TMH.API.Helpers
{
    public class VnPayLibrary
    {
        private readonly SortedList<string, string> _requestData = new(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new(new VnPayCompare());

        // ================= REQUEST =================
        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                _requestData[key] = value;
        }

        public string CreateRequestUrl(string baseUrl, string hashSecret)
        {
            var data = _requestData
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key);

            StringBuilder query = new();
            StringBuilder hashData = new();

            foreach (var (key, value) in data)
            {
                var encodedValue = WebUtility.UrlEncode(value);

                if (query.Length > 0)
                {
                    query.Append('&');
                    hashData.Append('&');
                }

                query.Append($"{key}={encodedValue}");
                hashData.Append($"{key}={encodedValue}");
            }

            var secureHash = HmacSHA512(hashSecret, hashData.ToString());

            return $"{baseUrl}?{query}&vnp_SecureHash={secureHash}";
        }
        // ================= RESPONSE =================
        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                _responseData[key] = value;
        }

        public string GetResponseData(string key)
        {
            return _responseData.TryGetValue(key, out var val) ? val : string.Empty;
        }

        public bool ValidateSignature(string inputHash, string hashSecret)
        {
            var data = _responseData
                .Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                .OrderBy(kv => kv.Key);

            StringBuilder hashData = new();

            foreach (var (key, value) in data)
            {
                if (hashData.Length > 0)
                    hashData.Append('&');

                // ⚠️ QUAN TRỌNG: phải decode trước khi hash
                hashData.Append($"{key}={WebUtility.UrlDecode(value)}");
            }

            string computedHash = HmacSHA512(hashSecret, hashData.ToString());

            return computedHash.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        // ================= HASH =================
        public static string HmacSHA512(string key, string inputData)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));

            StringBuilder sb = new();
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        // ================= IP =================
        public static string GetIpAddress(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(ip))
                ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            return string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip;
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return string.CompareOrdinal(x, y);
        }
    }
}
