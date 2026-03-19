using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.Question;

namespace ELearning_ToanHocHay_Control.Services.Interfaces
{
    public interface IQuestionService
    {
        Task<ApiResponse<QuestionDto>> CreateQuestionAsync(CreateQuestionDto dto);
        Task<ApiResponse<List<QuestionDto>>> CreateQuestionsAsync(List<CreateQuestionDto> dtos);

        // (Nếu có các hàm khác cũ thì giữ nguyên, chỉ thêm hàm trên vào)
    }
}