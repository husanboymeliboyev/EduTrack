using System.Net.Mime;
using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Controllers;

[Authorize(Roles = "Admin,Teacher,Student")]
public sealed class FilesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITeacherAccessService _teacherAccessService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public FilesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ITeacherAccessService teacherAccessService,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _teacherAccessService = teacherAccessService;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Assignment(int id)
    {
        var assignment = await _context.Assignments
            .Include(a => a.Subject)
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assignment == null || string.IsNullOrWhiteSpace(assignment.FilePath))
        {
            return NotFound();
        }

        if (!await CanAccessAssignmentAsync(assignment))
        {
            return NotFound();
        }

        return ServeStoredFile(assignment.FilePath, assignment.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Submission(int id)
    {
        var submission = await _context.Submissions
            .Include(s => s.Assignment)
                .ThenInclude(a => a!.Subject)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (submission == null || string.IsNullOrWhiteSpace(submission.FilePath))
        {
            return NotFound();
        }

        if (!await CanAccessSubmissionAsync(submission))
        {
            return NotFound();
        }

        return ServeStoredFile(submission.FilePath, submission.FileName);
    }

    private async Task<bool> CanAccessAssignmentAsync(Assignment assignment)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null || assignment.Subject == null)
        {
            return false;
        }

        if (User.IsInRole("Teacher"))
        {
            return await _teacherAccessService.OwnsSubjectAsync(user.Id, assignment.SubjectId);
        }

        if (!User.IsInRole("Student") || user.GroupId == null)
        {
            return false;
        }

        var isInAssignmentGroup = assignment.GroupId == null || assignment.GroupId == user.GroupId;
        if (!isInAssignmentGroup)
        {
            return false;
        }

        return await _context.GroupSubjects
            .AnyAsync(gs => gs.GroupId == user.GroupId && gs.SubjectId == assignment.SubjectId);
    }

    private async Task<bool> CanAccessSubmissionAsync(Submission submission)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        var user = await _userManager.GetUserAsync(User);
        var assignment = submission.Assignment;
        if (user == null || assignment?.Subject == null)
        {
            return false;
        }

        if (User.IsInRole("Teacher"))
        {
            return await _teacherAccessService.OwnsSubjectAsync(user.Id, assignment.SubjectId);
        }

        return User.IsInRole("Student") && submission.StudentId == user.Id;
    }

    private IActionResult ServeStoredFile(string relativePath, string? originalFileName)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var configuredDataDirectory = _configuration["EDUTRACK_DATA_DIR"];
        var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
            ? (_environment.IsProduction() ? "/app/data" : null)
            : configuredDataDirectory;
        var candidateRoots = new List<string>();

        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            candidateRoots.Add(Path.Combine(dataDirectory, "uploads"));
        }

        candidateRoots.Add(Path.Combine(_environment.WebRootPath, "uploads"));
        var fullPath = string.Empty;

        foreach (var root in candidateRoots)
        {
            var uploadsRoot = Path.GetFullPath(root);
            var candidatePath = Path.GetFullPath(Path.Combine(uploadsRoot, Path.GetFileName(normalizedRelativePath)));
            if (candidatePath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && System.IO.File.Exists(candidatePath))
            {
                fullPath = candidatePath;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return NotFound();
        }

        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(originalFileName)
            ? fullPath
            : originalFileName);
        var contentType = GetContentType(fullPath);
        Response.Headers.ContentDisposition = new ContentDisposition
        {
            DispositionType = "inline",
            FileName = fileName
        }.ToString();

        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            _ => "application/octet-stream"
        };
    }
}
