using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class LessonProgressService : ILessonProgressService
    {
        private readonly AppDbContext _context;
        private readonly ILessonRepository _lessonRepository;

        public LessonProgressService(AppDbContext context, ILessonRepository lessonRepository)
        {
            _context = context;
            _lessonRepository = lessonRepository;
        }
        // Làm nhanh, chưa theo cấu trúc repo. service
        public async Task UpdateLessonProgress(int studentId, int lessonId, int watchTime)
        {
            var lesson = await _lessonRepository.GetByIdAsync(lessonId);
            if (lesson == null)
            {
                throw new Exception("Lesson not found");
            }

            var progress = await _context.LessonProgresses
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.LessonId == lessonId);

            bool isCompleted = true;

            if (progress == null)
            {
                progress = new LessonProgress
                {
                    StudentId = studentId,
                    LessonId = lessonId,
                    WatchTime = watchTime,
                    IsCompleted = isCompleted,
                    LastAccessed = DateTime.UtcNow
                };

                _context.LessonProgresses.Add(progress);
            }
            else
            {
                progress.WatchTime = watchTime;
                progress.LastAccessed = DateTime.UtcNow;
                if (isCompleted)
                {
                    progress.IsCompleted = true;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
