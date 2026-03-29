namespace ELearning_ToanHocHay_Control.Models
{
    public class RecentLessonModel
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; }
        public string TopicName { get; set; }
        public string ChapterName { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? DurationMinutes { get; set; }
        public bool IsCompleted { get; set; }
        public int ProgressPercentage { get; set; }
        public double? Score { get; set; } // THÊM DÒNG NÀY
        // Mới thêm cho tính năng theo dõi vi phạm
        public int? AttemptId { get; set; }
        public int? TabSwitchCount { get; set; }
    }
}
