using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.ExerciseAttempt;
using ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard;

namespace ELearning_ToanHocHay_Control.Services.Interfaces
{
    public interface IExerciseAttemptService
    {
        // Bắt đầu làm bài theo đề có sẵn
        Task<ApiResponse<ExerciseAttemptDto>> StartExerciseAsync(StartExerciseDto dto);

        // Bắt đầu làm bài random
        Task<ApiResponse<ExerciseAttemptDto>> StartRandomExerciseAsync(StartRandomExerciseDto dto);

        // Lưu câu trả lời (Bản cũ)
        Task<ApiResponse<bool>> SubmitAnswerAsync(SaveAnswerDto dto);

        // Lưu câu trả lời (Bản mới)
        Task<ApiResponse<bool>> SaveAnswerAsync(SaveAnswerDto dto);

        // Hoàn thành bài tập và tính điểm
        Task<ApiResponse<ExerciseResultDto>> CompleteExerciseAsync(CompleteExerciseDto dto);

        // Xem lại bài làm
        Task<ApiResponse<ExerciseResultDto>> GetExerciseResultAsync(int attemptId);

        // Lấy lịch sử làm bài của học sinh
        Task<ApiResponse<List<ExerciseResultDto>>> GetStudentHistoryAsync(int studentId);

        // Nộp toàn bộ câu hỏi
        Task<ApiResponse<bool>> SubmitExamAsync(SubmitExamDto dto);
        Task<ApiResponse<StudentDashboardDto>> GetDashboardStatsAsync(int userId);

        // Báo cáo chuyển tab
        Task<ApiResponse<bool>> ReportTabSwitchAsync(int attemptId);

        // Lấy lịch sử chuyển tab
        Task<ApiResponse<List<DateTime>>> GetTabSwitchLogsAsync(int attemptId);
    }
}
