namespace ELearning_ToanHocHay_Control.Models.DTOs.AIFeedback
{
    public class CreateAIFeedbackDto
    {
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public string? StudentAnswer { get; set; }
        public string? FullSolution { get; set; }
        public string? MistakeAnalysis { get; set; }
        public string? ImprovementAdvice { get; set; }
    }
}
