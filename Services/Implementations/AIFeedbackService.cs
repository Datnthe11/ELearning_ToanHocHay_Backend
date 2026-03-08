using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.AIFeedback;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using ELearning_ToanHocHay_Control.Models.DTOs.AI;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class AIFeedbackService : IAIFeedbackService
    {
        private readonly IAIFeedbackRepository _feedbackRepository;
        private readonly IExerciseAttemptRepository _attemptRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAIService _aiService;
        private readonly ILogger<AIFeedbackService> _logger;

        public AIFeedbackService(
            IAIFeedbackRepository feedbackRepository,
            IExerciseAttemptRepository attemptRepository,
            IQuestionRepository questionRepository,
            IAIService aiService,
            ILogger<AIFeedbackService> logger)
        {
            _feedbackRepository = feedbackRepository;
            _attemptRepository = attemptRepository;
            _questionRepository = questionRepository;
            _aiService = aiService;
            _logger = logger;
        }

        public async Task<ApiResponse<AIFeedbackDto>> CreateAsync(CreateAIFeedbackDto dto)
        {
            // Check Attempt
            var attempt = await _attemptRepository.GetAttemptWithDetailsAsync(dto.AttemptId);
            if (attempt == null)
                return ApiResponse<AIFeedbackDto>.ErrorResponse("Attempt not found");

            // Check Question
            var question = await _questionRepository.GetQuestionByIdAsync(dto.QuestionId);
            if (question == null)
                return ApiResponse<AIFeedbackDto>.ErrorResponse("Question not found");

            string fullSolution = dto.FullSolution ?? string.Empty;
            string mistakeAnalysis = dto.MistakeAnalysis ?? string.Empty;
            string improvementAdvice = dto.ImprovementAdvice ?? string.Empty;

            // KIỂM TRA XEM ĐÃ CÓ FEEDBACK CHƯA
            var existingFeedbacks = await _feedbackRepository.GetByAttemptAsync(dto.AttemptId);
            var existing = existingFeedbacks.FirstOrDefault(f => f.QuestionId == dto.QuestionId);
            if (existing != null)
            {
                return ApiResponse<AIFeedbackDto>.SuccessResponse(MapToDto(existing), "Feedback already exists");
            }

            // Nếu dữ liệu trống, gọi AI sinh mới
            if (string.IsNullOrWhiteSpace(fullSolution))
            {
                _logger.LogInformation($"Generating AI feedback for Question {dto.QuestionId}, Attempt {dto.AttemptId}");

                // Lấy câu trả lời của học sinh cho câu hỏi này trong lượt làm bài
                var studentAnswer = attempt.StudentAnswers?.FirstOrDefault(a => a.QuestionId == dto.QuestionId);
                
                var aiRequest = new AIFeedbackRequest
                {
                    QuestionText = question.QuestionText ?? string.Empty,
                    QuestionType = question.QuestionType.ToString(),
                    StudentAnswer = dto.StudentAnswer ?? "Không có câu trả lời",
                    CorrectAnswer = question.CorrectAnswer ?? string.Empty,
                    // So sánh đúng: câu trả lời của học sinh có trùng với đáp án đúng không 
                    IsCorrect = !string.IsNullOrWhiteSpace(dto.StudentAnswer)
                                && !dto.StudentAnswer.Equals("Bạn chưa trả lời câu hỏi này")
                                && dto.StudentAnswer.Trim().Equals(question.CorrectAnswer?.Trim() ?? "", StringComparison.OrdinalIgnoreCase),
                    Explanation = question.Explanation,
                    AttemptId = dto.AttemptId,
                    QuestionId = dto.QuestionId,
                    QuestionImageUrl = question.QuestionImageUrl,
                    Options = question.QuestionOptions?.Select(o => new AIOptionDto
                    {
                        OptionId = o.OptionId,
                        OptionText = o.OptionText ?? string.Empty,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                };

                var aiResponse = await _aiService.GenerateFeedbackStructuredAsync(aiRequest);

                if (aiResponse != null && aiResponse.Status == "success")
                {
                    fullSolution = aiResponse.FullSolution;
                    mistakeAnalysis = aiResponse.MistakeAnalysis;
                    improvementAdvice = aiResponse.ImprovementAdvice;
                }
                else
                {
                    _logger.LogError("AI Service failed to generate feedback");
                    return ApiResponse<AIFeedbackDto>.ErrorResponse("AI Service failed to generate feedback");
                }
            }

            var feedback = new AIFeedback
            {
                AttemptId = dto.AttemptId,
                QuestionId = dto.QuestionId,
                FullSolution = fullSolution,
                MistakeAnalysis = mistakeAnalysis,
                ImprovementAdvice = improvementAdvice
            };

            var created = await _feedbackRepository.CreateAsync(feedback);

            return ApiResponse<AIFeedbackDto>.SuccessResponse(
                MapToDto(created),
                "AI feedback created successfully"
            );
        }

        public async Task<ApiResponse<bool>> DeleteAsync(int feedbackId)
        {
            var success = await _feedbackRepository.DeleteAsync(feedbackId);

            return success
                ? ApiResponse<bool>.SuccessResponse(true, "Feedback deleted")
                : ApiResponse<bool>.ErrorResponse("Feedback not found");
        }

        public async Task<ApiResponse<IEnumerable<AIFeedbackDto>>> GetByAttemptAsync(int attemptId)
        {
            var feedbacks = await _feedbackRepository.GetByAttemptAsync(attemptId);

            return ApiResponse<IEnumerable<AIFeedbackDto>>.SuccessResponse(
                feedbacks.Select(MapToDto)
            );
        }

        public async Task<ApiResponse<AIFeedbackDto>> GetByIdAsync(int feedbackId)
        {
            var feedback = await _feedbackRepository.GetByIdAsync(feedbackId);
            if (feedback == null)
                return ApiResponse<AIFeedbackDto>.ErrorResponse("Feedback not found");

            return ApiResponse<AIFeedbackDto>.SuccessResponse(MapToDto(feedback));
        }

        public async Task<ApiResponse<AIFeedbackDto>> UpdateAsync(int feedbackId, UpdateAIFeedbackDto dto)
        {
            var existing = await _feedbackRepository.GetByIdAsync(feedbackId);
            if (existing == null)
                return ApiResponse<AIFeedbackDto>.ErrorResponse("Feedback not found");

            existing.FullSolution = dto.FullSolution;
            existing.MistakeAnalysis = dto.MistakeAnalysis;
            existing.ImprovementAdvice = dto.ImprovementAdvice;

            var updated = await _feedbackRepository.UpdateAsync(existing);

            return ApiResponse<AIFeedbackDto>.SuccessResponse(
                MapToDto(updated!),
                "Feedback updated successfully"
            );
        }

        private static AIFeedbackDto MapToDto(AIFeedback f)
        {
            return new AIFeedbackDto
            {
                FeedbackId = f.FeedbackId,
                AttemptId = f.AttemptId,
                QuestionId = f.QuestionId,
                FullSolution = f.FullSolution,
                MistakeAnalysis = f.MistakeAnalysis,
                ImprovementAdvice = f.ImprovementAdvice,
                CreatedAt = f.CreatedAt
            };
        }
    }
}
