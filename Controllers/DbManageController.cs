using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DbManageController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DbManageController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var curriculums = await _context.Curriculums.Select(c => new {
                c.CurriculumId,
                c.CurriculumName,
                c.GradeLevel,
                ChapterCount = _context.Chapters.Count(ch => ch.CurriculumId == c.CurriculumId)
            }).ToListAsync();

            var summary = new
            {
                Curriculums = curriculums,
                TotalChapters = await _context.Chapters.CountAsync(),
                TotalTopics = await _context.Topics.CountAsync(),
                TotalLessons = await _context.Lessons.CountAsync(),
                TotalExercises = await _context.Exercises.CountAsync()
            };
            return Ok(summary);
        }

        [HttpPost("seed-basic-data")]
        public async Task<IActionResult> Seed()
        {
            // Seed a Curriculum if none exists with this name
            var name = "Toán Lớp 6 - Kết nối tri thức";
            var curriculum = await _context.Curriculums.FirstOrDefaultAsync(c => c.CurriculumName == name);
            if (curriculum == null)
            {
                curriculum = new Curriculum
                {
                    CurriculumName = name,
                    GradeLevel = 6,
                    Subject = "Toán học",
                    Description = "Dữ liệu mẫu cho hệ thống bài giảng",
                    Status = CurriculumStatus.Published,
                    CreatedBy = 1,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Curriculums.Add(curriculum);
                await _context.SaveChangesAsync();
            }

            // Seed Chapter if none for this curriculum
            if (!await _context.Chapters.AnyAsync(ch => ch.CurriculumId == curriculum.CurriculumId))
            {
                var ch1 = new Chapter
                {
                    CurriculumId = curriculum.CurriculumId,
                    ChapterName = "Chương 1: Số tự nhiên",
                    OrderIndex = 1,
                    Description = "Làm quen với tập hợp và các phép toán cơ bản",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Chapters.Add(ch1);
                await _context.SaveChangesAsync();

                var topic1 = new Topic
                {
                    ChapterId = ch1.ChapterId,
                    TopicName = "Tập hợp và phần tử của tập hợp",
                    OrderIndex = 1,
                    Description = "Khái niệm mở đầu về tập hợp",
                    IsFree = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Topics.Add(topic1);
                await _context.SaveChangesAsync();

                var lesson1 = new Lesson
                {
                    TopicId = topic1.TopicId,
                    LessonName = "Bài 1: Tập hợp",
                    Description = "Bài học về khái niệm tập hợp",
                    DurationMinutes = 15,
                    OrderIndex = 1,
                    IsFree = true,
                    IsActive = true,
                    Status = LessonStatus.Approved,
                    CreatedBy = 1,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Lessons.Add(lesson1);
                
                var lessonContents = new List<LessonContent>
                {
                    new LessonContent { Lesson = lesson1, BlockType = LessonBlockType.Text, ContentText = "Chào mừng bạn đến với bài học đầu tiên!", OrderIndex = 1 },
                    new LessonContent { Lesson = lesson1, BlockType = LessonBlockType.Image, ContentUrl = "https://example.com/set.jpg", OrderIndex = 2 }
                };
                _context.LessonContents.AddRange(lessonContents);
                
                await _context.SaveChangesAsync();
                return Ok(new { message = "Data seeded successfully for Curriculum ID: " + curriculum.CurriculumId });
            }

            return Ok(new { message = "Data already exists for Curriculum ID: " + curriculum.CurriculumId });
        }
        [HttpGet("check-raw")]
        public async Task<IActionResult> CheckRaw()
        {
            var report = new
            {
                Curriculums = await _context.Curriculums.Select(c => new { c.CurriculumId, c.CurriculumName, c.GradeLevel }).ToListAsync(),
                Chapters = await _context.Chapters.Select(c => new { c.ChapterId, c.ChapterName, c.CurriculumId }).ToListAsync(),
                Topics = await _context.Topics.Select(t => new { t.TopicId, t.TopicName, t.ChapterId }).ToListAsync(),
                Lessons = await _context.Lessons.Select(l => new { l.LessonId, l.LessonName, l.TopicId }).ToListAsync(),
                Exercises = await _context.Exercises.Select(e => new { e.ExerciseId, e.ExerciseName, e.ChapterId }).ToListAsync()
            };

            return Ok(new {
                Message = "Database Raw Check Report",
                Counts = new {
                    Curriculums = report.Curriculums.Count,
                    Chapters = report.Chapters.Count,
                    Topics = report.Topics.Count,
                    Lessons = report.Lessons.Count,
                    Exercises = report.Exercises.Count
                },
                Data = report
            });
        }
    }
}
