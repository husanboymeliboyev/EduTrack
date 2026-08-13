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
        public DbSet<GradeComponent> GradeComponents { get; set; }
        public DbSet<StudentGrade> StudentGrades { get; set; }
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
            // Topshiriq guruhga tegishli bo'lishi mumkin (null = barcha guruhlarga ko'rinadi)
            builder.Entity<Assignment>()
                .HasOne(a => a.Group)
                .WithMany()
                .HasForeignKey(a => a.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // Imtihon ham xuddi shunday
            builder.Entity<Exam>()
                .HasOne(e => e.Group)
                .WithMany()
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
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

            // Fan o'chirilganda uning baholash komponentlari ham o'chsin
            builder.Entity<GradeComponent>()
                .HasOne(c => c.Subject)
                .WithMany()
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Komponent o'chirilganda unga tegishli baholar ham o'chsin
            builder.Entity<StudentGrade>()
                .HasOne(g => g.GradeComponent)
                .WithMany(c => c.Grades)
                .HasForeignKey(g => g.GradeComponentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bitta talaba bitta komponent bo'yicha faqat bitta baho yozuviga ega bo'ladi
            builder.Entity<StudentGrade>()
                .HasIndex(g => new { g.GradeComponentId, g.StudentId })
                .IsUnique();

            // Komponent aniq bitta Topshiriqqa bog'lanishi mumkin
            builder.Entity<GradeComponent>()
                .HasOne(c => c.Assignment)
                .WithMany()
                .HasForeignKey(c => c.AssignmentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Komponent aniq bitta Imtihonga bog'lanishi mumkin
            builder.Entity<GradeComponent>()
                .HasOne(c => c.Exam)
                .WithMany()
                .HasForeignKey(c => c.ExamId)
                .OnDelete(DeleteBehavior.SetNull);

            // Bitta Topshiriq faqat bitta komponentga bog'lanishi mumkin
            builder.Entity<GradeComponent>()
                .HasIndex(c => c.AssignmentId)
                .IsUnique()
                .HasFilter("AssignmentId IS NOT NULL");

            // Bitta Imtihon faqat bitta komponentga bog'lanishi mumkin
            builder.Entity<GradeComponent>()
                .HasIndex(c => c.ExamId)
                .IsUnique()
                .HasFilter("ExamId IS NOT NULL");

            // Komponent bir vaqtning o'zida ham Topshiriqqa, ham Imtihonga bog'lanmasin
            builder.Entity<GradeComponent>()
                .ToTable(t => t.HasCheckConstraint("CK_GradeComponent_SingleLink", "AssignmentId IS NULL OR ExamId IS NULL"));
        }
    
    }
}