using System.Security.Claims;
using ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard;
using ELearning_ToanHocHay_Control.Services.Implementations;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ELearning_ToanHocHay_Control.Controllers
{
    [Route("api/student/{studentId}/dashboard")]
    [ApiController]
    [Authorize] // Backend sẽ kiểm tra Token do WebApp gửi sang
    public class DashboardController : ControllerBase
    {
        private readonly ICoreDashboardService _coreDashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ICoreDashboardService coreDashboardService, ILogger<DashboardController> logger)
        {
            _coreDashboardService = coreDashboardService;
            _logger = logger;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<CoreDashboardDto>> GetCoreDashboard(int studentId)
        {
            try
            {
                // LẤY USERID TỪ TOKEN (Đảm bảo khớp với AccountController WebApp)
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int currentUserId))
                {
                    _logger.LogWarning("Token không chứa UserId hợp lệ.");
                    return Unauthorized(new { message = "Lỗi xác thực Token." });
                }

                // KIỂM TRA QUYỀN: User này có phải chủ sở hữu của StudentId này không?
                var hasAccess = await _coreDashboardService.VerifyStudentAccessAsync(studentId, currentUserId);
                if (!hasAccess)
                {
                    _logger.LogWarning("User {UserId} cố truy cập Student {StudentId}", currentUserId, studentId);
                    return StatusCode(403, new { message = "Bạn không có quyền xem dữ liệu này." });
                }

                // LẤY DỮ LIỆU THỰC
                var dashboard = await _coreDashboardService.GetCoreDashboardAsync(studentId);

                if (dashboard == null)
                {
                    return NotFound(new { message = "Không tìm thấy dữ liệu học sinh." });
                }

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi tại Dashboard API");
                return StatusCode(500, new { message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        [HttpGet("chapter-score-comparison")]
        public async Task<IActionResult> GetChapterScoreComparison(int studentId)
        {
            var package = await _coreDashboardService.GetPackageTypeAsync(studentId);

            if (package < PackageType.Standard)
                return StatusCode(403, new { message = "Gói của bạn không hỗ trợ tính năng này." });

            var result = await _coreDashboardService.GetChapterScoreComparisonAsync(studentId);

            return Ok(result);
        }

        [HttpGet("ai-insights")]
        public async Task<IActionResult> GetAIInsights(int studentId)
        {
            var package = await _coreDashboardService.GetPackageTypeAsync(studentId);

            if (package < PackageType.Premium)
                return StatusCode(403, new { message = "Tính năng này chỉ dành cho tài khoản Premium." });

            var result = await _coreDashboardService.GetAIInsightAsync(studentId);

            if (result == null)
                return NotFound(new { message = "Chưa có dữ liệu để phân tích." });

            return Ok(result);
        }
    }
}