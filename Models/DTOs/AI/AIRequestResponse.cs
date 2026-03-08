using System.Text.Json.Serialization;

namespace ELearning_ToanHocHay_Control.Models.DTOs.AI
{
    public class AIHintRequest
    {
        [JsonPropertyName("question_text")]
        public string QuestionText { get; set; }

        [JsonPropertyName("question_type")]
        public string QuestionType { get; set; }

        [JsonPropertyName("difficulty_level")]
        public string DifficultyLevel { get; set; }

        [JsonPropertyName("student_answer")]
        public string StudentAnswer { get; set; }

        [JsonPropertyName("hint_level")]
        public int HintLevel { get; set; } = 1;

        [JsonPropertyName("question_id")]
        public int? QuestionId { get; set; }

        [JsonPropertyName("question_image_url")]
        public string? QuestionImageUrl { get; set; }

        [JsonPropertyName("options")]
        public List<AIOptionDto>? Options { get; set; }
    }

    public class AIHintResponse
    {
        [JsonPropertyName("hint_text")]
        public string HintText { get; set; }

        [JsonPropertyName("hint_level")]
        public int HintLevel { get; set; }

        [JsonPropertyName("question_id")]
        public int? QuestionId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class AIFeedbackRequest
    {
        [JsonPropertyName("question_text")]
        public string QuestionText { get; set; }

        [JsonPropertyName("question_type")]
        public string QuestionType { get; set; }

        [JsonPropertyName("student_answer")]
        public string StudentAnswer { get; set; }

        [JsonPropertyName("correct_answer")]
        public string CorrectAnswer { get; set; }

        [JsonPropertyName("is_correct")]
        public bool IsCorrect { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }

        [JsonPropertyName("attempt_id")]
        public int? AttemptId { get; set; }

        [JsonPropertyName("question_id")]
        public int? QuestionId { get; set; }

        [JsonPropertyName("question_image_url")]
        public string? QuestionImageUrl { get; set; }

        [JsonPropertyName("options")]
        public List<AIOptionDto>? Options { get; set; }
    }

    public class AIFeedbackResponse
    {
        [JsonPropertyName("full_solution")]
        public string FullSolution { get; set; }

        [JsonPropertyName("mistake_analysis")]
        public string MistakeAnalysis { get; set; }

        [JsonPropertyName("improvement_advice")]
        public string ImprovementAdvice { get; set; }

        [JsonPropertyName("attempt_id")]
        public int? AttemptId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public class AIInsightRequest
    {
        [JsonPropertyName("question_text")]
        public string QuestionText { get; set; }

        [JsonPropertyName("student_answer")]
        public string StudentAnswer { get; set; } = string.Empty;

        [JsonPropertyName("correct_answer")]
        public string CorrectAnswer { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "assessment";
    }

    public class AIInsightResponse
    {
        [JsonPropertyName("concepts_to_review")]
        public List<string> ConceptsToReview { get; set; } = new();

        [JsonPropertyName("recommended_exercises")]
        public List<string> RecommendedExercises { get; set; } = new();

        [JsonPropertyName("quick_tips")]
        public List<string> QuickTips { get; set; } = new();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("lesson_id")]
        public int? LessonId { get; set; }
    }

    public class AIOptionDto
    {
        [JsonPropertyName("option_id")]
        public int OptionId { get; set; }

        [JsonPropertyName("option_text")]
        public string OptionText { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("is_correct")]
        public bool IsCorrect { get; set; }
    }
}
