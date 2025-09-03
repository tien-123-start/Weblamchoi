using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace weblamchoi.Controllers
{
    [Route("AI")]
    [ApiController]
    public class AIController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIController> _logger;

        public AIController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<AIController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("GetResponse")]
        public async Task<IActionResult> GetResponse([FromForm] string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
            {
                _logger.LogWarning("Received empty prompt");
                return BadRequest(new { answer = "Empty message" });
            }

            _logger.LogInformation("Received prompt: {Prompt}", prompt);

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _configuration["xAI:ApiKey"]);

                var requestBody = new
                {
                    model = "grok-3-mini", // Hoặc "grok-3", kiểm tra tài liệu xAI để chọn mô hình phù hợp
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful assistant." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 1000, // Giới hạn độ dài phản hồi
                    temperature = 0.2 // Độ ngẫu nhiên thấp để phản hồi chính xác hơn
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(_configuration["xAI:BaseUrl"] + "/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API error: {ErrorContent}", errorContent);
                    try
                    {
                        using var errorDoc = JsonDocument.Parse(errorContent);
                        var errorMessage = errorDoc.RootElement.GetProperty("error").GetProperty("message").GetString();
                        return StatusCode((int)response.StatusCode, new { answer = $"API error: {errorMessage}" });
                    }
                    catch
                    {
                        return StatusCode((int)response.StatusCode, new { answer = $"API error: {errorContent}" });
                    }
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentProperty))
                {
                    var answer = contentProperty.GetString();
                    _logger.LogInformation("Received response: {Answer}", answer);
                    return Ok(new { answer });
                }

                _logger.LogError("Unable to parse response from API: {Json}", json);
                return BadRequest(new { answer = "Unable to parse response from API" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing request");
                return StatusCode(500, new { answer = "Internal server error" });
            }
        }
    }
}