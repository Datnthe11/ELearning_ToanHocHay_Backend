using ELearning_ToanHocHay_Control.Data.Entities;

namespace ELearning_ToanHocHay_Control.Repositories.Interfaces
{
    public interface IQuestionRepository
    {
        // Hàm lấy 1 câu hỏi (kèm đáp án) theo ID
        Task<Question?> GetQuestionByIdAsync(int id);

        Task<Question> CreateAsync(Question question);
        Task<List<Question>> CreateMultipleAsync(List<Question> questions);
    }
}