using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SmartClinic.Services
{
    /// <summary>
    /// VNPay Sandbox Payment Service
    /// Tài liệu: https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html
    /// </summary>
    public class VNPayService
    {
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VNPayService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Tạo URL thanh toán VNPay
        /// </summary>
        public string CreatePaymentUrl(int prescriptionId, decimal amount, string patientName)
        {
            var vnpUrl = _config["VNPay:Url"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            var tmnCode = _config["VNPay:TmnCode"] ?? "DEMOV210";
            var hashSecret = _config["VNPay:HashSecret"] ?? "RAOEXHYVSDDIIENYWSLDIIZTANXUXZFJ";
            var returnUrl = _config["VNPay:ReturnUrl"] ?? "https://localhost:7062/cashier/vnpay-return";

            var vnpParams = new SortedDictionary<string, string>
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmnCode,
                // Amount × 100 (VNPay tính theo đơn vị đồng × 100)
                ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = $"{prescriptionId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                ["vnp_OrderInfo"] = $"Thanh toan don thuoc #{prescriptionId} - {patientName}",
                ["vnp_OrderType"] = "other",
                ["vnp_Locale"] = "vn",
                ["vnp_ReturnUrl"] = returnUrl,
                ["vnp_IpAddr"] = GetClientIp(),
                ["vnp_CreateDate"] = DateTime.Now.ToString("yyyyMMddHHmmss"),
                ["vnp_ExpireDate"] = DateTime.Now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
            };

            // Build query string + tạo checksum
            var queryBuilder = new StringBuilder();
            foreach (var kv in vnpParams)
            {
                queryBuilder.Append(WebUtility.UrlEncode(kv.Key));
                queryBuilder.Append('=');
                queryBuilder.Append(WebUtility.UrlEncode(kv.Value));
                queryBuilder.Append('&');
            }
            // Bỏ dấu & cuối
            var queryString = queryBuilder.ToString().TrimEnd('&');

            var secureHash = HmacSha512(hashSecret, queryString);
            return $"{vnpUrl}?{queryString}&vnp_SecureHash={secureHash}";
        }

        /// <summary>
        /// Xác thực chữ ký VNPay khi callback về
        /// </summary>
        public bool ValidateSignature(IQueryCollection query, out string txnRef, out bool isSuccess)
        {
            txnRef = query["vnp_TxnRef"].ToString();
            var responseCode = query["vnp_ResponseCode"].ToString();
            isSuccess = responseCode == "00";

            var hashSecret = _config["VNPay:HashSecret"] ?? "RAOEXHYVSDDIIENYWSLDIIZTANXUXZFJ";
            var receivedHash = query["vnp_SecureHash"].ToString();

            // Build lại chuỗi ký (bỏ vnp_SecureHash)
            var sortedParams = new SortedDictionary<string, string>();
            foreach (var key in query.Keys)
            {
                if (key.StartsWith("vnp_") && key != "vnp_SecureHash" && key != "vnp_SecureHashType")
                    sortedParams[key] = query[key].ToString();
            }

            var sb = new StringBuilder();
            foreach (var kv in sortedParams)
            {
                sb.Append(WebUtility.UrlEncode(kv.Key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(kv.Value));
                sb.Append('&');
            }
            var rawData = sb.ToString().TrimEnd('&');
            var expectedHash = HmacSha512(hashSecret, rawData);

            return string.Equals(expectedHash, receivedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string HmacSha512(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hash).ToLower();
        }

        private string GetClientIp()
        {
            var ctx = _httpContextAccessor.HttpContext;
            return ctx?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }
    }

    /// <summary>
    /// Kết quả VNPay callback
    /// </summary>
    public class VNPayCallbackResult
    {
        public bool IsValid { get; set; }
        public bool IsSuccess { get; set; }
        public int PrescriptionId { get; set; }
        public string ResponseCode { get; set; } = "";
        public string Message { get; set; } = "";
    }
}