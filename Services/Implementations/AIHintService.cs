using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.AIHint;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using ELearning_ToanHocHay_Control.Models.DTOs.AI;
using System.Linq;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class AIHintService : IAIHintService
    {
        private readonly IAIHintRepository _hintRepository;
        private readonly IExerciseAttemptRepository _attemptRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAIService _aiService;
        private readonly ILogger<AIHintService> _logger;

        public AIHintService(
            IAIHintRepository hintRepository,
            IExerciseAttemptRepository attemptRepository,
            IQuestionRepository questionRepository,
            IAIService aiService,
            ILogger<AIHintService> logger)
        {
            _hintRepository = hintRepository;
            _attemptRepository = attemptRepository;
            _questionRepository = questionRepository;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<ApiResponse<AIHintDto>> CreateAsync(CreateAIHintDto dto)
        {
            // Check Attempt
            var attempt = await _attemptRepository.GetAttemptByIdAsync(dto.AttemptId);
            if (attempt == null)
                return ApiResponse<AIHintDto>.ErrorResponse("Attempt not found");

            // Check Question
            var question = await _questionRepository.GetQuestionByIdAsync(dto.QuestionId);
            if (question == null)
                return ApiResponse<AIHintDto>.ErrorResponse("Question not found");

            // Nếu HintText chưa có, gọi AI để sinh
            string hintText = dto.HintText ?? string.Empty;
            
            if (string.IsNullOrWhiteSpace(hintText))
            {
                _logger.LogInformation($"Generating AI hint for Question {dto.QuestionId}, Attempt {dto.AttemptId}");
                
                var aiRequest = new AIHintRequest
                {
                    QuestionText = question.QuestionText ?? string.Empty,
                    QuestionType = question.QuestionType.ToString(), // ✅ Convert Enum to String
                    DifficultyLevel = question.DifficultyLevel.ToString(), // ✅ Convert Enum to String
                    StudentAnswer = dto.StudentAnswer ?? "Chưa trả lời",
                    HintLevel = dto.HintLevel,
                    QuestionId = dto.QuestionId,
                    QuestionImageUrl = question.QuestionImageUrl, // ✅ Sửa từ ImageUrl -> QuestionImageUrl
                    Options = question.QuestionOptions?.Select(o => new AIOptionDto
                    {
                        OptionId = o.OptionId,
                        OptionText = o.OptionText ?? string.Empty,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                };

                var aiResponse = await _aiService.GenerateHintStructuredAsync(aiRequest);
                
                if (aiResponse == null || string.IsNullOrWhiteSpace(aiResponse.HintText))
                {
                    _logger.LogError("AI Service failed to generate hint");
                    return ApiResponse<AIHintDto>.ErrorResponse("Failed to generate hint from AI");
                }

                hintText = aiResponse.HintText;
                _logger.LogInformation($"AI hint generated successfully: {hintText.Substring(0, Math.Min(50, hintText.Length))}...");
            }



            var hint = new AIHint
            {
                AttemptId = dto.AttemptId,
                QuestionId = dto.QuestionId,
                HintText = hintText,
                HintLevel = dto.HintLevel
            };

            var created = await _hintRepository.CreateAsync(hint);

            return ApiResponse<AIHintDto>.SuccessResponse(
                MapToDto(created),
                "AI hint created successfully"
            );
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int hintId)
        {
            var success = await _hintRepository.DeleteAsync(hintId);

            return success
                ? ApiResponse<bool>.SuccessResponse(true, "Hint deleted")
                : ApiResponse<bool>.ErrorResponse("Hint not found");
        }

        public async Task<ApiResponse<IEnumerable<AIHintDto>>> GetByAttemptAsync(int attemptId)
        {
            var hints = await _hintRepository.GetByAttemptAsync(attemptId);

            return ApiResponse<IEnumerable<AIHintDto>>.SuccessResponse(
                hints.Select(MapToDto)
            );
        }

        public async Task<ApiResponse<IEnumerable<AIHintDto>>> GetByAttemptAndQuestionAsync(int attemptId, int questionId)
        {
            var hints = await _hintRepository.GetByAttemptAndQuestionAsync(attemptId, questionId);

            return ApiResponse<IEnumerable<AIHintDto>>.SuccessResponse(
                hints.Select(MapToDto)
            );
        }

        public async Task<ApiResponse<AIHintDto>> GetByIdAsync(int hintId)
        {
            var hint = await _hintRepository.GetByIdAsync(hintId);
            if (hint == null)
                return ApiResponse<AIHintDto>.ErrorResponse("Hint not found");

            return ApiResponse<AIHintDto>.SuccessResponse(MapToDto(hint));
        }

        public async Task<ApiResponse<AIHintDto>> UpdateAsync(int hintId, UpdateAIHintDto dto)
        {
            var existing = await _hintRepository.GetByIdAsync(hintId);
            if (existing == null)
                return ApiResponse<AIHintDto>.ErrorResponse("Hint not found");

            existing.HintText = dto.HintText;
            existing.HintLevel = dto.HintLevel;

            var updated = await _hintRepository.UpdateAsync(existing);

            return ApiResponse<AIHintDto>.SuccessResponse(
                MapToDto(updated!),
                "Hint updated successfully"
            );
        }

        private static AIHintDto MapToDto(AIHint h)
        {
            return new AIHintDto
            {
                HintId = h.HintId,
                AttemptId = h.AttemptId,
                QuestionId = h.QuestionId,
                HintText = h.HintText,
                HintLevel = h.HintLevel,
                CreatedAt = h.CreatedAt
            };
        }
    }
}
