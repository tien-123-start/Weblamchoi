using Microsoft.AspNetCore.SignalR;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Weblamchoi.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ChatHub> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendMessage(string user, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Vui lòng nhập tin nhắn.");
                return;
            }

            _logger.LogInformation("Received message from {User}: {Message}", user, message);
            await Clients.All.SendAsync("ReceiveMessage", user, message);

            try
            {
                var client = _httpClientFactory.CreateClient("xAIClient");
                var endpoint = "/chat/completions";
                var fullUrl = new Uri(client.BaseAddress, endpoint).ToString();
                _logger.LogInformation("Calling xAI API at: {Url}", fullUrl);

                var requestBody = new
                {
                    model = "grok-beta", // Thử model này
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful assistant." },
                        new { role = "user", content = message }
                    },
                    max_tokens = 256,
                    temperature = 0.2
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                _logger.LogDebug("Request body: {RequestBody}", JsonSerializer.Serialize(requestBody));

                var response = await client.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Grok API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "System", "Lỗi 404: Endpoint hoặc model không tồn tại. Vui lòng kiểm tra https://docs.x.ai hoặc thử model khác.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "System", "Lỗi quyền: Tài khoản xAI thiếu tín dụng hoặc quyền truy cập. Kiểm tra tại https://console.x.ai.");
                    }
                    else
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "System", $"Lỗi API: {errorContent}");
                    }
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Response body: {ResponseBody}", json);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var messageProp) &&
                    messageProp.TryGetProperty("content", out var contentProp))
                {
                    var answer = contentProp.GetString();
                    _logger.LogInformation("Grok response: {Answer}", answer);
                    await Clients.All.SendAsync("ReceiveMessage", "Grok", answer);
                }
                else
                {
                    _logger.LogError("Unable to parse Grok API response: {Json}", json);
                    await Clients.Caller.SendAsync("ReceiveMessage", "System", "Không thể xử lý phản hồi từ API.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Grok API request");
                await Clients.Caller.SendAsync("ReceiveMessage", "System", "Lỗi hệ thống, vui lòng thử lại.");
            }
        }
    }
}