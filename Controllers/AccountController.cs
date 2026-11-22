using System.Text.Encodings.Web;
using DemoMVC.Models;
using DemoMVC.Models.Account;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers;

[Authorize]
public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IStudentEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IStudentEmailSender emailSender,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = new ApplicationUser
        {
            UserName = input.Email.Trim(),
            Email = input.Email.Trim()
        };
        var result = await _userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(input);
        }

        try
        {
            await SendConfirmationEmailAsync(user);
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogError(exception, "Could not send confirmation email to {Email}.", user.Email);
            TempData["EmailDeliveryError"] =
                "Tài khoản đã được tạo nhưng email xác thực chưa gửi được. Hãy kiểm tra cấu hình email hoặc thử gửi lại.";
        }

        TempData["RegisterEmail"] = user.Email;
        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterConfirmation()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return View("ConfirmEmail", false);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return View("ConfirmEmail", false);
        }

        var result = await _userManager.ConfirmEmailAsync(user, code);
        return View("ConfirmEmail", result.Succeeded);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResendConfirmation()
    {
        return View(new ResendConfirmationViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user is not null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                await SendConfirmationEmailAsync(user);
            }
            catch (EmailDeliveryException exception)
            {
                _logger.LogError(
                    exception,
                    "Could not resend confirmation email to {Email}.",
                    user.Email);
                ModelState.AddModelError(
                    string.Empty,
                    exception.Message);
                return View(input);
            }
        }

        ViewData["StatusMessage"] =
            "Nếu địa chỉ email tồn tại và chưa xác thực, hướng dẫn xác thực đã được gửi.";
        return View(new ResendConfirmationViewModel());
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await _signInManager.PasswordSignInAsync(
            input.Email,
            input.Password,
            input.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(
                Url.IsLocalUrl(input.ReturnUrl)
                    ? input.ReturnUrl!
                    : Url.Action("Index", "Student")!);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm khóa do đăng nhập sai nhiều lần.");
        }
        else if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                string.Empty,
                "Vui lòng xác thực email trước khi đăng nhập.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
        }

        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Student");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { email = user.Email, code = token },
                Request.Scheme);

            try
            {
                await _emailSender.SendAsync(
                    user.Email!,
                    "Đặt lại mật khẩu",
                    $"<p>Để đặt lại mật khẩu, hãy <a href=\"{HtmlEncoder.Default.Encode(callbackUrl!)}\">nhấn vào liên kết này</a>.</p>");
            }
            catch (EmailDeliveryException exception)
            {
                _logger.LogError(exception, "Could not send password reset email to {Email}.", user.Email);
                TempData["EmailDeliveryError"] = exception.Message;
            }
        }

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? email, string? code)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("Liên kết đặt lại mật khẩu không hợp lệ.");
        }

        return View(new ResetPasswordViewModel { Email = email, Code = code });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = await _userManager.FindByEmailAsync(input.Email);
        if (user is null)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await _userManager.ResetPasswordAsync(user, input.Code, input.Password);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        AddIdentityErrors(result);
        return View(input);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackUrl = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, code = token },
            Request.Scheme);

        await _emailSender.SendAsync(
            user.Email!,
            "Xác thực tài khoản sinh viên",
            $"<p>Để xác thực tài khoản, hãy <a href=\"{HtmlEncoder.Default.Encode(callbackUrl!)}\">nhấn vào liên kết này</a>.</p>");
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            var message = error.Code switch
            {
                "DuplicateUserName" or "DuplicateEmail" => "Email này đã được đăng ký.",
                "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
                "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
                "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
                "PasswordRequiresUpper" => "Mật khẩu phải có ít nhất một chữ hoa.",
                "PasswordRequiresNonAlphanumeric" => "Mật khẩu phải có ít nhất một ký tự đặc biệt.",
                _ => error.Description
            };
            ModelState.AddModelError(string.Empty, message);
        }
    }
}
