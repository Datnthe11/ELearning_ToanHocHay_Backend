using ELearning_ToanHocHay_Control.Data.Entities;

namespace ELearning_ToanHocHay_Control.Models.DTOs.ExerciseAttempt
{
    // Request DTOs
    public class StartExerciseDto
    {
        public int ExerciseId { get; set; }
        public int StudentId { get; set; }
    }

    public class StartRandomExerciseDto
    {
        public int StudentId { get; set; }
        public int BankId { get; set; }
        public ExerciseType ExerciseType { get; set; }
        public int NumberOfQuestions { get; set; } = 10;
        public double MaxScore { get; set; } = 10;
        public int? DurationMinutes { get; set; }
    }

    public class SaveAnswerDto
    {
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public string? AnswerText { get; set; }
        public int? SelectedOptionId { get; set; }
    }
    public class SubmitExamDto
    {
        public int AttemptId { get; set; }
        public List<SaveAnswerDto> Answers { get; set; } = new();
    }

    public class CompleteExerciseDto
    {
        public int AttemptId { get; set; }
    }

    // Response DTOs
    public class ExerciseAttemptDto
    {
        public int AttemptId { get; set; }
        public int StudentId { get; set; }
        public int? ExerciseId { get; set; }
        public string ExerciseName { get; set; }
        public ExerciseType ExerciseType { get; set; }
        public DateTime StartTime { get; set; }
        // Thời điểm PHẢI kết thúc (đếm giờ)
        public DateTime PlannedEndTime { get; set; }

        // Thời điểm thực sự nộp bài (null nếu chưa submit)
        public DateTime? SubmittedAt { get; set; }

        public AttemptStatus Status { get; set; }
        public int TotalQuestions { get; set; }
        public List<QuestionInAttemptDto> Questions { get; set; } = new();
        // Helper cho frontend
        public bool IsCompleted =>
            Status == AttemptStatus.Submitted ||
            Status == AttemptStatus.Timeout;
    }

    public class QuestionInAttemptDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public QuestionType QuestionType { get; set; }
        public double Score { get; set; }
        public string? ImageUrl { get; set; }

        public List<AnswerOptionDto> Options { get; set; } = new();
    }

    public class AnswerOptionDto
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class ExerciseResultDto
    {
        public int AttemptId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; }

        public AttemptStatus Status { get; set; }

        public DateTime StartTime { get; set; }
        // Thời điểm nộp bài thực tế
        public DateTime SubmittedAt { get; set; }

        public TimeSpan Duration { get; set; }
        public double TotalScore { get; set; }
        public double MaxScore { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public bool IsPassed { get; set; }
        public List<AnswerDetailDto> AnswerDetails { get; set; } = new();
    }

    public class AnswerDetailDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string? StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsAnswered { get; set; }
        public bool IsCorrect { get; set; }
        public double PointsEarned { get; set; }
        public double MaxScores { get; set; }
        public string? Explanation { get; set; }

        // AI Feedback fields
        public string? FullSolution { get; set; }
        public string? MistakeAnalysis { get; set; }
        public string? ImprovementAdvice { get; set; }
    }
}
