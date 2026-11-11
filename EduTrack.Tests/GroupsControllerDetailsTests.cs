using EduTrack.Controllers;
using EduTrack.Models;
using EduTrack.Tests.Infrastructure;
using EduTrack.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EduTrack.Tests
{
    // Guruhlar -> "O'quvchilar" sahifasi (GroupsController.Details) uchun
    // so'rov SQLite'da to'g'ri tarjima bo'lishini va tarkibni to'g'ri
    // qaytarishini tekshiradi.
    public class GroupsControllerDetailsTests : IClassFixture<UserDirectoryTestFixture>
    {
        private readonly UserDirectoryTestFixture _fixture;

        public GroupsControllerDetailsTests(UserDirectoryTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Details_ReturnsGroupMembers()
        {
            var controller = new GroupsController(_fixture.Db.Context);

            var result = await controller.Details(_fixture.Seed.GroupAId);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GroupDetailsViewModel>(view.Model);

            Assert.Equal("Guruh A", model.Group.Name);
            Assert.Equal(28, model.Members.Count);
            Assert.All(model.Members, m =>
            {
                Assert.Equal("Guruh A", m.StreamName);
                Assert.Null(m.ClassName);
                Assert.False(string.IsNullOrWhiteSpace(m.LoginId));
            });
        }

        [Fact]
        public async Task Details_MissingGroup_ReturnsNotFound()
        {
            var controller = new GroupsController(_fixture.Db.Context);

            var result = await controller.Details(999999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_WithoutId_ReturnsNotFound()
        {
            var controller = new GroupsController(_fixture.Db.Context);

            var result = await controller.Details(null);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
