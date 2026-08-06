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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Talaba o'chirilganda guruh o'chib ketmasligi uchun
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Group)
                .WithMany(g => g.Students)
                .HasForeignKey(u => u.GroupId)
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
        }
    }
}