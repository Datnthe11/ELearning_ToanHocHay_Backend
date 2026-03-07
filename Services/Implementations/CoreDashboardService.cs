using AutoMapper;
using ELearning_ToanHocHay_Control.Models.DTOs.Chapter;
using ELearning_ToanHocHay_Control.Models.DTOs.Student;
using ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard;
using ELearning_ToanHocHay_Control.Models.DTOs.AI;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using SendGrid.Helpers.Errors.Model;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class CoreDashboardService : ICoreDashboardService
    {
        private readonly IDashboardRepository _dashboardRepo;
        private readonly IStudentRepository _studentRepo;
        private readonly IPackageRepository _packageRepo;
        private readonly IAIService _aiService;
        private readonly IExerciseAttemptRepository _attemptRepo;
        private readonly IMapper _mapper;
        private readonly ILogger<CoreDashboardService> _logger;

        public CoreDashboardService(
            IDashboardRepository dashboardRepo,
            IStudentRepository studentRepo,
            IPackageRepository packageRepo,
            IAIService aiService,
            IExerciseAttemptRepository attemptRepo,
            IMapper mapper,
            ILogger<CoreDashboardService> logger)
        {
            _dashboardRepo = dashboardRepo;
            _studentRepo = studentRepo;
            _packageRepo = packageRepo;
            _aiService = aiService;
            _attemptRepo = attemptRepo;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<CoreDashboardDto> GetCoreDashboardAsync(int studentId)
        {
            _logger.LogInformation("Building core dashboard for student {StudentId}", studentId);

            var studentInfoTask = await GetStudentInfoAsync(studentId);
            var statsTask = await GetOverviewStatsAsync(studentId);
            var recentLessonsTask = await GetRecentLessonsAsync(studentId, 5);
            var chapterProgressTask = await GetChapterProgressSummaryAsync(studentId);
            var packageTypeTask = await GetPackageTypeAsync(studentId);

            var dashboard = new CoreDashboardDto
            {
                StudentInfo = studentInfoTask,
                Stats = statsTask,
                RecentLessons = recentLessonsTask,
                ChapterProgress = chapterProgressTask,
                PackageType = packageTypeTask,
                Links = GenerateDashboardLinks(studentId, packageTypeTask)
            };

            return dashboard;
        }

        private async Task<StudentInfoDto> GetStudentInfoAsync(int studentId)
        {
            var student = await _studentRepo.GetStudentWithUserAsync(studentId);
            if (student == null)
                throw new NotFoundException($"Student {studentId} not found");

            return new StudentInfoDto
            {
                StudentId = student.StudentId,
                FullName = student.User.FullName,
                GradeLevel = student.GradeLevel,
                SchoolName = student.SchoolName,
            };
        }

        private async Task<OverviewStatsDto> GetOverviewStatsAsync(int studentId)
        {
            var weekStart = GetWeekStart(DateTime.UtcNow);
            var weekEnd = weekStart.AddDays(7);

            var thisWeekStats = await _dashboardRepo.GetWeeklyStatsAsync(studentId, weekStart, weekEnd);
            var lastWeekStats = await _dashboardRepo.GetWeeklyStatsAsync(studentId, weekStart.AddDays(-7), weekStart);
            var overallStats = await _dashboardRepo.GetOverallStatsAsync(studentId);
            var streakData = await _dashboardRepo.GetStreakDataAsync(studentId);

            var comparison = new ComparisonDto
            {
                ScoreChange = (int)(thisWeekStats.AverageScore - lastWeekStats.AverageScore),
                StudyTimeChange = thisWeekStats.TotalMinutes - lastWeekStats.TotalMinutes,
                ExerciseCountChange = thisWeekStats.ExerciseCount - lastWeekStats.ExerciseCount,
                Direction = DetermineDirection(thisWeekStats.AverageScore, lastWeekStats.AverageScore)
            };

            return new OverviewStatsDto
            {
                WeeklyStudyMinutes = thisWeekStats.TotalMinutes,
                WeeklyExercisesCompleted = thisWeekStats.ExerciseCount,
                AverageScore = overallStats.AverageScore,
                TotalExercisesCompleted = overallStats.TotalExercises,
                TotalLessonsCompleted = overallStats.TotalLessons,
                WeekComparison = comparison,
                CurrentStreak = streakData.CurrentStreak,
                LongestStreak = streakData.LongestStreak,
                StudiedToday = streakData.StudiedToday
            };
        }

        private async Task<List<RecentLessonDto>> GetRecentLessonsAsync(int studentId, int limit)
        {
            var recentLessons = await _dashboardRepo.GetRecentLessonsAsync(studentId, limit);
            return recentLessons.Select(l => new RecentLessonDto
            {
                LessonId = l.LessonId,
                LessonName = l.LessonName,
                TopicName = l.TopicName,
                ChapterName = l.ChapterName,
                CompletedAt = l.CompletedAt,
                DurationMinutes = l.DurationMinutes,
                IsCompleted = l.IsCompleted,
                ProgressPercentage = l.ProgressPercentage,
                Score = l.Score
            }).ToList();
        }

        private async Task<List<ChapterProgressSummaryDto>> GetChapterProgressSummaryAsync(int studentId)
        {
            var chapterProgress = await _dashboardRepo.GetChapterProgressAsync(studentId);
            return chapterProgress.Select(cp => new ChapterProgressSummaryDto
            {
                ChapterId = cp.ChapterId,
                ChapterName = cp.ChapterName,
                OrderIndex = cp.OrderIndex,
                CompletionPercentage = cp.CompletionPercentage,
                CompletedTopics = cp.CompletedTopics,
                TotalTopics = cp.TotalTopics,
                IsLocked = cp.IsLocked,
                CurrentMastery = cp.AverageMastery
            })
            .OrderBy(cp => cp.OrderIndex)
            .ToList();
        }

        public async Task<PackageType> GetPackageTypeAsync(int studentId)
        {
            var subscription = await _packageRepo.GetActivePackageAsync(studentId);
            if (subscription?.Package == null) return PackageType.Free;

            var name = subscription.Package.PackageName.ToLower();
            return name switch
            {
                var n when n.Contains("premium") => PackageType.Premium,
                var n when n.Contains("standard") => PackageType.Standard,
                _ => PackageType.Free
            };
        }

        private DashboardLinksDto GenerateDashboardLinks(int studentId, PackageType packageType)
        {
            var baseUrl = $"/api/student/{studentId}/dashboard";
            return new DashboardLinksDto
            {
                ExerciseHistory = $"{baseUrl}/exercise-history",
                Charts = packageType >= PackageType.Standard ? $"{baseUrl}/charts" : null,
                AIInsights = packageType >= PackageType.Premium ? $"{baseUrl}/ai-insights" : null,
                Notifications = packageType >= PackageType.Standard ? $"{baseUrl}/notifications" : null
            };
        }

        public async Task<bool> VerifyStudentAccessAsync(int studentId, int userId)
        {
            var student = await _studentRepo.GetByIdAsync(studentId);
            return student?.UserId == userId;
        }

        public async Task<List<ChapterScoreComparisonDto>> GetChapterScoreComparisonAsync(int studentId)
        {
            return await _dashboardRepo.GetChapterComparisonAsync(studentId);
        }

        public async Task<AIInsightResponse?> GetAIInsightAsync(int studentId)
        {
            _logger.LogInformation("Generating AI insight for dashboard student {StudentId}", studentId);
            
            // Lấy những bài làm gần nhất của học sinh có câu trả lời sai
            var attempts = await _attemptRepo.GetStudentAttemptsAsync(studentId);
            var lastAttemptWithMistakes = attempts
                .Where(a => a.Status != AttemptStatus.InProgress && a.WrongAnswers > 0)
                .OrderByDescending(a => a.SubmittedAt)
                .FirstOrDefault();

            if (lastAttemptWithMistakes == null)
            {
                return new AIInsightResponse { 
                    Summary = "Bạn đang làm bài rất tốt và không có lỗi sai nào gần đây. Hãy tiếp tục thử thách mình với các bài tập khó hơn nhé!",
                    ConceptsToReview = new List<string> { "Duy trì phong độ", "Mở rộng bài tập" },
                    Status = "success" 
                };
            }

            // Lấy chi tiết bài làm để tìm câu sai
            var details = await _attemptRepo.GetAttemptWithDetailsAsync(lastAttemptWithMistakes.AttemptId);
            var wrongAnswer = details.StudentAnswers?.FirstOrDefault(sa => !sa.IsCorrect && sa.Question != null);

            if (wrongAnswer == null || wrongAnswer.Question == null)
            {
                return new AIInsightResponse { Summary = "Chúng tôi chưa tìm thấy lỗ hổng kiến thức qua các bài làm gần đây.", Status = "success" };
            }

            var aiRequest = new AIInsightRequest
            {
                QuestionText = wrongAnswer.Question.QuestionText ?? "",
                StudentAnswer = wrongAnswer.AnswerText ?? "",
                CorrectAnswer = wrongAnswer.Question.CorrectAnswer ?? ""
            };

            return await _aiService.GenerateInsightStructuredAsync(aiRequest);
        }

        private DateTime GetWeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private TrendDirection DetermineDirection(decimal current, decimal previous)
        {
            var diff = current - previous;
            if (Math.Abs(diff) < 1) return TrendDirection.Same;
            return diff > 0 ? TrendDirection.Up : TrendDirection.Down;
        }
    }
}