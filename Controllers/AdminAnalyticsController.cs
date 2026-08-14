using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminAnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAnalyticsService _analyticsService;

        public AdminAnalyticsController(ApplicationDbContext context, IAnalyticsService analyticsService)
        {
            _context = context;
            _analyticsService = analyticsService;
        }

        // Barcha fanlar ro'yxati
        public async Task<IActionResult> Index()
        {
            var summaries = await _analyticsService.GetSubjectSummariesAsync();
            return View(summaries);
        }

        // Bitta fan ichida, guruhlar kesimida statistika
        public async Task<IActionResult> Subject(int subjectId)
        {
            var subject = await _context.Subjects.FindAsync(subjectId);
            if (subject == null) return NotFound();

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;

            var groups = await _analyticsService.GetGroupSummariesForSubjectAsync(subjectId);
            return View(groups);
        }

        // Bitta fan + bitta guruh ichida: imtihonlar statistikasi + talabalar reytingi
        public async Task<IActionResult> Detail(int subjectId, int groupId)
        {
            var subject = await _context.Subjects.FindAsync(subjectId);
            var group = await _context.Groups.FindAsync(groupId);
            if (subject == null || group == null) return NotFound();

            ViewBag.SubjectId = subjectId;
            ViewBag.SubjectName = subject.Name;
            ViewBag.GroupId = groupId;
            ViewBag.GroupName = group.Name;

            ViewBag.Exams = await _analyticsService.GetExamRowsAsync(subjectId, groupId);
            var ranking = await _analyticsService.GetSubjectGroupRankingAsync(subjectId, groupId);

            return View(ranking);
        }
    }
}