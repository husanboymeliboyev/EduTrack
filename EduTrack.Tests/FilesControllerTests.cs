using System.Security.Claims;
using EduTrack.Controllers;
using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using EduTrack.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTrack.Tests
{
    // Fayl yuklab olish ruxsatlari: fanni o'qitadigan HAR QANDAY o'qituvchi
    // materialni ocha olishi kerak (avval faqat Subject.TeacherId ishlatilgani
    // uchun ikkinchi o'qituvchi AccessDenied olardi).
    public class FilesControllerTests : IAsyncLifetime
    {
        private const string SubjectTeacherId = "t-owner";
        private const string CoTeacherId = "t-co";
        private const string OtherTeacherId = "t-other";
        private const string StudentId = "s-1";
        private const string OtherStudentId = "s-2";

        private TestDbFactory _db = null!;
        private FakeFileUploadService _files = null!;
        private string _tempDir = null!;

        private int _subjectId;
        private int _otherSubjectId;
        private int _groupId;
        private int _assignmentId;
        private int _otherAssignmentId;
        private int _submissionId;

        public async Task InitializeAsync()
        {
            _db = await TestDbFactory.CreateAsync();
            _tempDir = Path.Combine(Path.GetTempPath(), "edutrack-files-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _files = new FakeFileUploadService(_tempDir);

            var db = _db.Context;

            db.Users.AddRange(
                NewUser(SubjectTeacherId, "10041"),
                NewUser(CoTeacherId, "10042"),
                NewUser(OtherTeacherId, "10043"),
                NewUser(StudentId, "10501"),
                NewUser(OtherStudentId, "10502"));

            var group = new Group { Name = "3-guruh", Type = GroupType.Stream };
            db.Groups.Add(group);
            await db.SaveChangesAsync();

            var subject = new Subject { Name = "Matematika", TeacherId = SubjectTeacherId };
            var otherSubject = new Subject { Name = "Fizika", TeacherId = OtherTeacherId };
            db.Subjects.AddRange(subject, otherSubject);
            await db.SaveChangesAsync();

            // Fan egasi boshqa, lekin bu guruhda darsni "t-co" olib boradi.
            db.GroupSubjects.Add(new GroupSubject
            {
                GroupId = group.Id,
                SubjectId = subject.Id,
                TeacherId = CoTeacherId
            });
            db.GroupMemberships.Add(new GroupMembership
            {
                StudentId = StudentId,
                GroupId = group.Id,
                Scope = GroupType.Stream,
                StartDate = new DateTime(2026, 9, 1),
                IsActive = true
            });
            await db.SaveChangesAsync();

            var assignment = new Assignment
            {
                Title = "1-topshiriq",
                DueDate = new DateTime(2026, 12, 1, 10, 0, 0),
                SubjectId = subject.Id,
                GroupId = group.Id,
                FilePath = "uploads/darslik.pdf",
                FileName = "darslik.pdf"
            };
            db.Assignments.Add(assignment);

            db.Assignments.Add(new Assignment
            {
                Title = "Boshqa fan",
                DueDate = new DateTime(2026, 12, 1, 10, 0, 0),
                SubjectId = otherSubject.Id,
                FilePath = "uploads/fizika.pdf",
                FileName = "fizika.pdf"
            });
            await db.SaveChangesAsync();

            var submission = new Submission
            {
                AssignmentId = assignment.Id,
                StudentId = StudentId,
                SubmittedDate = new DateTime(2026, 11, 19, 10, 0, 0),
                FilePath = "uploads/ish.cpp",
                FileName = "ish.cpp"
            };
            db.Submissions.Add(submission);
            await db.SaveChangesAsync();

            _subjectId = subject.Id;
            _otherSubjectId = otherSubject.Id;
            _groupId = group.Id;
            _assignmentId = assignment.Id;
            _otherAssignmentId = db.Assignments.Single(a => a.Title == "Boshqa fan").Id;
            _submissionId = submission.Id;

            _files.Add("uploads/darslik.pdf", "PDF"u8.ToArray());
            _files.Add("uploads/fizika.pdf", "FIZIKA"u8.ToArray());
            _files.Add("uploads/ish.cpp", "int main(){}"u8.ToArray());
        }

        public async Task DisposeAsync()
        {
            await _db.DisposeAsync();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task Co_teacher_of_the_subject_can_download_material()
        {
            var controller = CreateController(CoTeacherId, "Teacher");

            var result = await controller.Assignment(_assignmentId);

            var file = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal("darslik.pdf", file.FileDownloadName);
        }

        [Fact]
        public async Task Subject_owner_can_download_material()
        {
            var controller = CreateController(SubjectTeacherId, "Teacher");

            var result = await controller.Assignment(_assignmentId);

            Assert.IsType<PhysicalFileResult>(result);
        }

        [Fact]
        public async Task Teacher_of_another_subject_is_forbidden()
        {
            var controller = CreateController(OtherTeacherId, "Teacher");

            var result = await controller.Assignment(_assignmentId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Student_of_the_group_can_download_material()
        {
            var controller = CreateController(StudentId, "Student");

            var result = await controller.Assignment(_assignmentId);

            Assert.IsType<PhysicalFileResult>(result);
        }

        [Fact]
        public async Task Student_of_another_subject_is_forbidden()
        {
            var controller = CreateController(StudentId, "Student");

            var result = await controller.Assignment(_otherAssignmentId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Teacher_of_the_subject_can_download_student_submission()
        {
            var controller = CreateController(CoTeacherId, "Teacher");

            var result = await controller.Submission(_submissionId);

            var file = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal("ish.cpp", file.FileDownloadName);
        }

        [Fact]
        public async Task Submission_owner_can_download_own_file()
        {
            var controller = CreateController(StudentId, "Student");

            var result = await controller.Submission(_submissionId);

            Assert.IsType<PhysicalFileResult>(result);
        }

        [Fact]
        public async Task Another_student_cannot_download_submission()
        {
            var controller = CreateController(OtherStudentId, "Student");

            var result = await controller.Submission(_submissionId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Missing_file_returns_not_found()
        {
            _files.Remove("uploads/darslik.pdf");
            var controller = CreateController(SubjectTeacherId, "Teacher");

            var result = await controller.Assignment(_assignmentId);

            Assert.IsType<NotFoundResult>(result);
        }

        private FilesController CreateController(string userId, string role)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId),
                new Claim(ClaimTypes.Role, role)
            }, "Test"));

            return new FilesController(
                _db.Context,
                CreateUserManager(_db.Context),
                _files,
                new FakeStudentSubjectGroupService(_subjectId, _groupId),
                new TeacherAccessService(_db.Context))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = principal }
                }
            };
        }

        private static ApplicationUser NewUser(string id, string loginId) => new()
        {
            Id = id,
            UserName = loginId,
            NormalizedUserName = loginId.ToUpperInvariant(),
            LoginId = loginId,
            FullName = loginId,
            EmailConfirmed = true,
            MustChangePassword = false
        };

        private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
        {
            var store = new UserStore<ApplicationUser>(db, new IdentityErrorDescriber());
            var options = Options.Create(new IdentityOptions());

            return new UserManager<ApplicationUser>(
                store,
                options,
                new PasswordHasher<ApplicationUser>(),
                new IUserValidator<ApplicationUser>[] { new UserValidator<ApplicationUser>() },
                new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<ApplicationUser>>.Instance);
        }

        private sealed class FakeStudentSubjectGroupService : IStudentSubjectGroupService
        {
            private readonly int _subjectId;
            private readonly int _groupId;

            public FakeStudentSubjectGroupService(int subjectId, int groupId)
            {
                _subjectId = subjectId;
                _groupId = groupId;
            }

            public Task<IReadOnlyList<int>> GetSubjectIdsAsync(string studentId, DateTime? onDate = null, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<int>>(new[] { _subjectId });

            public Task<int?> GetGroupIdAsync(string studentId, int subjectId, DateTime? onDate = null, CancellationToken cancellationToken = default)
                => Task.FromResult<int?>(subjectId == _subjectId ? _groupId : null);

            public Task<int?> GetDefaultGroupIdAsync(string studentId, CancellationToken cancellationToken = default)
                => Task.FromResult<int?>(_groupId);

            public Task<IReadOnlyDictionary<int, int>> GetGroupMapAsync(string studentId, IEnumerable<int> subjectIds, DateTime? onDate = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<IReadOnlyList<StudentGroupSubject>> GetAssignmentsAsync(string studentId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<IReadOnlyList<string>> GetStudentIdsAsync(int groupId, int subjectId, DateTime? onDate = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<bool> IsStudentInGroupSubjectAsync(string studentId, int groupId, int subjectId, DateTime? onDate = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<StudentGroupSubject?> SetGroupAsync(string studentId, int subjectId, int groupId, DateTime? startDate = null, string? reason = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<bool> ResetToDefaultAsync(string studentId, int subjectId, DateTime? startDate = null, string? reason = null, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        private sealed class FakeFileUploadService : IFileUploadService
        {
            private readonly string _root;
            private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

            public FakeFileUploadService(string root)
            {
                _root = root;
            }

            public void Add(string relativePath, byte[] content)
            {
                var physical = Path.Combine(_root, Path.GetFileName(relativePath));
                File.WriteAllBytes(physical, content);
                _paths[relativePath] = physical;
            }

            public void Remove(string relativePath)
            {
                if (_paths.Remove(relativePath, out var physical) && File.Exists(physical))
                {
                    File.Delete(physical);
                }
            }

            public string? ResolvePath(string? relativePath)
            {
                if (relativePath == null) return null;

                var normalized = relativePath.Replace('\\', '/');
                if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized["uploads/".Length..];
                }

                return _paths.TryGetValue(relativePath, out var physical) && File.Exists(physical)
                    ? physical
                    : null;
            }

            public bool Exists(string? relativePath) => ResolvePath(relativePath) != null;

            public Task<FileUploadResult> UploadAsync(IFormFile? file, long maxSizeBytes)
                => throw new NotImplementedException();

            public Task DeleteAsync(string? relativePath)
            {
                Remove(relativePath ?? string.Empty);
                return Task.CompletedTask;
            }

            public string GetContentType(string fileName) => "application/octet-stream";
        }
    }
}
