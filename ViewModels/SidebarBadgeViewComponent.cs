using EduTrack.Data;
using EduTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.ViewComponents
{
    public class SidebarBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SidebarBadgeViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // type = "assignments" (talaba uchun: hali topshirilmagan topshiriqlar)
        // type = "grading" (o'qituvchi uchun: hali baholanmagan ishlar)
        public async Task<IViewComponentResult> InvokeAsync(string type)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null) return Content("");

            int count = 0;

            if (type == "assignments" && user.GroupId != null)
            {
                var subjectIds = await _context.GroupSubjects
                    .Where(gs => gs.GroupId == user.GroupId)
                    .Select(gs => gs.SubjectId)
                    .ToListAsync();

                count = await _context.Assignments
                    .Where(a => subjectIds.Contains(a.SubjectId)
                        && (a.GroupId == null || a.GroupId == user.GroupId)
                        && a.DueDate > DateTime.Now
                        && !_context.Submissions.Any(s => s.AssignmentId == a.Id && s.StudentId == user.Id))
                    .CountAsync();
            }
            else if (type == "grading")
            {
                var subjectIds = await _context.Subjects
                    .Where(s => s.TeacherId == user.Id)
                    .Select(s => s.Id)
                    .ToListAsync();

                count = await _context.Submissions
                    .Include(s => s.Assignment)
                    .Where(s => s.Assignment != null && subjectIds.Contains(s.Assignment.SubjectId) && s.Grade == null)
                    .CountAsync();
            }

            if (count == 0) return Content("");

            return View("Default", count);
        }
    }
}