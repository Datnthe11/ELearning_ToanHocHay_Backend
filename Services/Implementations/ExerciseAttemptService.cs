using AutoMapper;
using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.ExerciseAttempt;
using ELearning_ToanHocHay_Control.Models.DTOs.AIFeedback;
using ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq; // Bổ sung để hỗ trợ LINQ Queryable
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;


namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class ExerciseAttemptService : IExerciseAttemptService
    {
        private readonly IExerciseAttemptRepository _attemptRepository;
        private readonly IExerciseRepository _exerciseRepository;
        private readonly IStudentAnswerRepository _answerRepository;
        private readonly IUserRepository _userRepository;
        private readonly IQuestionBankRepository _questionBankRepository;
        private readonly IMapper _mapper;
        private readonly IExerciseQuestionRepository _exerciseQuestionRepository;
        private readonly IAIFeedbackRepository _feedbackRepository;
        private readonly IAIFeedbackService _aiFeedbackService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public ExerciseAttemptService(
            IExerciseAttemptRepository attemptRepository,
            IExerciseRepository exerciseRepository,
            IStudentAnswerRepository answerRepository,
            IUserRepository userRepository,
            IQuestionBankRepository questionBankRepository,
            IExerciseQuestionRepository exerciseQuestionRepository,
            IAIFeedbackRepository feedbackRepository,
            IAIFeedbackService aiFeedbackService,
            IServiceScopeFactory scopeFactory,
            IMapper mapper,
            AppDbContext context,
            IEmailService emailService)
        {
            _attemptRepository = attemptRepository;
            _exerciseRepository = exerciseRepository;
            _answerRepository = answerRepository;
            _userRepository = userRepository;
            _questionBankRepository = questionBankRepository;
            _exerciseQuestionRepository = exerciseQuestionRepository;
            _feedbackRepository = feedbackRepository;
            _aiFeedbackService = aiFeedbackService;
            _scopeFactory = scopeFactory;
            _mapper = mapper;
            _context = context;
            _emailService = emailService;
        }

        public async Task<ApiResponse<ExerciseResultDto>> CompleteExerciseAsync(CompleteExerciseDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy attempt + kiểm tra
                var attempt = await _attemptRepository.GetAttemptWithDetailsAsync(dto.AttemptId);

                if (attempt == null)
                {
                    return ApiResponse<ExerciseResultDto>.ErrorResponse(
                        "Attempt not found",
                        new List<string> { $"No attempt found with ID: {dto.AttemptId}" }
                    );
                }

                if (attempt.Status != AttemptStatus.InProgress)
                {
                    return ApiResponse<ExerciseResultDto>.ErrorResponse(
                        "Attempt already completed",
                        new List<string> { "This attempt has already been completed" }
                    );
                }

                var now = DateTime.UtcNow;
                var isTimeout = now >= attempt.PlannedEndTime;

                // 2. Lấy danh sách câu hỏi của bài
                var exerciseQuestions = await _exerciseQuestionRepository
                    .GetByExerciseIdAsync(attempt.ExerciseId);

                // ✅ FIX: Tính scorePerQuestion fallback khi eq.Score = 0
                var totalQuestions = exerciseQuestions.Count;
                var scorePerQuestion = totalQuestions > 0
                    ? attempt.MaxScore / totalQuestions
                    : 0;

                // 3. Lấy các câu trả lời đã làm
                var answers = await _answerRepository
                    .GetAttemptAnswersAsync(dto.AttemptId);

                var answerLookup = answers.ToDictionary(a => a.QuestionId);

                // 4. Biến chấm điểm
                double totalScore = 0;
                int correctAnswers = 0;
                int wrongAnswers = 0;

                var answerDetails = new List<AnswerDetailDto>();

                // 5. DUYỆT THEO CÂU HỎI
                foreach (var eq in exerciseQuestions)
                {
                    answerLookup.TryGetValue(eq.QuestionId, out var answer);
                    var question = eq.Question;

                    bool isCorrect = false;

                    if (answer != null)
                    {
                        switch (question.QuestionType)
                        {
                            case QuestionType.MultipleChoice:
                                if (answer.SelectedOptionId.HasValue)
                                {
                                    var correctOption = question.QuestionOptions?
                                        .FirstOrDefault(o => o.IsCorrect);

                                    Console.WriteLine($"=== CHẤM qId={question.QuestionId} | studentOpt={answer.SelectedOptionId} | correctOpt={correctOption?.OptionId} | match={correctOption?.OptionId == answer.SelectedOptionId} ===");

                                    isCorrect = correctOption != null &&
                                        correctOption.OptionId == answer.SelectedOptionId;
                                }
                                break;

                            case QuestionType.TrueFalse:
                            case QuestionType.FillBlank:
                                string studentAns = (answer.AnswerText ?? "").Trim().ToLower();
                                string correctAns = (question.CorrectAnswer ?? "").Trim().ToLower();
                                isCorrect = !string.IsNullOrEmpty(correctAns) && studentAns == correctAns;
                                break;
                        }
                    }

                    // ✅ FIX: Dùng eq.Score nếu > 0, ngược lại dùng scorePerQuestion
                    var maxScore = eq.Score > 0 ? eq.Score : scorePerQuestion;
                    var pointsEarned = isCorrect ? maxScore : 0;

                    if (isCorrect)
                    {
                        totalScore += pointsEarned;
                        correctAnswers++;
                    }
                    else if (answer != null)
                    {
                        wrongAnswers++;
                    }

                    if (answer != null)
                    {
                        answer.IsCorrect = isCorrect;
                        answer.PointsEarned = pointsEarned;
                        _answerRepository.Update(answer);
                    }

                    answerDetails.Add(new AnswerDetailDto
                    {
                        QuestionId = question.QuestionId,
                        QuestionText = question.QuestionText,
                        StudentAnswer = answer == null
                            ? null
                            : answer.AnswerText ??
                              question.QuestionOptions?
                                  .FirstOrDefault(o => o.OptionId == answer.SelectedOptionId)
                                  ?.OptionText,
                        CorrectAnswer = question.CorrectAnswer ??
                            question.QuestionOptions?
                                .FirstOrDefault(o => o.IsCorrect)
                                ?.OptionText,
                        IsCorrect = isCorrect,
                        PointsEarned = pointsEarned,
                        MaxScores = maxScore,
                        Explanation = question.Explanation
                    });
                }

                // 6. Cập nhật attempt
                attempt.TotalScore = totalScore;
                attempt.CorrectAnswers = correctAnswers;
                attempt.WrongAnswers = wrongAnswers;
                attempt.CompletionPercentage = attempt.MaxScore > 0
                    ? (decimal)(totalScore / attempt.MaxScore) * 100
                    : 0;
                attempt.Status = isTimeout ? AttemptStatus.Timeout : AttemptStatus.Submitted;
                attempt.SubmittedAt = now;

                _attemptRepository.Update(attempt);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ✅ Kích hoạt AI tạo lời giải cho các câu sai trong background
                // Chỉ lấy câu học sinh ĐÃ TRẢ LỜI NHƯNG SAI (loại bỏ câu bỏ trống)
                var wrongAnswerDetails = answerDetails
                    .Where(d => !d.IsCorrect && d.StudentAnswer != null)
                    .ToList();

                Console.WriteLine($"[AI Feedback] AttemptId={attempt.AttemptId} | Sai: {wrongAnswerDetails.Count} câu");

                if (wrongAnswerDetails.Any())
                {
                    try
                    {
                        var aiTasks = wrongAnswerDetails.Select(async w =>
                        {
                            Console.WriteLine($"[AI Feedback] Đang tạo feedback cho QuestionId={w.QuestionId}, AttemptId={attempt.AttemptId}");
                            try
                            {
                                // ✅ TẠO SCOPE RIÊNG CHO TỪNG TASK
                                using var scope = _scopeFactory.CreateScope();
                                var feedbackService = scope.ServiceProvider.GetRequiredService<IAIFeedbackService>();
                                
                                var result = await feedbackService.CreateAsync(new CreateAIFeedbackDto
                                {
                                    AttemptId = attempt.AttemptId,
                                    QuestionId = w.QuestionId,
                                    StudentAnswer = w.StudentAnswer
                                });
                                Console.WriteLine($"[AI Feedback] QuestionId={w.QuestionId}: {(result.Success ? "✅ OK" : "❌ Lỗi: " + result.Message)}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[AI Feedback Error] Attempt {attempt.AttemptId}, Question {w.QuestionId}: {ex.GetType().Name} - {ex.Message}");
                            }
                        });
                        
                        // Đợi TẤT CẢ các câu được AI chấm xong mới nộp bài được
                        await Task.WhenAll(aiTasks);
                    }
                    catch (Exception globalEx)
                    {
                        Console.WriteLine($"[AI Feedback Fatal Error]: {globalEx.GetType().Name} - {globalEx.Message}");
                    }
                }

                // 7. Trả kết quả
                var result = new ExerciseResultDto
                {
                    AttemptId = attempt.AttemptId,
                    StudentId = attempt.StudentId,
                    StudentName = attempt.Student?.User?.FullName,
                    ExerciseName = attempt.Exercise?.ExerciseName,
                    StartTime = attempt.StartTime,
                    Duration = attempt.PlannedEndTime - attempt.StartTime,
                    TotalScore = attempt.TotalScore,
                    MaxScore = attempt.MaxScore,
                    CompletionPercentage = attempt.CompletionPercentage,
                    CorrectAnswers = attempt.CorrectAnswers,
                    WrongAnswers = attempt.WrongAnswers,
                    TotalQuestions = exerciseQuestions.Count,
                    IsPassed = attempt.Exercise != null &&
                               attempt.TotalScore >= attempt.Exercise.PassingScore,
                    AnswerDetails = answerDetails
                };

                return ApiResponse<ExerciseResultDto>.SuccessResponse(
                    result,
                    isTimeout ? "Exercise auto-submitted due to timeout" : "Exercise submitted successfully"
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<ExerciseResultDto>.ErrorResponse(
                    "Error completing exercise",
                    new List<string> { ex.Message }
                );
            }
        }


        public async Task<ApiResponse<ExerciseResultDto>> GetExerciseResultAsync(int attemptId)
        {
            try
            {
                // 1. Lấy thông tin lượt làm bài
                var attempt = await _attemptRepository.GetAttemptWithDetailsAsync(attemptId);
                if (attempt == null)
                    return ApiResponse<ExerciseResultDto>.ErrorResponse("Không tìm thấy lượt làm bài");

                // Chưa hoàn thành thì không cho xem kết quả
                if (attempt.Status == AttemptStatus.InProgress)
                {
                    return ApiResponse<ExerciseResultDto>.ErrorResponse(
                        "Bài làm chưa được nộp, không thể xem kết quả"
                    );
                }

                // 2. Lấy danh sách câu hỏi của bài
                var exerciseQuestions = await _exerciseQuestionRepository.GetByExerciseIdAsync(attempt.ExerciseId);

                // 3. Lấy câu trả lời của học sinh
                var studentAnswers =
                    await _answerRepository.GetAttemptAnswersAsync(attemptId);

                var answerLookup = studentAnswers.ToDictionary(a => a.QuestionId);

                // Lấy Feedback của AI nếu có
                var aiFeedbacks = await _feedbackRepository.GetByAttemptAsync(attemptId);
                var feedbackLookup = aiFeedbacks.GroupBy(f => f.QuestionId).ToDictionary(g => g.Key, g => g.First());

                var answerDetails = new List<AnswerDetailDto>();

                // 4. DUYỆT THEO DANH SÁCH CÂU HỎI GỐC CỦA ĐỀ THI
                foreach (var eq in exerciseQuestions)
                {
                    var question = eq.Question;
                    answerLookup.TryGetValue(question.QuestionId, out var answer);

                    bool isAnswered = answer != null;
                    bool isCorrect = isAnswered && answer!.IsCorrect;

                    answerDetails.Add(new AnswerDetailDto
                    {
                        QuestionId = question.QuestionId,
                        QuestionText = question.QuestionText,

                        StudentAnswer = !isAnswered
                            ? "Bạn chưa trả lời câu hỏi này"
                            : (answer.AnswerText ?? question.QuestionOptions?.FirstOrDefault(o => o.OptionId == answer.SelectedOptionId)?.OptionText),

                        CorrectAnswer = question.CorrectAnswer ??
                                        question.QuestionOptions?.FirstOrDefault(o => o.IsCorrect)?.OptionText,

                        IsCorrect = isCorrect,
                        PointsEarned = isAnswered ? answer!.PointsEarned : 0,
                        MaxScores = eq.Score,
                        Explanation = question.Explanation,
                        
                        // Gán Feedback từ AI
                        FullSolution = feedbackLookup.TryGetValue(question.QuestionId, out var fb) ? fb.FullSolution : null,
                        MistakeAnalysis = fb?.MistakeAnalysis,
                        ImprovementAdvice = fb?.ImprovementAdvice
                    });
                }

                // 5. Map kết quả
                var result = new ExerciseResultDto
                {
                    AttemptId = attempt.AttemptId,
                    StudentId = attempt.StudentId,
                    StudentName = attempt.Student?.User?.FullName,

                    ExerciseId = attempt.ExerciseId,
                    ExerciseName = attempt.Exercise?.ExerciseName,

                    Status = attempt.Status,

                    StartTime = attempt.StartTime,
                    SubmittedAt = attempt.SubmittedAt ?? DateTime.UtcNow,
                    Duration = attempt.SubmittedAt.HasValue
                        ? attempt.SubmittedAt.Value - attempt.StartTime
                        : TimeSpan.Zero,

                    TotalScore = attempt.TotalScore,
                    MaxScore = attempt.MaxScore,
                    CorrectAnswers = attempt.CorrectAnswers,
                    WrongAnswers = attempt.WrongAnswers,
                    TotalQuestions = exerciseQuestions.Count,
                    CompletionPercentage = attempt.CompletionPercentage,
                    //IsPassed = 
                    AnswerDetails = answerDetails
                };

                return ApiResponse<ExerciseResultDto>.SuccessResponse(result, "Lấy kết quả thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<ExerciseResultDto>.ErrorResponse("Lỗi hệ thống khi tính điểm", new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<List<ExerciseResultDto>>> GetStudentHistoryAsync(int studentId)
        {
            try
            {
                var attempts = await _attemptRepository.GetStudentAttemptsAsync(studentId);

                var results = attempts
                    .Where(a => a.Status != AttemptStatus.InProgress)
                    .Select(a => new ExerciseResultDto
                    {
                        AttemptId = a.AttemptId,
                        StudentId = a.StudentId,
                        StudentName = a.Student?.User?.FullName,
                        ExerciseId = a.ExerciseId,
                        ExerciseName = a.Exercise?.ExerciseName,
                        StartTime = a.StartTime,
                        SubmittedAt = a.SubmittedAt.Value,
                        Duration = a.SubmittedAt.Value - a.StartTime,
                        TotalScore = a.TotalScore,
                        MaxScore = a.MaxScore,
                        CompletionPercentage = a.CompletionPercentage,
                        CorrectAnswers = a.CorrectAnswers,
                        WrongAnswers = a.WrongAnswers,
                        TotalQuestions = a.Exercise?.TotalQuestions ?? 0,
                        IsPassed = a.Exercise != null &&
                                   a.TotalScore >= a.Exercise.PassingScore
                    })
                    .ToList();

                return ApiResponse<List<ExerciseResultDto>>.SuccessResponse(
                    results,
                    "History retrieved successfully"
                );
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ExerciseResultDto>>.ErrorResponse(
                    "Error retrieving history",
                    new List<string> { ex.Message }
                );
            }
        }

        public async Task<ApiResponse<bool>> SaveAnswerAsync(SaveAnswerDto dto)
        {
            try
            {
                var attempt = await _attemptRepository.GetAttemptByIdAsync(dto.AttemptId);

                if (attempt == null)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "Attempt not found",
                        new List<string> { $"No attempt found with ID: {dto.AttemptId}" }
                    );
                }

                // Không cho lưu nếu đã submit
                if (attempt.Status != AttemptStatus.InProgress)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "Attempt is not active",
                        new List<string> { "Cannot save answer for completed attempt" }
                    );
                }

                // Hết giờ thì KHÔNG cho save nữa
                if (attempt.PlannedEndTime <= DateTime.UtcNow)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "Time is up",
                        new List<string> { "Exam time has expired" }
                    );
                }

                var existingAnswer = await _answerRepository.GetAnswerAsync(
                    dto.AttemptId,
                    dto.QuestionId
                );

                if (existingAnswer != null)
                {
                    // Update (autosave)
                    existingAnswer.AnswerText = dto.AnswerText;
                    existingAnswer.SelectedOptionId = dto.SelectedOptionId;
                    existingAnswer.AnsweredAt = DateTime.UtcNow;

                    await _answerRepository.UpdateAnswerAsync(existingAnswer);
                }
                else
                {
                    // Insert
                    var answer = new StudentAnswer
                    {
                        AttemptId = dto.AttemptId,
                        QuestionId = dto.QuestionId,
                        AnswerText = dto.AnswerText,
                        SelectedOptionId = dto.SelectedOptionId,
                        AnsweredAt = DateTime.UtcNow
                    };

                    await _answerRepository.CreateAnswerAsync(answer);
                }

                return ApiResponse<bool>.SuccessResponse(
                    true,
                    "Answer saved"
                );
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse(
                    "Error saving answer",
                    new List<string> { ex.Message, ex.InnerException?.Message ?? "no inner exception" }
                );
            }
        }

        public async Task<ApiResponse<ExerciseAttemptDto>> StartExerciseAsync(StartExerciseDto dto)
        {
            try
            {
                var user = await _userRepository.GetUserByStudentIdAsync(dto.StudentId);
                if (user == null) 
                    return ApiResponse<ExerciseAttemptDto>.ErrorResponse($"Không tìm thấy học sinh với Id: ${dto.StudentId}");

                var now = DateTime.UtcNow;

                /*var hasCompletedAttempt =
                    await _context.ExerciseAttempts.AnyAsync(a =>
                    a.StudentId == dto.StudentId
                    && a.ExerciseId == dto.ExerciseId
                    && a.Status != AttemptStatus.InProgress
                );*/

                // Check đã làm bài kiểm tra này hay chưa (chỉ cho làm 1 lần)
                /*if (hasCompletedAttempt)
                {
                    return ApiResponse<ExerciseAttemptDto>.ErrorResponse(
                        "Bạn đã hoàn thành bài thi này"
                    );
                }*/

                // 1. LOGIC RESUME: Tìm lượt cũ chưa nộp (Status == InProgress)
                // Nếu tìm thấy, trả về ngay lập tức để học sinh làm tiếp
                var existingAttempt = await _context.ExerciseAttempts
                    .AsNoTracking()
                    .Where(a => a.StudentId == dto.StudentId && a.ExerciseId == dto.ExerciseId && a.Status == AttemptStatus.InProgress)
                    .OrderByDescending(a => a.StartTime)
                    .FirstOrDefaultAsync();

                if (existingAttempt != null)
                {
                    if (now < existingAttempt.PlannedEndTime)
                    {
                        var exerciseInfo = await _exerciseRepository.GetExerciseWithQuestionsAsync(dto.ExerciseId);

                        var resumeDto =
                            MapToAttemptDto(existingAttempt, exerciseInfo);

                        return ApiResponse<ExerciseAttemptDto>
                            .SuccessResponse(resumeDto, "Tiếp tục lượt làm bài trước");
                    }
                    else
                    {
                        // Hết giờ → cập nhật timeout
                        existingAttempt.Status = AttemptStatus.Timeout;
                        existingAttempt.SubmittedAt = existingAttempt.PlannedEndTime;
                        await _context.SaveChangesAsync();
                    }
                }

                // 2. TẠO MỚI (Chỉ chạy khi không có bài cũ dang dở)
                var exercise = await _exerciseRepository.GetExerciseWithQuestionsAsync(dto.ExerciseId);
                if (exercise == null) return ApiResponse<ExerciseAttemptDto>.ErrorResponse("Không tìm thấy đề thi");

                if (!exercise.IsActive || exercise.Status != ExerciseStatus.Published)
                {
                    return ApiResponse<ExerciseAttemptDto>.ErrorResponse("Bài thi hiện không khả dụng.");
                }

                var startTime = DateTime.UtcNow;

                var attempt = new ExerciseAttempt
                {
                    StudentId = dto.StudentId,
                    ExerciseId = dto.ExerciseId,
                    StartTime = startTime,
                    PlannedEndTime = startTime.AddMinutes((double)exercise.DurationMinutes),
                    MaxScore = exercise.TotalScores,
                    Status = AttemptStatus.InProgress
                };

                var createdAttempt = await _attemptRepository.CreateAttemptAsync(attempt);
                var attemptDto = MapToAttemptDto(createdAttempt, exercise);

                return ApiResponse<ExerciseAttemptDto>.SuccessResponse(attemptDto, "Bắt đầu bài thi mới");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL ERROR] StartExercise: {ex.Message}");
                return ApiResponse<ExerciseAttemptDto>.ErrorResponse("Lỗi khởi tạo: " + ex.Message);
            }
        }

        public async Task<ApiResponse<ExerciseAttemptDto>> StartRandomExerciseAsync(StartRandomExerciseDto dto)
        {
            try
            {
                // Lấy câu hỏi random từ question bank
                var questions = await _exerciseRepository.GetRandomQuestionsAsync(
                    dto.BankId,
                    dto.NumberOfQuestions
                );

                if (!questions.Any())
                {
                    return ApiResponse<ExerciseAttemptDto>.ErrorResponse(
                        "No questions available",
                        new List<string> { "Cannot find enough questions for this exercise" }
                    );
                }

                // Get UserId from StudentId to save to CreatedBy attribute
                var user = await _userRepository.GetUserByStudentIdAsync(dto.StudentId);

                if (user == null)
                {
                    return ApiResponse<ExerciseAttemptDto>.ErrorResponse(
                        "User not found",
                        new List<string> { "Invalid StudentId" }
                    );
                }

                // Get QuestionBank from bankId
                var questionBank = await _questionBankRepository.GetQuestionBankByIdAsync(dto.BankId);

                if (questionBank == null)
                {
                    return ApiResponse<ExerciseAttemptDto>.ErrorResponse(
                        "Question bank not found",
                        new List<string> { "Invalid BankId" }
                    );
                }

                // Tạo exercise tạm thời (hoặc lưu vào DB nếu cần)
                var exercise = new Exercise
                {
                    ExerciseName = $"Random {dto.ExerciseType} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                    ChapterId = questionBank.ChapterId,
                    TopicId = questionBank.TopicId,
                    ExerciseType = dto.ExerciseType,
                    TotalQuestions = questions.Count,
                    DurationMinutes = dto.DurationMinutes,
                    TotalScores = dto.MaxScore,
                    Status = ExerciseStatus.Published,
                    IsActive = true,
                    CreatedBy = user.UserId,
                    CreatedAt = DateTime.UtcNow
                };

                await _exerciseRepository.CreateExerciseAsync(exercise);

                // Tạo ExerciseQuestion (NƠI LƯU SCORE)
                var exerciseQuestions = questions.Select((q, index) => new ExerciseQuestion
                {
                    ExerciseId = exercise.ExerciseId,
                    QuestionId = q.QuestionId,
                    Score = dto.MaxScore / questions.Count,   // chia đều điểm
                    OrderIndex = index + 1
                }).ToList();

                await _exerciseQuestionRepository.AddRangeAsync(exerciseQuestions);

                // Tạo attempt
                var attempt = new ExerciseAttempt
                {
                    StudentId = dto.StudentId,
                    ExerciseId = exercise.ExerciseId,
                    StartTime = DateTime.UtcNow,
                    MaxScore = dto.MaxScore,
                };

                var createdAttempt = await _attemptRepository.CreateAttemptAsync(attempt);

                // Tạo lookup để lấy Score nhanh
                var scoreLookup = exerciseQuestions.ToDictionary(
                    eq => eq.QuestionId,
                    eq => eq.Score
                );

                // Map sang DTO với questions
                var attemptDto = new ExerciseAttemptDto
                {
                    AttemptId = createdAttempt.AttemptId,
                    StudentId = createdAttempt.StudentId,
                    ExerciseId = createdAttempt.ExerciseId,
                    ExerciseName = exercise.ExerciseName,
                    ExerciseType = exercise.ExerciseType,
                    StartTime = createdAttempt.StartTime,
                    TotalQuestions = questions.Count,
                    Questions = questions.Select(q => new QuestionInAttemptDto
                    {
                        QuestionId = q.QuestionId,
                        QuestionText = q.QuestionText,
                        QuestionType = q.QuestionType,
                        Score = scoreLookup[q.QuestionId],
                        ImageUrl = q.QuestionImageUrl,
                        Options = q.QuestionOptions?.Select(o => new AnswerOptionDto
                        {
                            OptionId = o.OptionId,
                            OptionText = o.OptionText,
                            ImageUrl = o.ImageUrl
                        }).ToList() ?? new List<AnswerOptionDto>()
                    }).ToList()
                };

                return ApiResponse<ExerciseAttemptDto>.SuccessResponse(
                    attemptDto,
                    "Random exercise created successfully"
                );
            }
            catch (Exception ex)
            {
                return ApiResponse<ExerciseAttemptDto>.ErrorResponse(
                    "Error creating random exercise",
                    new List<string> { ex.Message }
                );
            }
        }

        public async Task<ApiResponse<bool>> SubmitAnswerAsync(SaveAnswerDto dto)
        {
            try
            {
                // Kiểm tra attempt có tồn tại không
                var attempt = await _attemptRepository.GetAttemptByIdAsync(dto.AttemptId);

                if (attempt == null)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "Attempt not found",
                        new List<string> { $"No attempt found with ID: {dto.AttemptId}" }
                    );
                }

                if (attempt.Status != AttemptStatus.InProgress)
                {
                    return ApiResponse<bool>.ErrorResponse(
                        "Attempt already completed",
                        new List<string> { "Cannot submit answer for completed attempt" }
                    );
                }

                // Kiểm tra xem đã trả lời câu này chưa
                var existingAnswer = await _answerRepository.GetAnswerAsync(
                    dto.AttemptId,
                    dto.QuestionId
                );

                if (existingAnswer != null)
                {
                    // Cập nhật câu trả lời
                    existingAnswer.AnswerText = dto.AnswerText;
                    existingAnswer.SelectedOptionId = dto.SelectedOptionId;
                    existingAnswer.AnsweredAt = DateTime.UtcNow;

                    await _answerRepository.UpdateAnswerAsync(existingAnswer);
                }
                else
                {
                    // Tạo câu trả lời mới
                    var answer = new StudentAnswer
                    {
                        AttemptId = dto.AttemptId,
                        QuestionId = dto.QuestionId,
                        AnswerText = dto.AnswerText,
                        SelectedOptionId = dto.SelectedOptionId,
                        AnsweredAt = DateTime.UtcNow
                    };

                    await _answerRepository.CreateAnswerAsync(answer);
                }

                return ApiResponse<bool>.SuccessResponse(
                    true,
                    "Answer submitted successfully"
                );
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse(
                    "Error submitting answer",
                    new List<string> { ex.Message }
                );
            }
        }

        public async Task<ApiResponse<bool>> SubmitExamAsync(SubmitExamDto dto)
        {
            try
            {
                var answers = dto.Answers.Select(a => new StudentAnswer
                {
                    AttemptId = dto.AttemptId,
                    QuestionId = a.QuestionId,
                    SelectedOptionId = a.SelectedOptionId
                }).ToList();

                await _attemptRepository.SubmitExamAsync(dto.AttemptId, answers);

                return ApiResponse<bool>.SuccessResponse(true, "Nộp bài thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse("Lỗi khi nộp bài", new List<string> { ex.Message });
            }
        }

        private ExerciseAttemptDto MapToAttemptDto(ExerciseAttempt attempt, Exercise exercise)
        {
            var questionsDto = new List<QuestionInAttemptDto>();

            if (exercise.ExerciseQuestions != null)
            {
                foreach (var eq in exercise.ExerciseQuestions)
                {
                    if (eq.Question != null)
                    {
                        questionsDto.Add(new QuestionInAttemptDto
                        {
                            QuestionId = eq.Question.QuestionId,
                            QuestionText = eq.Question.QuestionText,
                            QuestionType = eq.Question.QuestionType,
                            Score = eq.Score,
                            ImageUrl = eq.Question.QuestionImageUrl,
                            Options = eq.Question.QuestionOptions?.Select(o => new AnswerOptionDto
                            {
                                OptionId = o.OptionId,
                                OptionText = o.OptionText,
                                ImageUrl = o.ImageUrl,
                                // Không gửi IsCorrect về máy khách để tránh gian lận
                            }).ToList() ?? new List<AnswerOptionDto>()
                        });
                    }
                }
            }

            return new ExerciseAttemptDto
            {
                AttemptId = attempt.AttemptId,
                StudentId = attempt.StudentId,
                ExerciseId = attempt.ExerciseId,
                ExerciseName = exercise.ExerciseName ?? "Không tên",
                ExerciseType = exercise.ExerciseType,
                StartTime = attempt.StartTime,
                PlannedEndTime = attempt.PlannedEndTime,
                SubmittedAt = attempt.SubmittedAt,
                Status = attempt.Status,
                TotalQuestions = questionsDto.Count,
                Questions = questionsDto
            };
        }
        public async Task<ApiResponse<StudentDashboardDto>> GetDashboardStatsAsync(int userId)
        {
            try
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                if (student == null) return ApiResponse<StudentDashboardDto>.ErrorResponse("Không tìm thấy học sinh.");

                // 1. Lấy tất cả chương và bài tập trong chương đó
                var allChapters = await _context.Chapters
                    .Include(c => c.Exercises)
                    .ToListAsync();

                // 2. Lấy lịch sử làm bài của học sinh
                var attempts = await _context.ExerciseAttempts
                    .Include(a => a.Exercise)
                    .Where(a => a.StudentId == student.StudentId && a.Status != AttemptStatus.InProgress)
                    .ToListAsync();

                var stats = new StudentDashboardDto();

                // --- Giữ nguyên logic cũ ---
                stats.TotalAttempts = attempts.Count;
                stats.AverageScore = attempts.Any() ? Math.Round(attempts.Average(a => a.TotalScore), 1) : 0;

                // --- LOGIC MỚI: Đổ dữ liệu vào danh sách Chapters ---
                foreach (var ch in allChapters)
                {
                    int totalExercisesInChapter = ch.Exercises.Count;

                    // Đếm số bài tập khác nhau trong chương này mà học sinh đã làm
                    int completedInChapter = attempts
                        .Where(a => a.Exercise.ChapterId == ch.ChapterId)
                        .Select(a => a.ExerciseId)
                        .Distinct()
                        .Count();

                    // Tính % tiến độ
                    int progress = 0;
                    if (totalExercisesInChapter > 0)
                    {
                        progress = (int)((double)completedInChapter / totalExercisesInChapter * 100);
                    }

                    stats.Chapters.Add(new ChapterProgressDto
                    {
                        ChapterId = ch.ChapterId,
                        ChapterName = ch.ChapterName,
                        TotalLessons = totalExercisesInChapter,
                        ProgressPercentage = progress > 100 ? 100 : progress // Đảm bảo không quá 100%
                    });
                }

                // Đếm số chương đã hoàn thành (tiến độ = 100%)
                stats.CompletedChapters = stats.Chapters.Count(x => x.ProgressPercentage == 100);

                // Biểu đồ và Lịch sử (Giữ nguyên như cũ)
                stats.ChartData = attempts.GroupBy(a => a.Exercise.ChapterId)
                    .Select(g => new ScoreChartItemDto
                    {
                        ChapterName = stats.Chapters.FirstOrDefault(c => c.ChapterId == g.Key)?.ChapterName ?? "Chương " + g.Key,
                        AvgScore = Math.Round(g.Average(a => a.TotalScore), 1)
                    }).ToList();

                stats.RecentAttempts = attempts.OrderByDescending(a => a.StartTime).Take(5).Select(a => new ExerciseAttemptDto
                {
                    AttemptId = a.AttemptId,
                    ExerciseName = a.Exercise?.ExerciseName ?? "Bài tập",
                    //Score = a.TotalScore,
                    StartTime = a.StartTime
                }).ToList();

                return ApiResponse<StudentDashboardDto>.SuccessResponse(stats, "Thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<StudentDashboardDto>.ErrorResponse(ex.Message);
            }
        }
        public async Task<ApiResponse<bool>> ReportTabSwitchAsync(int attemptId)
        {
            try
            {
                // Load attempt with Student → User and StudentParents → Parent → User
                var attempt = await _context.ExerciseAttempts
                    .Include(a => a.Exercise)
                    .Include(a => a.Student)
                        .ThenInclude(s => s.User)
                    .Include(a => a.Student)
                        .ThenInclude(s => s.StudentParents)
                            .ThenInclude(sp => sp.Parent)
                                .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

                if (attempt == null)
                    return ApiResponse<bool>.ErrorResponse("Không tìm thấy lượt làm bài.");

                if (attempt.Status != AttemptStatus.InProgress)
                    return ApiResponse<bool>.ErrorResponse("Bài thi đã kết thúc.");

                var studentName = attempt.Student?.User?.FullName ?? "Học sinh";
                var exerciseName = attempt.Exercise?.ExerciseName ?? "Bài kiểm tra";
                var switchedAt = DateTime.UtcNow;

                // Lưu lịch sử vi phạm vào DB
                var log = new TabSwitchLog { AttemptId = attemptId, SwitchedAt = switchedAt };
                _context.TabSwitchLogs.Add(log);
                await _context.SaveChangesAsync();

                // Lấy số lần vi phạm
                var switchCount = await _context.TabSwitchLogs.CountAsync(l => l.AttemptId == attemptId);

                var parents = attempt.Student?.StudentParents
                    ?.Select(sp => sp.Parent)
                    .Where(p => p?.User?.Email != null)
                    .ToList() ?? new List<Parent>();

                if (!parents.Any())
                {
                    // Không có phụ huynh liên kết — vẫn báo success
                    return ApiResponse<bool>.SuccessResponse(true, "Không có phụ huynh liên kết.");
                }

                // Gửi email song song cho tất cả phụ huynh
                var emailTasks = parents.Select(p =>
                    _emailService.SendTabSwitchNotificationAsync(
                        toEmail: p.User!.Email,
                        parentName: p.User.FullName,
                        studentName: studentName,
                        exerciseName: exerciseName,
                        switchedAt: switchedAt,
                        switchCount: switchCount
                    )
                );

                await Task.WhenAll(emailTasks);

                return ApiResponse<bool>.SuccessResponse(true, "Đã gửi thông báo cho phụ huynh.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportTabSwitch Error] {ex.Message}");
                return ApiResponse<bool>.ErrorResponse("Lỗi khi gửi thông báo: " + ex.Message);
            }
        }

        public async Task<ApiResponse<List<DateTime>>> GetTabSwitchLogsAsync(int attemptId)
        {
            try
            {
                var logs = await _context.TabSwitchLogs
                    .Where(l => l.AttemptId == attemptId)
                    .OrderBy(l => l.SwitchedAt)
                    .Select(l => l.SwitchedAt)
                    .ToListAsync();

                return ApiResponse<List<DateTime>>.SuccessResponse(logs, "Thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DateTime>>.ErrorResponse("Lỗi truy xuất lịch sử: " + ex.Message);
            }
        }
    }
}
