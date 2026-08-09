using EduTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Group> Groups { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<ExamResult> ExamResults { get; set; }
        public DbSet<ExamAttempt> ExamAttempts { get; set; }
        public DbSet<GroupSubject> GroupSubjects { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Talaba o'chirilganda guruh o'chib ketmasligi uchun
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Group)
                .WithMany(g => g.Students)
                .HasForeignKey(u => u.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<Submission>()
    .HasIndex(s => new { s.AssignmentId, s.StudentId })
    .IsUnique();
            // O'qituvchi o'chirilganda fan o'chib ketmasligi uchun
            builder.Entity<Subject>()
                .HasOne(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Entity<Attendance>()
    .HasIndex(a => new { a.Date, a.StudentId, a.SubjectId })
    .IsUnique();
            // Savol o'chirilganda uning javob variantlari ham o'chsin
            builder.Entity<AnswerOption>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Imtihon o'chirilganda natijalar o'chib ketmasin (tarixni saqlab qolish uchun)
            builder.Entity<ExamResult>()
                .HasOne(r => r.Exam)
                .WithMany(e => e.Results)
                .HasForeignKey(r => r.ExamId)
                .OnDelete(DeleteBehavior.Restrict);
            // Bitta talaba bitta imtihonni faqat bir marta boshlashi mumkin
            builder.Entity<ExamAttempt>()
                .HasIndex(a => new { a.ExamId, a.StudentId })
                .IsUnique();
            // Bitta guruh-fan juftligi faqat bir marta bo'lishi mumkin
            builder.Entity<GroupSubject>()
                .HasIndex(gs => new { gs.GroupId, gs.SubjectId })
                .IsUnique();
        }
    }
}