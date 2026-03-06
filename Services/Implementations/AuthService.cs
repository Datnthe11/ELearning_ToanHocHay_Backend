using System;
using ELearning_ToanHocHay_Control.Data;
using ELearning_ToanHocHay_Control.Data.Entities;
using ELearning_ToanHocHay_Control.Models.DTOs;
using ELearning_ToanHocHay_Control.Repositories.Implementations;
using ELearning_ToanHocHay_Control.Repositories.Interfaces;
using ELearning_ToanHocHay_Control.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning_ToanHocHay_Control.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IUserRepository _userRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IParentRepository _parentRepository;
        private readonly IJwtService _jwtService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailService _emailService;
        private readonly AppSettings _appSettings;
        private readonly IBackgroundEmailService _backgroundEmailService;
        private readonly IConfiguration _configuration;

        public AuthService(
            AppDbContext context,
            IUserRepository userRepository,
            IStudentRepository studentRepository,
            IParentRepository parentRepository,
            IJwtService jwtService,
            IPasswordHasher passwordHasher,
            IEmailService emailService,
            IOptions<AppSettings> appSettings,
            IBackgroundEmailService backgroundEmailService,
            IConfiguration configuration)
        {
            _context = context;
            _userRepository = userRepository;
            _studentRepository = studentRepository;
            _parentRepository = parentRepository;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
            _appSettings = appSettings.Value;
            _backgroundEmailService = backgroundEmailService;
            _configuration = configuration;
        }

        public async Task<ApiResponse<bool>> ChangePasswordAsync(int userId, ChangePasswordDto request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<bool>.ErrorResponse("User không tồn tại");

            if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                return ApiResponse<bool>.ErrorResponse("Mật khẩu hiện tại không đúng");
            }

            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            await _userRepository.UpdateUserAsync(user);

            return ApiResponse<bool>.SuccessResponse(true, "Đổi mật khẩu thành công");
        }

        public async Task<ApiResponse<bool>> ConfirmEmailAsync(string token)
        {
            var emailToken = await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                x.Token == token &&
                !x.IsUsed &&
                x.ExpiredAt > DateTime.UtcNow);

            if (emailToken == null)
                return ApiResponse<bool>.ErrorResponse("Token không hợp lệ");

            emailToken.User.IsEmailConfirmed = true;
            emailToken.User.EmailConfirmedAt = DateTime.UtcNow;
            emailToken.IsUsed = true;

            await _context.SaveChangesAsync();

            return ApiResponse<bool>.SuccessResponse(true, "Xác nhận email thành công");
        }

        public async Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto request)
        {
            try
            {
                int? studentId = null;
                int? parentId = null;
                int packageType = 0; // ← THÊM

                var user = await _userRepository.GetByEmailAsync(request.Email);
                if (user == null)
                    return ApiResponse<LoginResponseDto>.ErrorResponse("Email hoặc mật khẩu không đúng");

                if (!user.IsEmailConfirmed)
                    return ApiResponse<LoginResponseDto>.ErrorResponse("Vui lòng xác nhận email trước khi đăng nhập");

                if (!user.IsActive)
                    return ApiResponse<LoginResponseDto>.ErrorResponse("Tài khoản đã bị vô hiệu hóa");

                if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                    return ApiResponse<LoginResponseDto>.ErrorResponse("Email hoặc mật khẩu không đúng");

                if (user.UserType == UserType.Student)
                {
                    var student = await _studentRepository.GetByUserIdAsync(user.UserId);
                    if (student == null)
                        return ApiResponse<LoginResponseDto>.ErrorResponse("Không tìm thấy thông tin học sinh");

                    studentId = student.StudentId;

                    // ← THÊM ĐOẠN NÀY: Lấy PackageType từ Subscription active
                    var activeSub = await _context.Subscriptions
                        .Include(s => s.Package)
                        .Where(s => s.StudentId == student.StudentId
                                 && s.Status == SubscriptionStatus.Active
                                 && s.EndDate > DateTime.UtcNow)
                        .OrderByDescending(s => s.EndDate)
                        .FirstOrDefaultAsync();

                    if (activeSub?.Package != null)
                    {
                        var name = activeSub.Package.PackageName.ToLower();
                        packageType = name.Contains("premium") ? 2
                                    : name.Contains("standard") ? 1
                                    : 0;
                    }
                }
                else if (user.UserType == UserType.Parent)
                {
                    var parent = await _parentRepository.GetByUserIdAsync(user.UserId);
                    if (parent == null)
                        return ApiResponse<LoginResponseDto>.ErrorResponse("Không tìm thấy thông tin phụ huynh");

                    parentId = parent.ParentId;
                }

                var token = _jwtService.GenerateToken(user, studentId, parentId);
                var expirationStr = _configuration["JwtSettings:ExpirationMinutes"] ?? "30";
                int expirationMinutes = int.Parse(expirationStr);

                await _userRepository.UpdateLastLoginAsync(user.UserId);

                var response = new LoginResponseDto
                {
                    UserId = user.UserId,
                    StudentId = studentId,
                    ParentId = parentId,
                    Email = user.Email,
                    FullName = user.FullName,
                    UserType = user.UserType,
                    Token = token,
                    TokenExpiration = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    AvatarUrl = user.AvatarUrl,
                    PackageType = packageType // ← THÊM
                };

                return ApiResponse<LoginResponseDto>.SuccessResponse(response, "Đăng nhập thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<LoginResponseDto>.ErrorResponse("Đã xảy ra lỗi", new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<bool>> LogoutAsync(int userId)
        {
            try
            {
                // Có thể thêm logic blacklist token hoặc invalidate token ở đây
                // Hiện tại chỉ return success
                return ApiResponse<bool>.SuccessResponse(true, "Đăng xuất thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResponse(
                    "Đã xảy ra lỗi khi đăng xuất",
                    new List<string> { ex.Message }
                );
            }
        }

        public async Task<ApiResponse<string>> RefreshTokenAsync(string token)
        {
            try
            {
                int? studentId = null;
                int? parentId = null;
                var principal = _jwtService.ValidateToken(token);
                if (principal == null)
                {
                    return ApiResponse<string>.ErrorResponse("Token không hợp lệ");
                }

                var userId = _jwtService.GetUserIdFromToken(token);
                if (!userId.HasValue)
                {
                    return ApiResponse<string>.ErrorResponse("Token không hợp lệ");
                }

                var user = await _userRepository.GetByIdAsync(userId.Value);
                if (user == null || !user.IsActive)
                {
                    return ApiResponse<string>.ErrorResponse("User không tồn tại hoặc bị khóa");
                }

                if (user.UserType == UserType.Student)
                {
                    var student = await _studentRepository.GetByUserIdAsync(user.UserId);
                    if (student == null)
                    {
                        return ApiResponse<string>.ErrorResponse(
                            "Không tìm thấy thông tin học sinh",
                            new List<string> { "Dữ liệu hệ thống không đồng bộ" }
                        );
                    }

                    studentId = student.StudentId;
                }

                if (user.UserType == UserType.Parent)
                {
                    var parent = await _parentRepository.GetByUserIdAsync(user.UserId);
                    if (parent == null)
                    {
                        return ApiResponse<string>.ErrorResponse(
                            "Không tìm thấy thông tin phụ huynh",
                            new List<string> { "Dữ liệu hệ thống không đồng bộ" }
                        );
                    }

                    parentId = parent.ParentId;
                }

                var newToken = _jwtService.GenerateToken(user, studentId, parentId);
                return ApiResponse<string>.SuccessResponse(newToken, "Refresh token thành công");
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.ErrorResponse("Lỗi refresh token", new List<string> { ex.Message });
            }
        }

        public async Task<ApiResponse<bool>> RegisterAsync(RegisterRequestDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                
                var existingUser = await _userRepository.GetByEmailAsync(request.Email);

                // Check emial if it not confirm
                if (existingUser != null && !existingUser.IsEmailConfirmed)
                {
                    return ApiResponse<bool>.ErrorResponse("Email đã được đăng ký và chờ xác nhận");
                }

                // 1. Check email if email existed
                if (existingUser != null && existingUser.IsEmailConfirmed)
                {
                    return ApiResponse<bool>.ErrorResponse("Email đã được đăng ký");
                }

                // 2. Create User
                var user = new User
                {
                    Email = request.Email,
                    PasswordHash = _passwordHasher.HashPassword(request.Password),
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Dob = request.Dob, // Đảm bảo DTO gửi lên kiểu DateTime
                    UserType = request.UserType,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _userRepository.CreateUserAsync(user);
                await _context.SaveChangesAsync(); // để có UserId

                // 3. Role-specific
                switch (request.UserType)
                {
                    case UserType.Student:
                        if (!request.GradeLevel.HasValue)
                        {
                            return ApiResponse<bool>.ErrorResponse("Học sinh phải có khối lớp");
                        }

                        var student = new Student
                        {
                            UserId = user.UserId,
                            GradeLevel = request.GradeLevel.Value,
                            SchoolName = request.SchoolName
                        };

                        await _studentRepository.AddAsync(student);
                        break;

                    case UserType.Parent:
                        var parent = new Parent
                        {
                            UserId = user.UserId,
                            Job = request.Job
                        };

                        await _parentRepository.AddAsync(parent);
                        break;

                    default:
                        return ApiResponse<bool>.ErrorResponse("Không cho phép đăng ký role này");
                }


                // 4. Create Email Verification Token
                var tokenValue = Guid.NewGuid().ToString("N");

                var emailToken = new EmailVerificationToken
                {
                    UserId = user.UserId,
                    Token = tokenValue,
                    ExpiredAt = DateTime.UtcNow.AddHours(24),
                    IsUsed = false
                };

                await _context.EmailVerificationTokens.AddAsync(emailToken);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 5. Send email confirm
                var confirmLink =
                    $"{_appSettings.BaseUrl}/Account/ConfirmEmail?token={tokenValue}";

                _backgroundEmailService.QueueConfirmationEmail(user.Email, user.FullName, confirmLink);

                return ApiResponse<bool>.SuccessResponse(true, "Đăng ký thành công. Vui lòng kiểm tra email để xác nhận tài khoản");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // --- ĐÂY LÀ PHẦN QUAN TRỌNG ĐÃ ĐƯỢC SỬA ---
                // Kiểm tra xem lỗi gốc (InnerException) là gì
                var errorDetail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                return ApiResponse<bool>.ErrorResponse(
                    "Đăng ký thất bại",
                    new List<string> { errorDetail } // Trả về lỗi chi tiết nhất
                );
                // ------------------------------------------
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var principal = _jwtService.ValidateToken(token);
                if (principal == null)
                    return false;

                var userId = _jwtService.GetUserIdFromToken(token);
                if (!userId.HasValue)
                    return false;

                var user = await _userRepository.GetByIdAsync(userId.Value);
                return user != null && user.IsActive;
            }
            catch
            {
                return false;
            }
        }
        public async Task<ApiResponse<bool>> ResendConfirmationEmailAsync(string email)
        {
            // 1. Tìm user theo email
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
                return ApiResponse<bool>.ErrorResponse("Email không tồn tại trong hệ thống");

            // 2. Nếu đã xác nhận rồi thì không cần gửi lại
            if (user.IsEmailConfirmed)
                return ApiResponse<bool>.ErrorResponse("Tài khoản này đã được xác nhận trước đó");

            // 3. Vô hiệu hóa các token cũ (nếu có) để tránh lãng phí
            var oldTokens = await _context.EmailVerificationTokens
                .Where(t => t.UserId == user.UserId && !t.IsUsed)
                .ToListAsync();
            foreach (var t in oldTokens) t.IsUsed = true;

            // 4. Tạo Token mới (tương tự logic trong RegisterAsync)
            var newTokenValue = Guid.NewGuid().ToString("N");
            var emailToken = new EmailVerificationToken
            {
                UserId = user.UserId,
                Token = newTokenValue,
                ExpiredAt = DateTime.UtcNow.AddHours(24),
                IsUsed = false
            };

            await _context.EmailVerificationTokens.AddAsync(emailToken);
            await _context.SaveChangesAsync();

            // 5. Gửi mail
            var confirmLink = $"{_appSettings.BaseUrl}/Account/ConfirmEmail?token={newTokenValue}";
            _backgroundEmailService.QueueConfirmationEmail(user.Email, user.FullName, confirmLink);

            return ApiResponse<bool>.SuccessResponse(true, "Email xác nhận mới đã được gửi");
        }
    }
}