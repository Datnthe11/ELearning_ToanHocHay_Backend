using AutoMapper;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.Question;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepository _questionRepository;

        public QuestionService(IQuestionRepository questionRepository)
        {
            _questionRepository = questionRepository;
        }

        public async Task<ApiResponse<QuestionDto>> CreateQuestionAsync(CreateQuestionDto dto)
        {
            try
            {
                var question = new Question
                {
                    BankId = dto.BankId,
                    QuestionText = dto.QuestionText,
                    QuestionImageUrl = dto.QuestionImageUrl,
                    QuestionType = dto.QuestionType,
                    DifficultyLevel = dto.DifficultyLevel,
                    CorrectAnswer = dto.CorrectAnswer,
                    Explanation = dto.Explanation,
                    Status = QuestionStatus.PendingReview,
                    // ID mẫu, sẽ lấy Id từ token
                    CreatedBy = 6,
                    CreatedAt = DateTime.UtcNow,
                    QuestionOptions = dto.Options?.Select(o => new QuestionOption
                    {
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect,
                        OrderIndex = o.OrderIndex
                    }).ToList()
                };

                // 2. Gọi Repository lưu vào DB
                var result = await _questionRepository.CreateAsync(question);

                // 3. Chuyển ngược lại từ Entity sang DTO (QuestionDto) để trả về
                var responseDto = new QuestionDto
                {
                    QuestionId = result.QuestionId,
                    QuestionText = result.QuestionText,
                    QuestionType = result.QuestionType,
                    DifficultyLevel = result.DifficultyLevel,
                    Options = result.QuestionOptions?.Select(o => new QuestionOptionDto
                    {
                        OptionId = o.OptionId,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                };

                return ApiResponse<QuestionDto>.SuccessResponse(responseDto, "Tạo câu hỏi thành công!");
            }
            catch (Exception ex)
            {
                return ApiResponse<QuestionDto>.ErrorResponse("Lỗi khi tạo câu hỏi", new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<List<QuestionDto>>> CreateQuestionsAsync(List<CreateQuestionDto> dtos)
        {
            try
            {
                var questions = dtos.Select(dto => new Question
                {
                    BankId = dto.BankId,
                    QuestionText = dto.QuestionText,
                    QuestionImageUrl = dto.QuestionImageUrl,
                    QuestionType = dto.QuestionType,
                    DifficultyLevel = dto.DifficultyLevel,
                    CorrectAnswer = dto.CorrectAnswer,
                    Explanation = dto.Explanation,
                    Status = QuestionStatus.PendingReview,
                    // ID mẫu, sẽ lấy Id từ token
                    CreatedBy = 6,
                    CreatedAt = DateTime.UtcNow,
                    QuestionOptions = dto.Options?.Select(o => new QuestionOption
                    {
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect,
                        OrderIndex = o.OrderIndex
                    }).ToList()
                }).ToList();

                // 2. Gọi Repository lưu vào DB
                var result = await _questionRepository.CreateMultipleAsync(questions);

                // 3. Chuyển ngược lại từ Entity sang DTO (QuestionDto) để trả về
                var responseDtos = result.Select(r => new QuestionDto
                {
                    QuestionId = r.QuestionId,
                    QuestionText = r.QuestionText,
                    QuestionType = r.QuestionType,
                    DifficultyLevel = r.DifficultyLevel,
                    Options = r.QuestionOptions?.Select(o => new QuestionOptionDto
                    {
                        OptionId = o.OptionId,
                        OptionText = o.OptionText,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                }).ToList();

                return ApiResponse<List<QuestionDto>>.SuccessResponse(responseDtos, "Tạo các câu hỏi thành công!");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuestionDto>>.ErrorResponse("Lỗi khi tạo các câu hỏi", new List<string> { ex.Message });
            }
        }
    }
}