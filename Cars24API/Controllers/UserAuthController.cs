using Microsoft.AspNetCore.Mvc;
using Cars24API.Models;
using Cars24API.Services;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;

namespace Cars24API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserAuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ReferralService _referralService;
    public UserAuthController(UserService userService, ReferralService referralService)
    {
        _userService = userService;
        _referralService = referralService;
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null)
            return NotFound("User not found.");

        return Ok(user);
    }
    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name, email and password are required." });

        var existingUser = await _userService.GetByEmailAsync(request.Email);
        if (existingUser != null)
            return BadRequest(new { message = "User already exists." });

        string? referralCode = string.IsNullOrWhiteSpace(request.ReferralCode)
            ? null
            : request.ReferralCode.Trim().ToUpperInvariant();

        User? referrer = null;
        if (referralCode != null)
        {
            referrer = await _referralService.GetUserByReferralCodeAsync(referralCode);
            if (referrer == null)
                return BadRequest(new { message = "Invalid referral code." });
        }

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Phone = request.Phone ?? string.Empty,
            ReferralCode = await _referralService.GenerateUniqueReferralCodeAsync()
        };
        await _userService.CreateAsync(user);

        if (referrer != null && referrer.Id != user.Id)
        {
            await _referralService.AttachAsync(user.Id!, referralCode!, referrer.Id!);
        }

        return Ok(new
        {
            message = "Signup successful",
            user = new
            {
                id = user.Id, // MongoDB-generated ObjectId
                fullName = user.FullName,
                email = user.Email,
                phone = user.Phone,
                referralCode = user.ReferralCode
            }
        });
    }

    public class SignupRequest
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Phone]
        public string? Phone { get; set; }

        public string? ReferralCode { get; set; }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userService.GetByEmailAsync(request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized(new { message = "Invalid credentials" });

        return Ok(new
        {
            message = "Login successful",
            user = new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email,
                phone = user.Phone
            }
        });
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
