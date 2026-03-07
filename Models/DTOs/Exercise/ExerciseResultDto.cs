namespace ELearning_ToanHocHay_Control.Models.DTOs.Exercise // Ở Backend thì đổi thành namespace ELearning_ToanHocHay_Control.Models.DTOs
{
    public class ExerciseResultDto
    {
        public int AttemptId { get; set; }
        public string? ExerciseName { get; set; }
        public int ExerciseId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public double TotalScore { get; set; }
        public double MaxScore { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int TotalQuestions { get; set; }
        public bool IsPassed { get; set; }
        public List<AnswerDetailDto> AnswerDetails { get; set; } = new List<AnswerDetailDto>();
    }

    public class AnswerDetailDto
    {
        public int QuestionId { get; set; }
        public string? QuestionText { get; set; }
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
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