using ELearning_ToanHocHay_Control.Data.Entities;

namespace ELearning_ToanHocHay_Control.Repositories.Interfaces
{
    public interface ICurriculumRepository
    {
        Task<Curriculum?> GetCurriculumByIdAsync(int curriculumId);
        Task<IEnumerable<Curriculum>> GetAllAsync();
        Task<Curriculum> CreateCurriculumAsync(Curriculum curriculum);
        Task<Curriculum?> UpdateCurriculumAsync(Curriculum curriculum);
        Task<bool> DeleteCurriculumAsync(int curriculumId);
        Task<bool> ExistsAsync(int id);
    }
}
