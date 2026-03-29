using ELearning_ToanHocHay_Control.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ELearning_ToanHocHay_Control.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        protected AppDbContext()
        {
        }

        #region DbSet
        public DbSet<User> Users { get; set; }
        public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<StudentParent> StudentParents { get; set; }
        public DbSet<Package> Packages { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<Curriculum> Curriculums { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonContent> LessonContents { get; set; }
        public DbSet<QuestionBank> QuestionBanks { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<QuestionTag> QuestionTags { get; set; }
        public DbSet<Exercise> Exercises { get; set; }
        public DbSet<ExerciseQuestion> ExerciseQuestions { get; set; }
        public DbSet<ExerciseAttempt> ExerciseAttempts { get; set; }
        public DbSet<StudentAnswer> StudentAnswers { get; set; }
        public DbSet<AIFeedback> AIFeedbacks { get; set; }
        public DbSet<AIHint> AIHints { get; set; }
        public DbSet<LearningPath> LearningPaths { get; set; }
        public DbSet<StudentProgress> StudentProgresses { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SupportMessage> SupportMessages { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SystemConfig> SystemConfigs { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }
        public DbSet<TabSwitchLog> TabSwitchLogs { get; set; }

        #endregion

        #region OnModelCreating
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================================
            // 1. NHÓM NGƯỜI DÙNG (USER, STUDENT, PARENT)
            // ============================================================
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.UserType).HasConversion<string>();
            });

            modelBuilder.Entity<EmailVerificationToken>(entity =>
            {
                entity.ToTable("EmailVerificationToken");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Token)
                    .IsUnique();

                entity.Property(e => e.ExpiredAt)
                    .IsRequired();

                entity.Property(e => e.IsUsed)
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Student>(entity =>
            {
                entity.ToTable("Student");
                entity.HasKey(e => e.StudentId);
                entity.HasOne(s => s.User)
                      .WithOne(u => u.Student)
                      .HasForeignKey<Student>(s => s.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Parent>(entity =>
            {
                entity.ToTable("Parent");
                entity.HasKey(e => e.ParentId);
                entity.HasOne(p => p.User)
                      .WithOne(u => u.Parent)
                      .HasForeignKey<Parent>(p => p.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StudentParent>(entity =>
            {
                entity.ToTable("StudentParent");
                entity.HasKey(sp => new { sp.StudentId, sp.ParentId });
                entity.HasOne(sp => sp.Student)
                      .WithMany(s => s.StudentParents)
                      .HasForeignKey(sp => sp.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sp => sp.Parent)
                      .WithMany(p => p.StudentParents)
                      .HasForeignKey(sp => sp.ParentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // 2. NHÓM NỘI DUNG (CURRICULUM, CHAPTER, TOPIC, LESSON)
            // ============================================================
            modelBuilder.Entity<Curriculum>(entity =>
            {
                entity.HasOne(c => c.Creator)
                      .WithMany(u => u.Curriculums)
                      .HasForeignKey(c => c.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Chapter>(entity =>
            {
                entity.HasOne(ch => ch.Curriculum)
                      .WithMany(c => c.Chapters)
                      .HasForeignKey(ch => ch.CurriculumId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Topic>(entity =>
            {
                entity.HasOne(t => t.Chapter)
                      .WithMany(ch => ch.Topics)
                      .HasForeignKey(t => t.ChapterId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.ToTable("Lesson");
                entity.HasOne(l => l.Topic)
                      .WithMany(t => t.Lessons)
                      .HasForeignKey(l => l.TopicId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.Creator)
                      .WithMany(u => u.CreatedLessons)
                      .HasForeignKey(l => l.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.Reviewer)
                      .WithMany(u => u.ReviewedLessons)
                      .HasForeignKey(l => l.ReviewedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<LessonContent>(entity =>
            {
                entity.ToTable("LessonContent");
                entity.HasOne(lc => lc.Lesson)
                      .WithMany(l => l.LessonContents)
                      .HasForeignKey(lc => lc.LessonId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // 3. NHÓM NGÂN HÀNG CÂU HỎI & TAGS
            // ============================================================
            // FIX LỖI: QuestionBank có ChapterId và TopicId (cả 2 nullable)
            // Vì Topic → Chapter tạo cascade path conflict
            modelBuilder.Entity<QuestionBank>(entity =>
            {
                entity.HasOne(qb => qb.Chapter)
                      .WithMany(c => c.QuestionBanks)
                      .HasForeignKey(qb => qb.ChapterId)
                      .OnDelete(DeleteBehavior.ClientSetNull); // ClientSetNull để tránh cascade conflict

                entity.HasOne(qb => qb.Topic)
                      .WithMany(t => t.QuestionBanks)
                      .HasForeignKey(qb => qb.TopicId)
                      .OnDelete(DeleteBehavior.ClientSetNull); // ClientSetNull để tránh cascade conflict
            });

            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasOne(q => q.QuestionBank)
                      .WithMany(qb => qb.Questions)
                      .HasForeignKey(q => q.BankId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(q => q.Creator)
                      .WithMany(u => u.CreatedQuestions)
                      .HasForeignKey(q => q.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(q => q.Reviewer)
                      .WithMany(u => u.ReviewedQuestions)
                      .HasForeignKey(q => q.ReviewedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<QuestionOption>(entity =>
            {
                entity.HasOne(o => o.Question)
                    .WithMany(q => q.QuestionOptions)
                    .HasForeignKey(o => o.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<QuestionTag>(entity =>
            {
                entity.HasKey(qt => new { qt.QuestionId, qt.TagId });
                entity.HasOne(qt => qt.Question)
                      .WithMany(q => q.QuestionTags)
                      .HasForeignKey(qt => qt.QuestionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(qt => qt.Tag)
                      .WithMany(t => t.QuestionTags)
                      .HasForeignKey(qt => qt.TagId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ============================================================
            // 4. NHÓM BÀI TẬP & KẾT QUẢ HỌC TẬP
            // ============================================================
            // FIX LỖI: Exercise có TopicId và ChapterId (cả 2 nullable)
            // Giống QuestionBank - tạo multiple cascade paths
            modelBuilder.Entity<Exercise>(entity =>
            {
                entity.HasOne(e => e.Topic)
                      .WithMany(t => t.Exercises)
                      .HasForeignKey(e => e.TopicId)
                      .OnDelete(DeleteBehavior.ClientSetNull); // ClientSetNull để tránh cascade conflict

                entity.HasOne(e => e.Chapter)
                      .WithMany(c => c.Exercises)
                      .HasForeignKey(e => e.ChapterId)
                      .OnDelete(DeleteBehavior.ClientSetNull); // ClientSetNull để tránh cascade conflict

                entity.HasOne(e => e.Creator)
                      .WithMany(u => u.Exercises)
                      .HasForeignKey(e => e.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ExerciseQuestion>(entity =>
            {
                entity.HasKey(eq => new { eq.ExerciseId, eq.QuestionId });
                entity.HasOne(eq => eq.Exercise)
                      .WithMany(e => e.ExerciseQuestions)
                      .HasForeignKey(eq => eq.ExerciseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(eq => eq.Question)
                      .WithMany(q => q.ExerciseQuestions)
                      .HasForeignKey(eq => eq.QuestionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ExerciseAttempt>(entity =>
            {
                entity.Property(ea => ea.CompletionPercentage).HasPrecision(18, 2);

                entity.HasOne(ea => ea.Student)
                      .WithMany(s => s.ExerciseAttempts)
                      .HasForeignKey(ea => ea.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ea => ea.Exercise)
                      .WithMany(e => e.ExerciseAttempts)
                      .HasForeignKey(ea => ea.ExerciseId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StudentAnswer>()
                .HasIndex(x => new { x.AttemptId, x.QuestionId })
                .IsUnique();

            modelBuilder.Entity<StudentAnswer>(entity =>
            {
                entity.HasOne(sa => sa.ExerciseAttempt)
                      .WithMany(a => a.StudentAnswers)
                      .HasForeignKey(sa => sa.AttemptId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sa => sa.Question)
                      .WithMany(q => q.StudentAnswers)
                      .HasForeignKey(sa => sa.QuestionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sa => sa.SelectedOption)
                      .WithMany()
                      .HasForeignKey(sa => sa.SelectedOptionId)
                      .OnDelete(DeleteBehavior.ClientSetNull)
                      .IsRequired(false);
            });

            modelBuilder.Entity<StudentProgress>(entity =>
            {
                entity.HasIndex(sp => new { sp.StudentId, sp.TopicId }).IsUnique();

                entity.HasOne(sp => sp.Student)
                      .WithMany(s => s.StudentProgresses)
                      .HasForeignKey(sp => sp.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sp => sp.Topic)
                      .WithMany(t => t.studentProgresses)
                      .HasForeignKey(sp => sp.TopicId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AIFeedback>(entity =>
            {
                entity.HasOne(af => af.Attempt)
                      .WithMany(a => a.AIFeedbacks)
                      .HasForeignKey(af => af.AttemptId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(af => af.Question)
                      .WithMany(q => q.AIFeedbacks)
                      .HasForeignKey(af => af.QuestionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AIHint>(entity =>
            {
                entity.HasOne(af => af.Attempt)
                      .WithMany(a => a.AIHints)
                      .HasForeignKey(af => af.AttemptId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(af => af.Question)
                      .WithMany(q => q.AIHints)
                      .HasForeignKey(af => af.QuestionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // 5. NHÓM GÓI CƯỚC & THANH TOÁN
            // ============================================================
            modelBuilder.Entity<Package>(entity =>
            {
                entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
                entity.HasOne(p => p.User)
                      .WithMany(u => u.Packages)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("Payment");
                entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                entity.HasOne(p => p.Student)
                      .WithMany(s => s.Payments)
                      .HasForeignKey(p => p.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.ToTable("Subscription");
                entity.Property(s => s.AmountPaid).HasColumnType("decimal(18,2)");

                entity.HasOne(s => s.Student)
                      .WithMany(st => st.Subscriptions)
                      .HasForeignKey(s => s.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Payment)
                      .WithOne(p => p.Subscription)
                      .HasForeignKey<Subscription>(s => s.PaymentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // 6. NHÓM HỖ TRỢ, THÔNG BÁO & HỆ THỐNG
            // ============================================================
            modelBuilder.Entity<SupportTicket>(entity =>
            {
                entity.HasOne(st => st.CreatedBy)
                      .WithMany(u => u.CreatedSupportTickets)
                      .HasForeignKey(st => st.CreatedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(st => st.AssignedStaff)
                      .WithMany(u => u.AssignedSupportTickets)
                      .HasForeignKey(st => st.AssignedToStaffId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SupportMessage>(entity =>
            {
                entity.HasOne(sm => sm.Ticket)
                      .WithMany(t => t.Messages)
                      .HasForeignKey(sm => sm.TicketId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sm => sm.Sender)
                      .WithMany(u => u.SupportMessages)
                      .HasForeignKey(sm => sm.SenderUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasOne(al => al.User)
                      .WithMany(u => u.AuditLogs)
                      .HasForeignKey(al => al.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SystemConfig>(entity =>
            {
                entity.HasIndex(sc => sc.ConfigKey).IsUnique();
                entity.HasOne(sc => sc.UpdatedByUser)
                      .WithMany(u => u.SystemConfigs)
                      .HasForeignKey(sc => sc.UpdatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LearningPath>(entity =>
            {
                entity.HasOne(lp => lp.Student)
                      .WithMany(s => s.LearningPaths)
                      .HasForeignKey(lp => lp.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TabSwitchLog>(entity =>
            {
                entity.HasOne(t => t.Attempt)
                      .WithMany()
                      .HasForeignKey(t => t.AttemptId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
        #endregion
    }
}