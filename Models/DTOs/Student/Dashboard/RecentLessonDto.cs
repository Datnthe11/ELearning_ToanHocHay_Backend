namespace ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard
{
    public class RecentLessonDto
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }
        public string TopicName { get; set; }
        public string ChapterName { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
        public int ProgressPercentage { get; set; }  // Nếu chưa hoàn thành
        public double? Score { get; set; }

        // Mới thêm cho Parent Dashboard
        public int? AttemptId { get; set; }
        public int? TabSwitchCount { get; set; }
    }
}
