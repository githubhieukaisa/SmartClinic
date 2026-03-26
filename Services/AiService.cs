using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartClinic.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private const string GEMINI_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent";

        public AiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<MedicalRecordResult?> RefineMedicalRecordAsync(string rawInput)
        {
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrWhiteSpace(rawInput)) return null;

            var prompt = $@"Bạn là một trợ lý y tế chuyên nghiệp. Bác sĩ vừa nhập ghi chú nhanh: '{rawInput}'. 
                            Hãy chuẩn hóa thông tin này thành một hồ sơ bệnh án đầy đủ bằng Tiếng Việt, chuyên nghiệp và chuẩn y khoa.
                            Trả về kết quả duy nhất dưới định dạng JSON (không có markdown code block) với các trường:
                            - diagnosis: Tóm tắt chẩn đoán ngắn gọn, đầy đủ thuật ngữ.
                            - treatment: Kế hoạch điều trị chi tiết, lời khuyên sinh hoạt.
                            - notes: Các ghi chú lâm sàng hoặc lưu ý đặc biệt.
                            
                            Lưu ý: Nếu ghi chú quá sơ sài, hãy tự suy luận các lời khuyên y tế phổ biến tương ứng.";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    response_mime_type = "application/json"
                }
            };

            var response = await _httpClient.PostAsJsonAsync($"{GEMINI_BASE_URL}?key={apiKey}", requestBody);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<GeminiResponse>();
                var content = json?.Candidates?[0]?.Content?.Parts?[0]?.Text;

                if (!string.IsNullOrEmpty(content))
                {
                    return JsonSerializer.Deserialize<MedicalRecordResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }

            return null;
        }
    }

    public class MedicalRecordResult
    {
        public string Diagnosis { get; set; } = "";
        public string Treatment { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    // DTO cho Gemini Response
    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }
    }

    public class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    public class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
