using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.ExerciseAttempt;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ELearning_ToanHocHay_Control.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExerciseAttemptsController : ControllerBase
    {
        private readonly IExerciseAttemptService _attemptService;

        public ExerciseAttemptsController(IExerciseAttemptService attemptService)
        {
            _attemptService = attemptService;
        }
        /// <summary>
        /// Bắt đầu làm bài tập theo đề có sẵn
        /// </summary>
        [HttpPost("start")]
        public async Task<ActionResult<ApiResponse<ExerciseAttemptDto>>> StartExercise(
            [FromBody] StartExerciseDto dto)
        {
            var response = await _attemptService.StartExerciseAsync(dto);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Bắt đầu làm bài tập random từ question bank
        /// </summary>
        [HttpPost("start-random")]
        public async Task<ActionResult<ApiResponse<ExerciseAttemptDto>>> StartRandomExercise(
            [FromBody] StartRandomExerciseDto dto)
        {
            var response = await _attemptService.StartRandomExerciseAsync(dto);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Submit câu trả lời cho một câu hỏi
        /// Đây là bản cũ, sẽ dùng /save-answer để lưu câu trả lời của học sinh.
        /// </summary>
        [HttpPost("submit-answer")]
        public async Task<ActionResult<ApiResponse<bool>>> SubmitAnswer(
            [FromBody] SaveAnswerDto dto)
        {
            var response = await _attemptService.SubmitAnswerAsync(dto);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Auto save cho từng câu trả lời
        /// </summary>
        [HttpPost("save-answer")]
        public async Task<ActionResult<ApiResponse<bool>>> SaveAnswer([FromBody] SaveAnswerDto dto)
        {
            var response = await _attemptService.SaveAnswerAsync(dto);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }


        /// <summary>
        /// Submit toàn bộ bài thi trong 1 request
        /// </summary>
        [HttpPost("submit")]
        public async Task<ActionResult<ApiResponse<bool>>> SubmitExam(
            [FromBody] SubmitExamDto dto)
        {
            if (dto == null || dto.AttemptId <= 0 || dto.Answers == null || !dto.Answers.Any())
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("Dữ liệu nộp bài không hợp lệ"));
            }

            var response = await _attemptService.SubmitExamAsync(dto);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }


        /// <summary>
        /// Hoàn thành bài tập và tính điểm
        /// </summary>
        [HttpPost("complete")]
        public async Task<ActionResult<ApiResponse<ExerciseResultDto>>> CompleteExercise(
            [FromBody] CompleteExerciseDto dto)
        {
            var response = await _attemptService.CompleteExerciseAsync(dto);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Xem kết quả bài tập đã hoàn thành
        /// </summary>
        [HttpGet("{attemptId}/result")]
        public async Task<ActionResult<ApiResponse<ExerciseResultDto>>> GetExerciseResult(
            int attemptId)
        {
            var response = await _attemptService.GetExerciseResultAsync(attemptId);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        /// <summary>
        /// Lấy lịch sử làm bài của học sinh
        /// </summary>
        [HttpGet("student/{studentId}/history")]
        public async Task<ActionResult<ApiResponse<List<ExerciseResultDto>>>> GetStudentHistory(
            int studentId)
        {
            var response = await _attemptService.GetStudentHistoryAsync(studentId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }
        /// <summary>
        /// Báo cáo học sinh chuyển tab trong khi làm bài — gửi email cho phụ huynh
        /// </summary>
        [HttpPost("{attemptId}/report-tab-switch")]
        public async Task<ActionResult<ApiResponse<bool>>> ReportTabSwitch(int attemptId)
        {
            var response = await _attemptService.ReportTabSwitchAsync(attemptId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Lấy danh sách lịch sử thoát tab của 1 lượt làm bài
        /// </summary>
        [HttpGet("{attemptId}/tab-switch-logs")]
        public async Task<ActionResult<ApiResponse<List<DateTime>>>> GetTabSwitchLogs(int attemptId)
        {
            var response = await _attemptService.GetTabSwitchLogsAsync(attemptId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }
    }
}
