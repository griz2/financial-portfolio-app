using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Api.Models;
using Portfolio.Core.Authentication;

namespace Portfolio.Api.Controllers
{
    [ApiController]
    [AutoValidateAntiforgeryToken]
    [Route("api/auth")]
    [ResponseCache(NoStore = true,
                Location = ResponseCacheLocation.None)]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly IAntiforgery _antiforgery;

        public AuthController(IAuthenticationService authenticationService, IAntiforgery antiforgery)
        {
            _authenticationService = authenticationService;
            _antiforgery = antiforgery;
        }

        [AllowAnonymous]
        [HttpGet("csrf")]
        public IActionResult GetCsrfToken()
        {
            AntiforgeryTokenSet tokens = _antiforgery.GetAndStoreTokens(HttpContext);

            Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions()
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });

            return Ok(new { token = tokens.RequestToken });
        }

        [AllowAnonymous]
        [EnableRateLimiting("authentication")]
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            RegistrationResult result = await _authenticationService.Register(request.Email, request.Password);

            if (!result.Succeeded)
            {
                foreach (string error in result.Errors)
                {
                    ModelState.AddModelError("registration", error);
                }

                return ValidationProblem(ModelState);
            }

            return StatusCode(StatusCodes.Status201Created, new { message = "Registration complete. Please log in." });
        }

        [AllowAnonymous]
        [EnableRateLimiting("authentication")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            bool succeeded = await _authenticationService.Login(request.Email, request.Password);

            if (!succeeded)
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized,
                    title: "Login failed.", detail: "Check your credentials or try again later.");
            }

            return NoContent();
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _authenticationService.Logout();
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserDetails>> GetCurrentUser()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            UserDetails? user = userId == null ? null : await _authenticationService.GetCurrentUser(userId);

            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(user);
        }
    }
}
