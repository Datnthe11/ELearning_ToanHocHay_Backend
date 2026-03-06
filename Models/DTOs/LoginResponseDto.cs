using ELearning_ToanHocHay_Control.Data.Entities;

namespace ELearning_ToanHocHay_Control.Models.DTOs
{
    public class LoginResponseDto
    {
        public int UserId { get; set; }
        public int? StudentId { get; set; }
        public int? ParentId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public UserType UserType { get; set; }
        public string Token { get; set; }
        public DateTime TokenExpiration { get; set; }
        public string AvatarUrl { get; set; }
        public int PackageType { get; set; } = 0; // ← THÊM
    }
}
