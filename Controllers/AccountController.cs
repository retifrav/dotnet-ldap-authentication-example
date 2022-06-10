using System.DirectoryServices.Protocols;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using decovar.dev.Code;
using decovar.dev.Models;

namespace decovar.dev.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly ConfigurationAD _configurationAD;
    private readonly ILogger _logger;
    private readonly ISignInManager _signInManager;

    public AccountController(
        IOptions<ConfigurationAD> configurationAD,
        ILogger<AccountController> logger,
        ISignInManager signInManager
        )
    {
        _configurationAD = configurationAD.Value;
        _logger = logger;
        _signInManager = signInManager;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("Index", "Account");
            }
            else { return Redirect(returnUrl); }
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        string defaultErrorMsg = new StringBuilder()
            .Append("Couldn't authenticate you, the credentials that you've provided ")
            .Append("are not correct, or your account doesn't belong to the authorized group")
            .ToString();
        string specificErrorMsg = string.Empty;

        if (ModelState.IsValid)
        {
            try
            {
                var signingInSuccessful = await _signInManager.SignIn(model.Username, model.Password);
                if (signingInSuccessful)
                {
                    if (string.IsNullOrEmpty(returnUrl)) { returnUrl = "/"; }
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    // most probably the request to AD has succeeded, and even user user credentials
                    // are correct, but the actual search results were empty. Perhaps, user
                    // isn't memeber of the crew group. Or search query attributes should
                    // be different for this particular LDAP server
                }
            }
            catch (DirectoryOperationException ex)
            {
                string errorMsg = "AD server returned an error";
                specificErrorMsg = $"Couldn't authenticate you, {errorMsg}";
                _logger.LogError($"{errorMsg}: {ex.Message}");
            }
            catch (Exception ex) // or perhaps catch LdapException (credentials) first
            {
                _logger.LogWarning(
                    new StringBuilder()
                        .Append($"Couldn't authenticate user [{model.Username}] with provided ")
                        .Append($"credentials. {ex.Message}")
                        .ToString()
                );
            }
        }

        //return Unauthorized();
        ModelState.AddModelError(
            "Error",
            string.IsNullOrEmpty(specificErrorMsg) ? defaultErrorMsg : specificErrorMsg
        );
        ViewData["ReturnUrl"] = returnUrl;
        return View(model);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _signInManager.SignOut();
            return RedirectToAction(nameof(AccountController.Login));
        }
        catch(Exception ex)
        {
            _logger.LogError($"Signing-out failed: {ex.Message}");
            return StatusCode(500);
        }
    }

    [Route("access-denied")]
    public IActionResult AccessDenied(string ReturnUrl)
    {
        Response.StatusCode = 403;
        return View();
    }
}
