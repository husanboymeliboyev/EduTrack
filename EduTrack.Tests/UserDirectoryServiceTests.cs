using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class UserDirectoryServiceTests : IClassFixture<UserDirectoryTestFixture>
    {
        private readonly UserDirectoryTestFixture _fixture;

        public UserDirectoryServiceTests(UserDirectoryTestFixture fixture)
        {
            _fixture = fixture;
        }

        private Task<UsersPageViewModel> AdminAsync(UsersFilterViewModel filter)
            => _fixture.Service.GetForAdminAsync(filter);

        private Task<UsersPageViewModel> Teacher1Async(UsersFilterViewModel filter)
            => _fixture.Service.GetForTeacherAsync(_fixture.Seed.Teacher1Id, filter);

        private Task<UsersPageViewModel> Teacher2Async(UsersFilterViewModel filter)
            => _fixture.Service.GetForTeacherAsync(_fixture.Seed.Teacher2Id, filter);

        [Fact]
        public async Task Admin_SearchByName_IsCaseInsensitive_ForAscii()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Search = "aliyev",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("Aliyev Vali", Assert.Single(page.Rows).FullName);
        }

        [Fact]
        public async Task Admin_Search_ByLoginId()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Search = "10003",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("10003", Assert.Single(page.Rows).LoginId);
        }

        [Fact]
        public async Task Admin_Search_ByEmail()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Search = "10004@",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("10004", Assert.Single(page.Rows).LoginId);
        }

        // % oddiy belgi sifatida qidirilishi kerak — aks holda barcha
        // foydalanuvchilar qaytadi.
        [Fact]
        public async Task Admin_Search_EscapesPercentWildcard()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Search = "%",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("100% Real_Student", Assert.Single(page.Rows).FullName);
        }

        // _ ham oddiy belgi sifatida qidirilishi kerak.
        [Fact]
        public async Task Admin_Search_EscapesUnderscoreWildcard()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Search = "_",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("100% Real_Student", Assert.Single(page.Rows).FullName);
        }

        [Fact]
        public async Task Admin_Filter_ByRole()
        {
            var students = await AdminAsync(new UsersFilterViewModel
            {
                Role = "Student",
                PageSize = 100
            });
            var teachers = await AdminAsync(new UsersFilterViewModel
            {
                Role = "Teacher",
                PageSize = 100
            });
            var admins = await AdminAsync(new UsersFilterViewModel
            {
                Role = "Admin",
                PageSize = 100
            });

            Assert.Equal(32, students.TotalCount);
            Assert.All(students.Rows, row => Assert.Equal("Student", row.Role));
            Assert.Equal(2, teachers.TotalCount);
            Assert.Equal(1, admins.TotalCount);
        }

        [Fact]
        public async Task Admin_Filter_ByGroup()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                GroupId = _fixture.Seed.GroupAId,
                PageSize = 100
            });

            Assert.Equal(28, page.TotalCount);
            Assert.All(page.Rows, row => Assert.Equal("Guruh A", row.GroupName));
        }

        [Fact]
        public async Task Admin_Filter_Combined_SearchAndGroup()
        {
            var matching = await AdminAsync(new UsersFilterViewModel
            {
                Search = "aliyev",
                GroupId = _fixture.Seed.GroupAId,
                PageSize = 100
            });
            var otherGroup = await AdminAsync(new UsersFilterViewModel
            {
                Search = "aliyev",
                GroupId = _fixture.Seed.GroupBId,
                PageSize = 100
            });

            Assert.Equal(1, matching.TotalCount);
            Assert.Equal(0, otherGroup.TotalCount);
        }

        [Fact]
        public async Task Admin_Sort_ByNameAsc_IsCaseInsensitive()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Sort = UsersSort.NameAsc,
                PageSize = 100
            });

            var names = page.Rows.Select(r => r.FullName).ToList();
            var lowercaseIndex = names.IndexOf("abdulaziz Karimov");
            var uppercaseIndex = names.IndexOf("Aliyev Vali");

            // Case-sensitive BINARY tartibda "Aliyev" "abdulaziz" dan oldin kelardi.
            Assert.True(lowercaseIndex < uppercaseIndex);
            Assert.Equal("100% Real_Student", names[0]);
        }

        [Fact]
        public async Task Admin_Sort_ByNameDesc()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Sort = UsersSort.NameDesc,
                PageSize = 100
            });

            var names = page.Rows.Select(r => r.FullName).ToList();
            var lowercaseIndex = names.IndexOf("abdulaziz Karimov");
            var uppercaseIndex = names.IndexOf("Aliyev Vali");

            Assert.True(lowercaseIndex > uppercaseIndex);
        }

        [Fact]
        public async Task Admin_Sort_ByLoginAsc()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                Sort = UsersSort.LoginAsc,
                PageSize = 100
            });

            var logins = page.Rows.Select(r => r.LoginId).ToList();
            var expected = logins.OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.Equal(expected, logins);
        }

        [Fact]
        public async Task Admin_Pagination_SplitsRowsAndKeepsTotal()
        {
            var first = await AdminAsync(new UsersFilterViewModel
            {
                GroupId = _fixture.Seed.GroupAId,
                Page = 1,
                PageSize = 20
            });
            var second = await AdminAsync(new UsersFilterViewModel
            {
                GroupId = _fixture.Seed.GroupAId,
                Page = 2,
                PageSize = 20
            });

            Assert.Equal(28, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
            Assert.Equal(20, first.Rows.Count);
            Assert.Equal(1, first.Page);

            Assert.Equal(8, second.Rows.Count);
            Assert.Equal(2, second.Page);

            var firstIds = first.Rows.Select(r => r.Id).ToHashSet();
            var secondIds = second.Rows.Select(r => r.Id).ToHashSet();

            Assert.Empty(firstIds.Intersect(secondIds));
        }

        [Fact]
        public async Task Admin_PageBeyondRange_ClampsToLastPage()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                GroupId = _fixture.Seed.GroupAId,
                Page = 99,
                PageSize = 20
            });

            Assert.Equal(2, page.Page);
            Assert.Equal(8, page.Rows.Count);
        }

        [Fact]
        public async Task Admin_InvalidPageSize_FallsBackToDefault()
        {
            var page = await AdminAsync(new UsersFilterViewModel
            {
                PageSize = 7
            });

            Assert.Equal(UsersFilterViewModel.DefaultPageSize, page.PageSize);
        }

        [Fact]
        public async Task Admin_EmptyFilter_ReturnsAllUsers()
        {
            var page = await AdminAsync(new UsersFilterViewModel());

            Assert.Equal(35, page.TotalCount);
            Assert.Equal(20, page.Rows.Count);
        }

        [Fact]
        public async Task Admin_ProvidesRoleAndGroupOptions()
        {
            var page = await AdminAsync(new UsersFilterViewModel());

            Assert.Equal(new[] { "Admin", "Student", "Teacher" }, page.Roles);
            Assert.Equal(3, page.Groups.Count);
            Assert.False(page.IsTeacherScope);
        }

        [Fact]
        public async Task Teacher_SeesOnlyStudentsFromOwnGroups()
        {
            var page = await Teacher1Async(new UsersFilterViewModel
            {
                PageSize = 100
            });

            Assert.Equal(31, page.TotalCount);
            Assert.True(page.IsTeacherScope);
            Assert.DoesNotContain(page.Rows, row => row.FullName == "Zero Savol");
            Assert.All(page.Rows, row => Assert.Contains(
                row.GroupName,
                new[] { "Guruh A", "Guruh B" }));
            Assert.All(page.Rows, row => Assert.Equal("Student", row.Role));
        }

        [Fact]
        public async Task Teacher_CannotSeeOtherTeachersStudent_BySearch()
        {
            var page = await Teacher1Async(new UsersFilterViewModel
            {
                Search = "Zero",
                PageSize = 100
            });

            Assert.Equal(0, page.TotalCount);
        }

        [Fact]
        public async Task Teacher_GroupOptions_LimitedToOwnGroups()
        {
            var teacher1 = await Teacher1Async(new UsersFilterViewModel());
            var teacher2 = await Teacher2Async(new UsersFilterViewModel());

            Assert.Equal(new[] { "Guruh A", "Guruh B" }, teacher1.Groups.Select(g => g.Name));
            Assert.Equal(new[] { "Guruh C" }, teacher2.Groups.Select(g => g.Name));
            Assert.Empty(teacher1.Roles);
        }

        [Fact]
        public async Task Teacher_Filter_ByOwnGroup()
        {
            var page = await Teacher1Async(new UsersFilterViewModel
            {
                GroupId = _fixture.Seed.GroupAId,
                PageSize = 100
            });

            Assert.Equal(28, page.TotalCount);
            Assert.All(page.Rows, row => Assert.Equal("Guruh A", row.GroupName));
        }

        [Fact]
        public async Task Teacher_Filter_ByForeignGroup_ReturnsEmpty()
        {
            var page = await Teacher1Async(new UsersFilterViewModel
            {
                GroupId = _fixture.Seed.GroupCId,
                PageSize = 100
            });

            Assert.Equal(0, page.TotalCount);
        }

        [Fact]
        public async Task Teacher_Search_DoesNotMatchNonStudents()
        {
            var page = await Teacher1Async(new UsersFilterViewModel
            {
                Search = "Teacher One",
                PageSize = 100
            });

            Assert.Equal(0, page.TotalCount);
        }

        [Fact]
        public async Task Teacher2_SeesOnlyGroupCStudent()
        {
            var page = await Teacher2Async(new UsersFilterViewModel
            {
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("Zero Savol", Assert.Single(page.Rows).FullName);
        }

        [Fact]
        public async Task Teacher_Pagination_Works()
        {
            var first = await Teacher1Async(new UsersFilterViewModel
            {
                Page = 1,
                PageSize = 20
            });
            var second = await Teacher1Async(new UsersFilterViewModel
            {
                Page = 2,
                PageSize = 20
            });

            Assert.Equal(31, first.TotalCount);
            Assert.Equal(2, first.TotalPages);
            Assert.Equal(20, first.Rows.Count);
            Assert.Equal(11, second.Rows.Count);
        }

        [Fact]
        public async Task Teacher_Search_WithinOwnGroups()
        {
            var page = await Teacher1Async(new UsersFilterViewModel
            {
                Search = "Aliyev",
                PageSize = 100
            });

            Assert.Equal(1, page.TotalCount);
            Assert.Equal("Aliyev Vali", Assert.Single(page.Rows).FullName);
        }
    }
}
