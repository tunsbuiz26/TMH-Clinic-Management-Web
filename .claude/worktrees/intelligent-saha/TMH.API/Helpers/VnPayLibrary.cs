using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TMH.API.Helpers
{
    public class VnPayLibrary
    {
        private SortedList<string, string> _requestData = new(new VnPayCompare());
        private SortedList<string, string> _responseData = new(new VnPayCompare());

        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) _requestData[key] = value;
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value)) _responseData[key] = value;
        }

        public string GetResponseData(string key) =>
            _responseData.TryGetValue(key, out var v) ? v : string.Empty;

        public string CreateRequestUrl(string baseUrl, string hashSecret)
        {
            // Chuỗi ký: raw value, không encode, sort theo VnPayCompare (ordinal)
            string signData = string.Join("&", _requestData
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={kv.Value}"));

            string secureHash = HmacSHA512(hashSecret, signData);

            // Query string: encode theo WebUtility (space → +)
            string queryString = string.Join("&", _requestData
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));

            return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
        }

        public bool ValidateSignature(string inputHash, string hashSecret)
        {
            string signData = string.Join("&", _responseData
                .Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                .Select(kv => $"{kv.Key}={kv.Value}"));

            return HmacSHA512(hashSecret, signData)
                .Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string HmacSHA512(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
                .Replace("-", "").ToLower();
        }

        public static string GetIpAddress(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip;
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);
    }
}