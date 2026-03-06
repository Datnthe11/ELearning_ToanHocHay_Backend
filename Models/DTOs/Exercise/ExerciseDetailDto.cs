using ELearning_ToanHocHay_Control.Models.DTOs.Question;

namespace ELearning_ToanHocHay_Control.Models.DTOs.Exercise
{
    public class ExerciseDetailDto
    {
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; }
        public int? DurationMinutes { get; set; }
        // Sử dụng QuestionDto đã định nghĩa bên dưới
        public List<QuestionDto> Questions { get; set; } = new List<QuestionDto>();
        public bool IsFree { get; set; } = true; // ← THÊM
    }

}