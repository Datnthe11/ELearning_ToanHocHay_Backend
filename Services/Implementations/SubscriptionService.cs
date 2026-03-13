using System.Threading.Tasks;
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
        private readonly SubscriptionInfoHelper _subscriptionInfoHelper;

        public SubscriptionService(
            ISubscriptionRepository repository,
            IPackageRepository packageRepository)
        {
            _repository = repository;
            _packageRepository = packageRepository;
            _subscriptionInfoHelper = new SubscriptionInfoHelper(packageRepository);
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
            return await _subscriptionInfoHelper.BuildSubscriptionInfo(activeSubscription);
        }

        // Services/Implementations/SubscriptionService.cs — thêm method
        public async Task<ApiResponse<bool>> UpdateStatusAsync(int id, SubscriptionStatus newStatus)
        {
            var sub = await _repository.GetByIdAsync(id);
            if (sub == null)
                return ApiResponse<bool>.ErrorResponse("Subscription không tồn tại");

            // ── Validate chuyển trạng thái hợp lệ ──────────────────────────────
            var allowed = new Dictionary<SubscriptionStatus, SubscriptionStatus[]>
            {
                [SubscriptionStatus.Pending] = [SubscriptionStatus.Active, SubscriptionStatus.Cancelled, SubscriptionStatus.Expired],
                [SubscriptionStatus.Active] = [SubscriptionStatus.Expired, SubscriptionStatus.Cancelled],
                [SubscriptionStatus.Expired] = [],   // trạng thái cuối
                [SubscriptionStatus.Cancelled] = [],   // trạng thái cuối
            };

            if (!allowed[sub.Status].Contains(newStatus))
                return ApiResponse<bool>.ErrorResponse(
                    $"Không thể chuyển từ '{sub.Status}' sang '{newStatus}'");

            // ── Cập nhật ────────────────────────────────────────────────────────
            sub.Status = newStatus;

            // Khi Active: ghi nhận ngày bắt đầu/kết thúc
            if (newStatus == SubscriptionStatus.Active)
            {
                sub.StartDate = DateTime.UtcNow;
                sub.EndDate = DateTime.UtcNow.AddMonths(1);

                if (sub.Payment != null)
                {
                    sub.Payment.Status = PaymentStatus.Completed;
                    sub.Payment.PaymentDate = DateTime.UtcNow;
                }
            }

            await _repository.UpdateAsync(sub);
            return ApiResponse<bool>.SuccessResponse(true, $"Cập nhật trạng thái thành '{newStatus}' thành công");
        }
    }

    // ── Tách ra ngoài class, cùng namespace ──────────────────────────────────
    public class SubscriptionInfoHelper
    {
        private readonly IPackageRepository _packageRepository;
        public SubscriptionInfoHelper (IPackageRepository packageRepository)
        {
            _packageRepository = packageRepository;
        }
        public async Task<SubscriptionInfoDto> BuildSubscriptionInfo(Subscription? activeSubscription)
        {
            var package = await _packageRepository.GetByIdAsync(activeSubscription.PackageId);
            int packageType = 0; // FREE
            if (package.PackageName == "Gói trải nghiệm") packageType = 1;
            if (package.PackageName == "Gói tiêu chuẩn") packageType = 2;
            if (package.PackageName == "Gói Premium") packageType = 3;

            DateTime now = DateTime.Now;
            return new SubscriptionInfoDto
            {
                PackageType = packageType, // Premium
                PackageName = package.PackageName,
                IsActive = package.IsActive,
                EndDate = activeSubscription.EndDate,
                DaysRemaining = (activeSubscription.EndDate - now).Days,
                UnlimitedAiHint = package.UnlimitedAiHint,
                AiHintLimitDaily = package.AiHintLimitDaily != 0 ? package.AiHintLimitDaily : 99,
                PersonalizedPath = package.PersonalizedPath,
                MistakeRetry = package.MistakeRetry,
                SmartReminder = package.SmartReminder,
                PrioritySupport = package.PrioritySupport,
            };
        }
    }
}