using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.Subscription;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _repository;
        private readonly IPackageRepository _packageRepository;

        public SubscriptionService(
            ISubscriptionRepository repository,
            IPackageRepository packageRepository)
        {
            _repository = repository;
            _packageRepository = packageRepository;
        }

        public async Task<ApiResponse<IEnumerable<SubscriptionDto>>> GetAllAsync()
        {
            var subs = await _repository.GetAllAsync();

            var data = subs.Select(x => new SubscriptionDto
            {
                SubscriptionId = x.SubscriptionId,
                StudentId = x.StudentId,
                PaymentId = x.PaymentId,
                PackageId = x.PackageId,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                AmountPaid = x.AmountPaid,
                CreatedAt = x.CreatedAt,
            });

            return ApiResponse<IEnumerable<SubscriptionDto>>
                .SuccessResponse(data, "Lấy danh sách subscription thành công");
        }

        public async Task<ApiResponse<SubscriptionDto>> GetByIdAsync(int id)
        {
            var sub = await _repository.GetByIdAsync(id);
            if (sub == null)
                return ApiResponse<SubscriptionDto>
                    .ErrorResponse("Không tìm thấy subscription");

            var dto = new SubscriptionDto
            {
                SubscriptionId = sub.SubscriptionId,
                StudentId = sub.StudentId,
                PackageId = sub.PackageId,
                PaymentId = sub.PaymentId,
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                Status = sub.Status,
                AmountPaid = sub.AmountPaid,
                CreatedAt = sub.CreatedAt,
            };

            return ApiResponse<SubscriptionDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<bool>> CancelAsync(int id)
        {
            var sub = await _repository.GetByIdAsync(id);
            if (sub == null)
                return ApiResponse<bool>.ErrorResponse("Subscription không tồn tại");

            sub.Status = SubscriptionStatus.Cancelled;
            await _repository.UpdateAsync(sub);

            return ApiResponse<bool>.SuccessResponse(true, "Hủy subscription thành công");
        }

        public async Task<ApiResponse<bool>> CheckPremiumAsync(int studentId)
        {
            var active = await _repository.GetActiveByStudentAsync(studentId);
            return ApiResponse<bool>.SuccessResponse(active != null);
        }

        /// <summary>
        /// Implement method từ ISubscriptionService — dùng trong CoreDashboardService.
        /// </summary>
        public async Task<SubscriptionInfoDto> GetActiveSubscriptionInfoAsync(int studentId)
        {
            var activeSubscription = await _packageRepository.GetActivePackageAsync(studentId);
            return SubscriptionInfoHelper.BuildSubscriptionInfo(activeSubscription);
        }
    }

    // ── Tách ra ngoài class, cùng namespace ──────────────────────────────────
    public static class SubscriptionInfoHelper
    {
        public static SubscriptionInfoDto BuildSubscriptionInfo(Subscription? activeSubscription)
        {
            if (activeSubscription == null ||
                activeSubscription.Status != SubscriptionStatus.Active ||
                activeSubscription.EndDate < DateTime.UtcNow)
            {
                return new SubscriptionInfoDto
                {
                    PackageType = 0,
                    PackageName = "Free",
                    IsActive = false,
                    AiHintLimitDaily = 0,
                    UnlimitedAiHint = false
                };
            }

            var pkg = activeSubscription.Package!;
            var name = pkg.PackageName.ToLower().Trim();

            // FIX: match đúng tên trong DB
            // "Gói Premium"      → 2
            // "Gói tiêu chuẩn"   → 1
            // "Gói trải nghiệm"  → 0 (free, nhưng có subscription thì vẫn active)
            int packageType = name switch
            {
                var n when n.Contains("premium") => 2,
                var n when n.Contains("tiêu chuẩn") || n.Contains("standard") || n.Contains("tieu chuan") => 1,
                var n when n.Contains("trải nghiệm") || n.Contains("trai nghiem") || n.Contains("free") => 0,
                _ => 0
            };

            return new SubscriptionInfoDto
            {
                PackageType = packageType,
                PackageName = pkg.PackageName,
                IsActive = true,
                EndDate = activeSubscription.EndDate,
                DaysRemaining = (int)(activeSubscription.EndDate - DateTime.UtcNow).TotalDays,
                UnlimitedAiHint = pkg.UnlimitedAiHint,
                AiHintLimitDaily = pkg.AiHintLimitDaily,
                PersonalizedPath = pkg.PersonalizedPath,
                MistakeRetry = pkg.MistakeRetry,
                SmartReminder = pkg.SmartReminder,
                PrioritySupport = pkg.PrioritySupport
            };
        }
    }
}