using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Models.DTOs.Chapter;
using ELearning_ToanHocHay_Control.Models.DTOs.Lesson;
using ELearning_ToanHocHay_Control.Models.DTOs.Topic;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
// Đặt Alias để tránh lỗi Ambiguous Reference (nhập nhằng tên)
using DtoStatus = ELearning_ToanHocHay_Control.Models.DTOs.CurriculumStatus;
using EntityStatus = ELearning_ToanHocHay_Control.Data.Entities.CurriculumStatus;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class CurriculumService : ICurriculumService
    {
        private readonly ICurriculumRepository _curriculumRepository;

        public CurriculumService(ICurriculumRepository curriculumRepository)
        {
            _curriculumRepository = curriculumRepository;
        }

        // ===================== MAPPING LOGIC (QUAN TRỌNG) =====================
        private static CurriculumDto MapToDto(Curriculum entity)
        {
            if (entity == null) return null!;

            return new CurriculumDto
            {
                CurriculumId = entity.CurriculumId,
                GradeLevel = entity.GradeLevel,
                Subject = entity.Subject,
                CurriculumName = entity.CurriculumName,
                Description = entity.Description,
                // Ép kiểu tường minh bằng cách dùng Alias đã khai báo ở đầu file
                Status = (DtoStatus)(int)entity.Status,
                Version = entity.Version,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,

                // Ánh xạ danh sách lồng nhau: Chapters -> Topics -> Lessons
                Chapters = entity.Chapters?.Select(ch => new ChapterDto
                {
                    ChapterId = ch.ChapterId,
                    ChapterName = ch.ChapterName,
                    OrderIndex = ch.OrderIndex,
                    Topics = ch.Topics?.Select(t => new TopicDto
                    {
                        TopicId = t.TopicId,
                        TopicName = t.TopicName,
                        OrderIndex = t.OrderIndex,
                        Lessons = t.Lessons?.Select(l => new LessonDto
                        {
                            LessonId = l.LessonId,
                            LessonName = l.LessonName,
                            DurationMinutes = l.DurationMinutes,
                            OrderIndex = l.OrderIndex
                        }).OrderBy(l => l.OrderIndex).ToList() ?? new List<LessonDto>()
                    }).OrderBy(t => t.OrderIndex).ToList() ?? new List<TopicDto>()
                }).OrderBy(ch => ch.OrderIndex).ToList() ?? new List<ChapterDto>()
            };
        }

        // ===================== CÁC HÀM XỬ LÝ NGHIỆP VỤ =====================

        public async Task<ApiResponse<CurriculumDto>> GetByIdAsync(int id)
        {
            var entity = await _curriculumRepository.GetCurriculumByIdAsync(id);
            if (entity != null)
            {
                Console.WriteLine("====================================");
                Console.WriteLine($"KIỂM TRA API: Curriculum ID = {id}");
                Console.WriteLine($"Số lượng Chapter tìm thấy: {entity.Chapters?.Count ?? 0}");

                if (entity.Chapters != null && entity.Chapters.Any())
                {
                    foreach (var ch in entity.Chapters)
                    {
                        Console.WriteLine($"+ Đã lấy được chương: {ch.ChapterName}");
                    }
                }
                else
                {
                    Console.WriteLine("!!! CẢNH BÁO: Không tìm thấy Chapter nào trong Database cho ID này.");
                }
                Console.WriteLine("====================================");
            }

            return ApiResponse<CurriculumDto>.SuccessResponse(MapToDto(entity));
        }

        public async Task<ApiResponse<IEnumerable<CurriculumDto>>> GetAllAsync()
        {
            var curriculums = await _curriculumRepository.GetAllAsync();
            var result = curriculums.Select(MapToDto);

            return ApiResponse<IEnumerable<CurriculumDto>>.SuccessResponse(result, "Lấy danh sách thành công");
        }

        public async Task<ApiResponse<CurriculumDto>> CreateAsync(CreateCurriculumDto dto, int currentUserId)
        {
            var curriculum = new Curriculum
            {
                GradeLevel = dto.GradeLevel,
                Subject = dto.Subject,
                CurriculumName = dto.CurriculumName,
                Description = dto.Description,
                // Chuyển đổi Enum từ DTO sang Entity
                Status = (EntityStatus)(int)dto.Status,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow,
                Version = 1
            };

            var created = await _curriculumRepository.CreateCurriculumAsync(curriculum);
            return ApiResponse<CurriculumDto>.SuccessResponse(MapToDto(created), "Tạo mới thành công");
        }

        public async Task<ApiResponse<CurriculumDto>> UpdateAsync(int curriculumId, UpdateCurriculumDto dto)
        {
            var existing = await _curriculumRepository.GetCurriculumByIdAsync(curriculumId);
            if (existing == null)
                return ApiResponse<CurriculumDto>.ErrorResponse("Không tìm thấy curriculum để cập nhật.");

            existing.GradeLevel = dto.GradeLevel;
            existing.Subject = dto.Subject;
            existing.CurriculumName = dto.CurriculumName;
            existing.Description = dto.Description;
            existing.Status = (EntityStatus)(int)dto.Status;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Version++;

            var updated = await _curriculumRepository.UpdateCurriculumAsync(existing);
            return ApiResponse<CurriculumDto>.SuccessResponse(MapToDto(updated!), "Cập nhật thành công");
        }

        // ... Các hàm Delete, Publish, Archive giữ nguyên logic ...
        public async Task<ApiResponse<bool>> DeleteAsync(int curriculumId)
        {
            var deleted = await _curriculumRepository.DeleteCurriculumAsync(curriculumId);
            return deleted
                ? ApiResponse<bool>.SuccessResponse(true, "Xóa thành công")
                : ApiResponse<bool>.ErrorResponse("Xóa thất bại");
        }

        public async Task<ApiResponse<bool>> PublishAsync(int curriculumId)
        {
            var curriculum = await _curriculumRepository.GetCurriculumByIdAsync(curriculumId);
            if (curriculum == null) return ApiResponse<bool>.ErrorResponse("Không tìm thấy");

            curriculum.Status = EntityStatus.Published;
            curriculum.UpdatedAt = DateTime.UtcNow;
            await _curriculumRepository.UpdateCurriculumAsync(curriculum);
            return ApiResponse<bool>.SuccessResponse(true, "Đã xuất bản");
        }

        public async Task<ApiResponse<bool>> ArchiveAsync(int curriculumId)
        {
            var curriculum = await _curriculumRepository.GetCurriculumByIdAsync(curriculumId);
            if (curriculum == null) return ApiResponse<bool>.ErrorResponse("Không tìm thấy");

            curriculum.Status = EntityStatus.Archived;
            curriculum.UpdatedAt = DateTime.UtcNow;
            await _curriculumRepository.UpdateCurriculumAsync(curriculum);
            return ApiResponse<bool>.SuccessResponse(true, "Đã lưu trữ");
        }
    }
}