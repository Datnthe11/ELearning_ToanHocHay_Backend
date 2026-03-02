// FILE: ELearning_ToanHocHay_Control/Controllers/StudentSubscriptionController.cs
// Thêm file này vào thư mục Controllers của Backend
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Services.Implementations;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning_ToanHocHay_Control.Controllers
{
    [Route("api/student/{studentId:int}")]
    [ApiController]
    [Authorize]
    public class StudentSubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public StudentSubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        /// <summary>
        /// GET /api/student/{studentId}/subscription/current
        /// Trả về thông tin gói đang dùng (Free nếu không có active subscription)
        /// </summary>
        [HttpGet("subscription/current")]
        public async Task<IActionResult> GetCurrentSubscription(int studentId)
        {
            var info = await _subscriptionService.GetActiveSubscriptionInfoAsync(studentId);

            // Luôn trả 200 — Free là hợp lệ, không phải lỗi
            return Ok(new
            {
                success = true,
                data = info,
                message = info.IsActive
                    ? $"Đang dùng gói {info.PackageName}"
                    : "Đang dùng gói Free"
            });
        }
    }
}