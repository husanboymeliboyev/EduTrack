#nullable disable

using System.ComponentModel.DataAnnotations;
using EduTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduTrack.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Joriy parolni kiriting")]
            [DataType(DataType.Password)]
            [Display(Name = "Joriy parol")]
            public string OldPassword { get; set; }

            [Required(ErrorMessage = "Yangi parolni kiriting")]
            [StringLength(100, ErrorMessage = "Parol kamida {2} belgidan iborat bo'lishi kerak", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Yangi parol")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Yangi parolni tasdiqlang")]
            [Compare("NewPassword", ErrorMessage = "Parollar bir-biriga mos emas.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid) return Page();

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Birinchi kirishda parol almashtirish talab qilingan bo'lsa, endi bu belgi olib tashlanadi
            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                await _userManager.UpdateAsync(user);
            }

            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = "Parolingiz muvaffaqiyatli almashtirildi.";
            return RedirectToPage();
        }
    }
}