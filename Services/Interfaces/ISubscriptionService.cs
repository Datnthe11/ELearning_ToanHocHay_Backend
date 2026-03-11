using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.Subscription;

namespace ELearning_ToanHocHay_Control.Services.Interfaces
{
    public interface ISubscriptionService
    {
        Task<ApiResponse<IEnumerable<SubscriptionDto>>> GetAllAsync();
        Task<ApiResponse<SubscriptionDto>> GetByIdAsync(int id);
        //Task<ApiResponse<SubscriptionDto>> CreateAsync(CreateSubscriptionDto dto);
        Task<ApiResponse<bool>> CancelAsync(int id);
        Task<ApiResponse<bool>> CheckPremiumAsync(int studentId);
        Task<SubscriptionInfoDto> GetActiveSubscriptionInfoAsync(int studentId);
        Task<ApiResponse<bool>> UpdateStatusAsync(int id, SubscriptionStatus newStatus);

    }
}
