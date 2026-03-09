using System.Text;
using System.Text.Json;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using ELearning_ToanHocHay_Control.Models.DTOs.Chatbot;
using ELearning_ToanHocHay_Control.Models.DTOs.AI;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIService> _logger;

        public AIService(HttpClient httpClient, IConfiguration configuration, ILogger<AIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Configure HttpClient to call Flask AI server
            var baseUrl = configuration["AI:PythonServerUrl"] ?? "http://localhost:5000";
            _logger.LogInformation($"[AIService] Initializing with BaseUrl: {baseUrl}");
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        private async Task<HttpResponseMessage> PostWithRetryAsync(string endpoint, string jsonContent)
        {
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)response.StatusCode == 429) 
                {
                    if (i == maxRetries - 1) return response;
                    var delay = Math.Pow(2, i) * 1000;
                    _logger.LogWarning($"Rate limit hit (429). Retrying in {delay}ms...");
                    await Task.Delay((int)delay);
                    continue;
                }
                return response;
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }

        // ==================== HINT GENERATION ====================
        public async Task<string> GenerateHintAsync(string prompt)
        {
            return await SendPromptAsync(prompt, temperature: 0.3);
        }

        // ==================== FEEDBACK GENERATION ====================
        public async Task<string> GenerateFeedbackAsync(string prompt)
        {
            return await SendPromptAsync(prompt, temperature: 0.2);
        }

        // ==================== STRUCTURED RESPONSES ====================

        public async Task<AIHintResponse?> GenerateHintStructuredAsync(AIHintRequest request)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request);
                var response = await PostWithRetryAsync("/api/hint", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"AI Hint API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AIHintResponse>(responseContent, GetJsonOptions());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating hint: {ex.Message}");
                return null;
            }
        }

        public async Task<AIFeedbackResponse?> GenerateFeedbackStructuredAsync(AIFeedbackRequest request)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request);
                var response = await PostWithRetryAsync("/api/feedback", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"AI Feedback API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AIFeedbackResponse>(responseContent, GetJsonOptions());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating feedback: {ex.Message}");
                return null;
            }
        }

        public async Task<AIInsightResponse?> GenerateInsightStructuredAsync(AIInsightRequest request)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request);
                var response = await PostWithRetryAsync("/api/ai-insights", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"AI Insight API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AIInsightResponse>(responseContent, GetJsonOptions());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating insight: {ex.Message}");
                return null;
            }
        }

        // ==================== CORE METHOD (Legacy Support) ==================
        private async Task<string> SendPromptAsync(string prompt, double temperature)
        {
            try
            {
                var hintRequest = new AIHintRequest
                {
                    QuestionText = prompt,
                    DifficultyLevel = "Medium",
                    HintLevel = 1
                };

                var result = await GenerateHintStructuredAsync(hintRequest);
                return result?.HintText ?? "AI không thể tạo gợi ý.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SendPromptAsync: {ex.Message}");
                return "AI không thể tạo gợi ý.";
            }
        }

        // ==================== CHATBOT METHODS ====================
        public async Task<ChatbotResponse?> SendChatbotMessageAsync(ChatbotMessageRequest request)
        {
            return await PostChatbotAsync("/api/chatbot/message", request);
        }

        public async Task<ChatbotResponse?> SendChatbotQuickReplyAsync(ChatbotQuickReplyRequest request)
        {
            return await PostChatbotAsync("/api/chatbot/quick-reply", request);
        }

        public async Task<ChatbotResponse?> SendChatbotTriggerAsync(ChatbotTriggerRequest request)
        {
            return await PostChatbotAsync("/api/chatbot/trigger", request);
        }

        private async Task<ChatbotResponse?> PostChatbotAsync<T>(string endpoint, T request)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(request);
                var response = await PostWithRetryAsync(endpoint, jsonContent);

                if (!response.IsSuccessStatusCode) return null;

                var responseString = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ChatbotResponse>(responseString, GetJsonOptions());
            }
            catch { return null; }
        }
    }
}