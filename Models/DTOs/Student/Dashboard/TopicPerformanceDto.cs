namespace ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard
{
    public class TopicPerformanceDto
    {
        public string TopicName { get; set; }
        public string ChapterName { get; set; }
        public decimal AverageScore { get; set; } // Thang 10
        public int TotalAttempts { get; set; }
        public bool IsStrength => AverageScore >= 8.0m;
        public bool IsWeakness => AverageScore < 5.0m;
    }
}
