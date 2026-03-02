using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Repositories.Implementations
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardRepository> _logger;

        // Cấu hình múi giờ Việt Nam để tính toán chuỗi ngày học (Streak) chính xác
        private static readonly TimeZoneInfo VnTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        private static DateTime VnNow =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTimeZone);

        public DashboardRepository(AppDbContext context, ILogger<DashboardRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy thống kê theo tuần (Thời gian học, số bài tập, điểm TB)
        /// </summary>
        public async Task<WeeklyStatsModel> GetWeeklyStatsAsync(
            int studentId, DateTime startDate, DateTime endDate)
        {
            var attempts = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.SubmittedAt.HasValue &&
                           a.SubmittedAt.Value >= startDate &&
                           a.SubmittedAt.Value < endDate)
                .ToListAsync();

            if (!attempts.Any())
                return new WeeklyStatsModel { TotalMinutes = 0, ExerciseCount = 0, AverageScore = 0 };

            var totalMinutes = attempts.Sum(a =>
                (int)(a.SubmittedAt!.Value - a.StartTime).TotalMinutes);

            var averageScore = attempts.Average(a =>
                a.MaxScore > 0 ? ((decimal)a.TotalScore / (decimal)a.MaxScore) * 100m : 0m);

            return new WeeklyStatsModel
            {
                TotalMinutes = totalMinutes,
                ExerciseCount = attempts.Count,
                AverageScore = Math.Round(averageScore, 1)
            };
        }

        /// <summary>
        /// Lấy thống kê tổng thể từ trước đến nay
        /// </summary>
        public async Task<OverallStatsModel> GetOverallStatsAsync(int studentId)
        {
            var attempts = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.MaxScore > 0)
                .ToListAsync();

            double averageScore = 0;
            int totalExercises = 0;

            if (attempts.Any())
            {
                totalExercises = attempts.Count;
                averageScore = attempts.Average(a =>
                    ((double)a.TotalScore / (double)a.MaxScore) * 100.0);
                averageScore = Math.Round(averageScore, 1);
            }

            var completedLessons = await _context.StudentProgresses
                .AsNoTracking()
                .Where(sp => sp.StudentId == studentId &&
                            sp.MasteryLevel >= MasteryLevel.Intermediate)
                .Select(sp => sp.TopicId)
                .Distinct()
                .CountAsync();

            return new OverallStatsModel
            {
                AverageScore = (decimal)averageScore,
                TotalExercises = totalExercises,
                TotalLessons = completedLessons
            };
        }

        /// <summary>
        /// Tính toán chuỗi ngày học liên tục (Streak)
        /// </summary>
        public async Task<StreakDataModel> GetStreakDataAsync(int studentId)
        {
            var attempts = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.SubmittedAt.HasValue)
                .Select(a => a.SubmittedAt!.Value)
                .ToListAsync();

            if (!attempts.Any())
                return new StreakDataModel { CurrentStreak = 0, LongestStreak = 0, StudiedToday = false };

            var studyDates = attempts
                .Select(dt => TimeZoneInfo.ConvertTimeFromUtc(dt, VnTimeZone).Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            var today = VnNow.Date;
            var studiedToday = studyDates.Contains(today);
            var currentStreak = CalculateCurrentStreak(studyDates, today);
            var longestStreak = CalculateLongestStreak(studyDates);

            return new StreakDataModel
            {
                CurrentStreak = currentStreak,
                LongestStreak = longestStreak,
                StudiedToday = studiedToday
            };
        }

        /// <summary>
        /// Lấy danh sách các bài học vừa hoàn thành gần đây
        /// </summary>
        // Chỉ thay thế method GetRecentLessonsAsync trong DashboardRepository.cs

        public async Task<List<RecentLessonModel>> GetRecentLessonsAsync(int studentId, int limit)
        {
            var raw = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.Exercise != null)          // guard: bỏ attempt mồ côi
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Chapter)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Lessons)
                .OrderByDescending(a => a.SubmittedAt)
                .Take(limit)
                .ToListAsync();

            return raw
                .Where(a => a.Exercise?.Topic != null)  // lọc thêm sau khi load
                .Select(a => new RecentLessonModel
                {
                    LessonId = a.Exercise.Topic.Lessons?.FirstOrDefault()?.LessonId ?? 0,
                    LessonName = a.Exercise.Topic.Lessons?.FirstOrDefault()?.LessonName ?? a.Exercise.Topic.TopicName,
                    TopicName = a.Exercise.Topic.TopicName ?? "N/A",
                    ChapterName = a.Exercise.Topic.Chapter?.ChapterName ?? "N/A",  // null-safe
                    CompletedAt = a.SubmittedAt,
                    DurationMinutes = a.SubmittedAt.HasValue
                        ? (int)(a.SubmittedAt.Value - a.StartTime).TotalMinutes
                        : 0,
                    IsCompleted = true,
                    ProgressPercentage = 100,
                    Score = a.MaxScore > 0
                        ? Math.Round((double)a.TotalScore / (double)a.MaxScore * 100.0, 1)
                        : (double?)null
                }).ToList();
        }

        /// <summary>
        /// Lấy danh sách chương và tiến độ học tập.
        /// CHÚ Ý: Đã fix cứng CurriculumId = 3 cho chương trình Kết Nối Tri Thức.
        /// </summary>
        public async Task<List<ChapterProgressModel>> GetChapterProgressAsync(int studentId)
        {
            // TODO: Tạm thời fix cứng giá trị ID = 3 (Bộ Kết Nối Tri Thức).
            // Sau này sẽ lấy động từ bảng Student thông qua tham số studentId.
            int targetCurriculumId = 3;

            var chapters = await _context.Chapters
                .AsNoTracking()
                .Include(c => c.Topics)
                    .ThenInclude(t => t.studentProgresses)
                // Lọc theo các chương đang hoạt động VÀ thuộc bộ sách ID 3
                .Where(c => c.IsActive && c.CurriculumId == targetCurriculumId)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            var progressList = new List<ChapterProgressModel>();

            foreach (var chapter in chapters)
            {
                var totalTopics = chapter.Topics.Count;

                var completedTopics = chapter.Topics
                    .Count(t => t.studentProgresses
                        .Any(sp => sp.StudentId == studentId &&
                                  sp.MasteryLevel >= MasteryLevel.Intermediate));

                var completionPercentage = totalTopics > 0
                    ? Math.Round((decimal)completedTopics / totalTopics * 100, 1)
                    : 0;

                var topicProgresses = chapter.Topics
                    .SelectMany(t => t.studentProgresses)
                    .Where(sp => sp.StudentId == studentId)
                    .ToList();

                MasteryLevel? avgMastery = null;
                if (topicProgresses.Any())
                {
                    var avgMasteryValue = (int)topicProgresses.Average(sp => (int)sp.MasteryLevel);
                    avgMastery = (MasteryLevel)avgMasteryValue;
                }

                var isLocked = !chapter.Topics.Any(t => t.IsFree);

                progressList.Add(new ChapterProgressModel
                {
                    ChapterId = chapter.ChapterId,
                    ChapterName = chapter.ChapterName,
                    OrderIndex = chapter.OrderIndex,
                    CompletionPercentage = completionPercentage,
                    CompletedTopics = completedTopics,
                    TotalTopics = totalTopics,
                    IsLocked = isLocked,
                    AverageMastery = avgMastery
                });
            }

            return progressList;
        }

        private int CalculateCurrentStreak(List<DateTime> studyDates, DateTime today)
        {
            if (!studyDates.Any()) return 0;

            int streak = 0;
            var checkDate = today;

            if (!studyDates.Contains(today))
            {
                checkDate = today.AddDays(-1);
                if (!studyDates.Contains(checkDate)) return 0;
            }

            while (studyDates.Contains(checkDate))
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }

            return streak;
        }

        private int CalculateLongestStreak(List<DateTime> studyDates)
        {
            if (!studyDates.Any()) return 0;

            studyDates = studyDates.OrderBy(d => d).ToList();

            int longestStreak = 1;
            int currentStreak = 1;

            for (int i = 1; i < studyDates.Count; i++)
            {
                var daysDiff = (studyDates[i] - studyDates[i - 1]).Days;
                if (daysDiff == 1)
                {
                    currentStreak++;
                    longestStreak = Math.Max(longestStreak, currentStreak);
                }
                else
                {
                    currentStreak = 1;
                }
            }

            return longestStreak;
        }
    }
}