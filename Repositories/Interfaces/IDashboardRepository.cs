using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models;
using ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard;

namespace ELearning_ToanHocHay_Control.Repositories.Interfaces
{
    public interface IDashboardRepository
    {
        Task<WeeklyStatsModel> GetWeeklyStatsAsync(int studentId, DateTime startDate, DateTime endDate);
        Task<OverallStatsModel> GetOverallStatsAsync(int studentId);
        Task<StreakDataModel> GetStreakDataAsync(int studentId);
        Task<List<RecentLessonModel>> GetRecentLessonsAsync(int studentId, int limit);
        Task<List<ChapterProgressModel>> GetChapterProgressAsync(int studentId);
        Task<List<ChapterScoreComparisonDto>> GetChapterComparisonAsync(int studentId);
        Task<List<WeakTopicDto>> GetWeakTopicsAsync(int studentId, int limit);
        Task<List<TopicPerformanceDto>> GetFullPerformanceAsync(int studentId);
    }
}
