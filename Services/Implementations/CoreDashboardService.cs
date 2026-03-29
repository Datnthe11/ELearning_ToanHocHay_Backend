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
        private readonly IStudentParentRepository _studentParentRepo;
        private readonly IParentRepository _parentRepo;
        private readonly IMapper _mapper;
        private readonly SubscriptionInfoHelper _subscriptionInfoHelper;
        private readonly ILogger<CoreDashboardService> _logger;

        public CoreDashboardService(
    IDashboardRepository dashboardRepo,
    IStudentRepository studentRepo,
    IPackageRepository packageRepo,
    IAIService aiService,
    IExerciseAttemptRepository attemptRepo,
    IStudentParentRepository studentParentRepo,
    IParentRepository parentRepo,
    IMapper mapper,
    SubscriptionInfoHelper subscriptionInfoHelper, // ← THÊM
    ILogger<CoreDashboardService> logger)
        {
            _dashboardRepo = dashboardRepo;
            _studentRepo = studentRepo;
            _packageRepo = packageRepo;
            _aiService = aiService;
            _attemptRepo = attemptRepo;
            _studentParentRepo = studentParentRepo;
            _parentRepo = parentRepo;
            _mapper = mapper;
            _subscriptionInfoHelper = subscriptionInfoHelper; // ← THÊM
            _logger = logger;
        }

        public async Task<CoreDashboardDto> GetCoreDashboardAsync(int studentId)
        {
            var studentInfoTask = await GetStudentInfoAsync(studentId);
            var statsTask = await GetOverviewStatsAsync(studentId);
            var recentLessonsTask = await GetRecentLessonsAsync(studentId, 5);
            var chapterProgressTask = await GetChapterProgressSummaryAsync(studentId);
            var packageTypeTask = await GetPackageTypeAsync(studentId);

            // ✅ THÊM DÒNG NÀY
            var subscription = await _packageRepo.GetActivePackageAsync(studentId);

            var dashboard = new CoreDashboardDto
            {
                StudentInfo = studentInfoTask,
                Stats = statsTask,
                RecentLessons = recentLessonsTask,
                ChapterProgress = chapterProgressTask,
                PackageType = packageTypeTask,
                // ✅ SỬA DÒNG NÀY — truyền subscription thật thay vì null
                SubscriptionInfo = await _subscriptionInfoHelper.BuildSubscriptionInfo(subscription),
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
                Score = l.Score,
                AttemptId = l.AttemptId,
                TabSwitchCount = l.TabSwitchCount
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

            var name = subscription.Package.PackageName.ToLower().Trim();
            return name switch
            {
                var n when n.Contains("premium") => PackageType.Premium,
                var n when n.Contains("tiêu chuẩn") || n.Contains("standard")
                        || n.Contains("tieu chuan") => PackageType.Standard,
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
            if (student == null) return false;

            // 1. Nếu là chính sinh viên đó
            if (student.UserId == userId) return true;

            // 2. Nếu là PHỤ HUYNH của sinh viên đó
            var parent = await _parentRepo.GetByUserIdAsync(userId);
            if (parent != null)
            {
                return await _studentParentRepo.ExistsAsync(studentId, parent.ParentId);
            }

            return false;
        }

        public async Task<List<ChapterScoreComparisonDto>> GetChapterScoreComparisonAsync(int studentId)
        {
            return await _dashboardRepo.GetChapterComparisonAsync(studentId);
        }

        public async Task<AIInsightResponse?> GetAIInsightAsync(int studentId)
        {
            _logger.LogInformation("Generating detailed strengths/weaknesses AI analysis for student {StudentId}", studentId);
            
            // 1. Lấy dữ liệu hiệu suất toàn diện
            var performance = await _dashboardRepo.GetFullPerformanceAsync(studentId);
            
            if (performance == null || !performance.Any())
            {
                return new AIInsightResponse { 
                    Summary = "Chào em! Hệ thống đang chờ em hoàn thành bài kiểm tra đầu tiên để bắt đầu phân tích năng lực. Cố lên nhé!",
                    ConceptsToReview = new List<string> { "Làm bài kiểm tra đầu tiên" },
                    Status = "success" 
                };
            }

            // Phân loại mạnh/yếu
            var strengths = performance.Where(p => p.IsStrength).OrderByDescending(p => p.AverageScore).Take(3).ToList();
            var weaknesses = performance.Where(p => p.IsWeakness).OrderBy(p => p.AverageScore).Take(3).ToList();

            // 2. Lấy ví dụ câu sai gần nhất
            var attempts = await _attemptRepo.GetStudentAttemptsAsync(studentId);
            var lastMistake = attempts.FirstOrDefault(a => a.WrongAnswers > 0);
            string specificMistakeInfo = "";
            if (lastMistake != null)
            {
                var details = await _attemptRepo.GetAttemptWithDetailsAsync(lastMistake.AttemptId);
                var wrong = details.StudentAnswers?.FirstOrDefault(sa => !sa.IsCorrect && sa.Question != null);
                if (wrong != null)
                {
                    specificMistakeInfo = $"Ví dụ câu sai gần đây: {wrong.Question.QuestionText} (Em chọn: {wrong.AnswerText}, Đáp án: {wrong.Question.CorrectAnswer})";
                }
            }

            // 3. Xây dựng prompt
            var strengthsText = strengths.Any() ? string.Join("\n", strengths.Select(s => $"- {s.TopicName} ({s.AverageScore}/10)")) : "Chưa xác định";
            var weaknessesText = weaknesses.Any() ? string.Join("\n", weaknesses.Select(w => $"- {w.TopicName} ({w.AverageScore}/10)")) : "Chưa xác định";

            var aiRequest = new AIInsightRequest
            {
                QuestionText = $"PHÂN TÍCH NĂNG LỰC TOÁN HỌC:\n\n🌟 ĐIỂM MẠNH:\n{strengthsText}\n\n⚠️ ĐIỂM YẾU:\n{weaknessesText}",
                StudentAnswer = specificMistakeInfo,
                CorrectAnswer = "Hãy phân tích cả điểm mạnh (để phát huy) và điểm yếu (để khắc phục) của học sinh.",
                Type = "assessment"
            };

            return await _aiService.GenerateInsightStructuredAsync(aiRequest);
        }

        public async Task<AIInsightResponse?> GetAIRoadmapAsync(int studentId)
        {
            _logger.LogInformation("Generating personalized roadmap AI analysis for student {StudentId}", studentId);
            
            var weakTopics = await _dashboardRepo.GetWeakTopicsAsync(studentId, 5);
            
            if (weakTopics == null || !weakTopics.Any())
            {
                return new AIInsightResponse { 
                    Summary = "Lộ trình hoàn hảo! Em đang đi đúng hướng, AI khuyên em nên bắt đầu các bài thi thử nâng cao.",
                    ConceptsToReview = new List<string> { "Luyện đề nâng cao" },
                    Status = "success" 
                };
            }

            var weakTopicsSummary = string.Join("\n", weakTopics.Select(t => 
                $"- {t.TopicName} ({t.ChapterName}). Bài học: {string.Join(", ", t.LessonNames)}"));

            var aiRequest = new AIInsightRequest
            {
                QuestionText = weakTopicsSummary,
                StudentAnswer = "Thiết lập lộ trình học tập",
                CorrectAnswer = "N/A",
                Type = "roadmap"
            };

            var result = await _aiService.GenerateInsightStructuredAsync(aiRequest);
            if (result != null && weakTopics.Any())
            {
                result.LessonId = weakTopics.First().FirstLessonId;
            }
            return result;
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