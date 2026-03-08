namespace ELearning_ToanHocHay_Control.Models.DTOs.Student.Dashboard
{
    public class WeakTopicDto
    {
        public int TopicId { get; set; }
        public string TopicName { get; set; }
        public string ChapterName { get; set; }
        public int ErrorCount { get; set; }
        public int? FirstLessonId { get; set; }
        public List<string> LessonNames { get; set; } = new();
    }
}
