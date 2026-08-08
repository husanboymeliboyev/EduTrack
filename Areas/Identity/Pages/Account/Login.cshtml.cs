#nullable disable

using System.ComponentModel.DataAnnotations;
using EduTrack.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduTrack.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Login ID kiriting")]
            [Display(Name = "Login ID")]
            public string LoginId { get; set; }

            [Required(ErrorMessage = "Parolni kiriting")]
            [DataType(DataType.Password)]
            [Display(Name = "Parol")]
            public string Password { get; set; }

            [Display(Name = "Meni eslab qol")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    Input.LoginId, Input.Password, Input.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Foydalanuvchi tizimga kirdi.");
                    return LocalRedirect(returnUrl);
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Hisob vaqtincha bloklandi.");
                    ModelState.AddModelError(string.Empty, "Hisobingiz vaqtincha bloklangan. Birozdan so'ng qayta urinib ko'ring.");
                    return Page();
                }

                ModelState.AddModelError(string.Empty, "Login ID yoki parol noto'g'ri.");
                return Page();
            }

            return Page();
        }
    }
}