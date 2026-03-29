using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELearning_ToanHocHay_Control.Data.Entities
{
    [Table("TabSwitchLog")]
    public class TabSwitchLog
    {
        [Key]
        public int Id { get; set; }

        public int AttemptId { get; set; }
        public DateTime SwitchedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ExerciseAttempt? Attempt { get; set; }
    }

}
