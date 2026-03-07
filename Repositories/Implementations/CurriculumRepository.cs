using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Repositories.Implementations
{
    public class CurriculumRepository : ICurriculumRepository
    {
        private readonly AppDbContext _context;

        public CurriculumRepository(AppDbContext context)
        {
            _context = context;
        }
        public async Task<Curriculum> CreateCurriculumAsync(Curriculum curriculum)
        {
            _context.Curriculums.Add(curriculum);
            await _context.SaveChangesAsync();
            return curriculum;
        }

        public async Task<bool> DeleteCurriculumAsync(int curriculumId)
        {
            var curriculum = await _context.Curriculums
                .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId);

            if (curriculum == null)
                return false;

            _context.Curriculums.Remove(curriculum);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            var entity = await GetCurriculumByIdAsync(id);
            return entity != null;
        }

        public async Task<IEnumerable<Curriculum>> GetAllAsync()
        {
            return await _context.Curriculums.ToListAsync();
        }

        public async Task<Curriculum?> GetCurriculumByIdAsync(int curriculumId)
        {
            return await _context.Curriculums
                .Include(c => c.Chapters)          // Lôi chương lên
                    .ThenInclude(ch => ch.Topics)  // Lôi chủ đề trong chương lên
                        .ThenInclude(t => t.Lessons) // Lôi bài học trong chủ đề lên
                .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId);
        }

        public async Task<Curriculum?> UpdateCurriculumAsync(Curriculum curriculum)
        {
            var existing = await _context.Curriculums
                .FirstOrDefaultAsync(c => c.CurriculumId == curriculum.CurriculumId);

            if (existing == null)
                return null;

            // Map field (tránh overwrite không cần thiết)
            existing.CurriculumName = curriculum.CurriculumName;
            existing.Description = curriculum.Description;
            existing.GradeLevel = curriculum.GradeLevel;
            existing.Version = curriculum.Version;
            existing.Status = curriculum.Status;

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
