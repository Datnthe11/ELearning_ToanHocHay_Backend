using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models;
using ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Repositories.Implementations
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardRepository> _logger;

        private static readonly TimeZoneInfo VnTimeZone = GetVnTimeZone();

        private static TimeZoneInfo GetVnTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch { }
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { }
            return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
        }

        private static DateTime VnNow =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTimeZone);

        public DashboardRepository(AppDbContext context, ILogger<DashboardRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lấy thống kê theo tuần (Thời gian học, số bài tập, điểm TB)
        /// FIX: Đổi *100m thành *10m để thống nhất thang điểm 10
        /// </summary>
        public async Task<WeeklyStatsModel> GetWeeklyStatsAsync(
            int studentId, DateTime startDate, DateTime endDate)
        {
            var query = _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.SubmittedAt.HasValue &&
                           a.SubmittedAt.Value >= startDate &&
                           a.SubmittedAt.Value < endDate);

            var totalMinutes = await query
                .SumAsync(a => (int)(a.SubmittedAt!.Value - a.StartTime).TotalMinutes);

            var exerciseCount = await query.CountAsync();

            var totalScore = await query.SumAsync(a => a.TotalScore);
            var totalMax = await query.SumAsync(a => a.MaxScore);

            // ✅ FIX: *10m thay vì *100m → thang điểm 10
            var averageScore = totalMax > 0
                ? Math.Round((decimal)totalScore / (decimal)totalMax * 10m, 1)
                : 0m;

            return new WeeklyStatsModel
            {
                TotalMinutes = totalMinutes,
                ExerciseCount = exerciseCount,
                AverageScore = averageScore
            };
        }

        /// <summary>
        /// Lấy thống kê tổng thể từ trước đến nay
        /// FIX: Thêm null check cho Exercise
        /// </summary>
        public async Task<OverallStatsModel> GetOverallStatsAsync(int studentId)
        {
            var attempts = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.MaxScore > 0 &&
                           a.Exercise != null) // ✅ FIX: null check
                .ToListAsync();

            double averageScore = 0;
            int totalExercises = 0;

            if (attempts.Any())
            {
                totalExercises = attempts.Count;
                // Thang 10, làm tròn 1 chữ số
                averageScore = Math.Round(
                    attempts.Average(a => (double)a.TotalScore / (double)a.MaxScore * 10.0), 1);
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
        public async Task<List<RecentLessonModel>> GetRecentLessonsAsync(int studentId, int limit)
        {
            var raw = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                           a.Status != AttemptStatus.InProgress &&
                           a.Exercise != null)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Chapter)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Lessons)
                .OrderByDescending(a => a.SubmittedAt)
                .Take(limit)
                .ToListAsync();

            if (!raw.Any()) return new List<RecentLessonModel>();

            var attemptIds = raw.Select(a => a.AttemptId).ToList();
            var tabSwitchCounts = await _context.TabSwitchLogs
                .Where(l => attemptIds.Contains(l.AttemptId))
                .GroupBy(l => l.AttemptId)
                .Select(g => new { AttemptId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AttemptId, x => x.Count);

            return raw
                .Where(a => a.Exercise != null) // ← bỏ check Topic
                .Select(a => new RecentLessonModel
                {
                    LessonId = a.Exercise.Topic?.Lessons?.FirstOrDefault()?.LessonId ?? 0,
                    LessonName = a.Exercise.Topic?.Lessons?.FirstOrDefault()?.LessonName
                                      ?? a.Exercise.Topic?.TopicName
                                      ?? a.Exercise.ExerciseName, // ← fallback
                    TopicName = a.Exercise.Topic?.TopicName ?? a.Exercise.ExerciseName ?? "N/A",
                    ChapterName = a.Exercise.Topic?.Chapter?.ChapterName
                               ?? a.Exercise.Chapter?.ChapterName ?? "N/A",
                    CompletedAt = a.SubmittedAt,
                    DurationMinutes = a.SubmittedAt.HasValue
                        ? (int)(a.SubmittedAt.Value - a.StartTime).TotalMinutes
                        : 0,
                    IsCompleted = true,
                    ProgressPercentage = 100,
                    Score = a.MaxScore > 0
                        ? Math.Round((double)a.TotalScore / (double)a.MaxScore * 10.0, 2) // ← 2 chữ số
                        : (double?)null,
                    AttemptId = a.AttemptId,
                    TabSwitchCount = tabSwitchCounts.ContainsKey(a.AttemptId) ? tabSwitchCounts[a.AttemptId] : 0
                }).ToList();
        }

        /// <summary>
        /// Lấy danh sách chương và tiến độ học tập.
        /// </summary>
        public async Task<List<ChapterProgressModel>> GetChapterProgressAsync(int studentId)
        {
            int targetCurriculumId = 3;

            var chapters = await _context.Chapters
                .AsNoTracking()
                .Include(c => c.Topics)
                    .ThenInclude(t => t.Lessons)
                        .ThenInclude(l => l.LessonProgresses)
                .Where(c => c.IsActive && c.CurriculumId == targetCurriculumId)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync();

            var progressList = new List<ChapterProgressModel>();

            foreach (var chapter in chapters)
            {
                var totalTopics = chapter.Topics.Count;

                int completedTopics = 0;

                foreach (var topic in chapter.Topics)
                {
                    int totalLessons = topic.Lessons.Count;

                    if (totalLessons == 0) continue;

                    int completedLessons = topic.Lessons
                        .Count(l => l.LessonProgresses
                            .Any(lp => lp.StudentId == studentId && lp.IsCompleted));

                    if (completedLessons == totalLessons)
                    {
                        completedTopics++;
                    }
                }

                var completionPercentage = totalTopics > 0
                    ? Math.Round((decimal)completedTopics / totalTopics * 100, 1)
                    : 0;

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
                    AverageMastery = null
                });
            }

            return progressList;
        }


        /// Lấy điểm TB theo từng chương để vẽ biểu đồ
        /// FIX: Load về client trước rồi GroupBy để tránh crash navigation property null
        /// FIX: Thang điểm 10 thay vì không nhất quán
        /// </summary>
        public async Task<List<ChapterScoreComparisonDto>> GetChapterComparisonAsync(int studentId)
        {
            var attempts = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId &&
                            a.Status != AttemptStatus.InProgress &&
                            a.MaxScore > 0)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Chapter)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Chapter) // ← include Chapter trực tiếp
                .ToListAsync();

            // Lấy chapter từ Topic hoặc trực tiếp từ Exercise
            var valid = attempts.Where(a =>
                a.Exercise != null &&
                (a.Exercise.Topic?.Chapter != null || a.Exercise.Chapter != null))
                .ToList();

            if (!valid.Any()) return new List<ChapterScoreComparisonDto>();

            return valid
                .GroupBy(a => {
                    var chapter = a.Exercise.Topic?.Chapter ?? a.Exercise.Chapter;
                    return new { chapter!.ChapterId, chapter.ChapterName };
                })
                .Select(g => new ChapterScoreComparisonDto
                {
                    ChapterId = g.Key.ChapterId,
                    ChapterName = g.Key.ChapterName,
                    AverageScore = Math.Round(
                        g.Sum(x => (decimal)x.TotalScore) * 10m /
                        g.Sum(x => (decimal)x.MaxScore), 1)
                })
                .OrderBy(x => x.ChapterId)
                .ToList();
        }

        public async Task<List<WeakTopicDto>> GetWeakTopicsAsync(int studentId, int limit)
        {
            // Lấy 20 bài làm gần nhất có lỗi sai
            var recentMistakes = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.WrongAnswers > 0 && a.Exercise != null && a.Exercise.Topic != null)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Chapter)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Lessons)
                .OrderByDescending(a => a.SubmittedAt)
                .Take(20)
                .ToListAsync();
            
            if (!recentMistakes.Any()) return new List<WeakTopicDto>();
            
            // Nhóm theo topic và đếm số lỗi tổng cộng trong 20 bài gần đây
            return recentMistakes
                .GroupBy(a => a.Exercise.Topic)
                .Select(g => new WeakTopicDto
                {
                    TopicId = g.Key.TopicId,
                    TopicName = g.Key.TopicName,
                    ChapterName = g.Key.Chapter?.ChapterName ?? "N/A",
                    ErrorCount = g.Sum(a => a.WrongAnswers),
                    FirstLessonId = g.Key.Lessons?.OrderBy(l => l.OrderIndex).FirstOrDefault()?.LessonId,
                    LessonNames = g.Key.Lessons?
                                    .OrderBy(l => l.OrderIndex)
                                    .Select(l => l.LessonName)
                                    .Take(3)
                                    .ToList() ?? new List<string>()
                })
                .OrderByDescending(x => x.ErrorCount)
                .Take(limit)
                .ToList();
        }

        public async Task<List<TopicPerformanceDto>> GetFullPerformanceAsync(int studentId)
        {
            var attempts = await _context.ExerciseAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.Status != AttemptStatus.InProgress && a.Exercise != null && a.Exercise.Topic != null)
                .Include(a => a.Exercise)
                    .ThenInclude(e => e.Topic)
                        .ThenInclude(t => t.Chapter)
                .OrderByDescending(a => a.SubmittedAt)
                .Take(30)
                .ToListAsync();
            
            if (!attempts.Any()) return new List<TopicPerformanceDto>();
            
            return attempts
                .GroupBy(a => a.Exercise.Topic)
                .Select(g => new TopicPerformanceDto
                {
                    TopicName = g.Key.TopicName,
                    ChapterName = g.Key.Chapter?.ChapterName ?? "N/A",
                    TotalAttempts = g.Count(),
                    AverageScore = Math.Round(g.Average(a => (decimal)a.TotalScore / (decimal)a.MaxScore * 10m), 1)
                })
                .ToList();
        }

        // ==================== PRIVATE HELPERS ====================

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