using ELearning_ToanHocHay_Control.Models.DTOs;

namespace ELearning_ToanHocHay_Control.Services.Interfaces
{
    public interface ICurriculumService
    {
        Task<ApiResponse<CurriculumDto>> CreateAsync(CreateCurriculumDto dto, int currentUserId);
        Task<ApiResponse<IEnumerable<CurriculumDto>>> GetAllAsync();
        Task<ApiResponse<CurriculumDto>> GetByIdAsync(int curriculumId);
        Task<ApiResponse<CurriculumDto>> UpdateAsync(int curriculumId, UpdateCurriculumDto dto);
        Task<ApiResponse<bool>> PublishAsync(int curriculumId);
        Task<ApiResponse<bool>> ArchiveAsync(int curriculumId);
        Task<ApiResponse<bool>> DeleteAsync(int curriculumId);
    }
}
