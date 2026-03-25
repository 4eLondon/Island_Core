using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace IslandCore.Pages;

public class RegistrationModel(DataService data) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? StatusMessage { get; set; }
    public bool IsSuccess { get; set; }

    public readonly string[] Parishes =
    [
        "St.Ann","St.Mary","Portland","St.Thomas","St.Andrew","Kingston",
        "Clarendon","Manchester","St.Elizabeth","Westmoreland","Hanover",
        "St.James","Trelawny","St.Catherine"
    ];

    public class InputModel
    {
        [Required] public string FirstName  { get; set; } = "";
        [Required] public string LastName   { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        public string Telephone { get; set; } = "";
        [Required] public string City       { get; set; } = "";
        [Required] public string Gender     { get; set; } = "";
        [Required] public string Username   { get; set; } = "";
        [Required, MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = "";
        [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var (ok, error) = data.CreateUser(new UserRecord(
                Input.Username, Input.Email, Input.FirstName, Input.LastName,
                Input.Telephone, Input.City, Input.Gender, Input.Password,
                DateTime.UtcNow));

            if (!ok) { StatusMessage = error; return Page(); }

            IsSuccess = true;
            StatusMessage = "Registration successful! You can now sign in.";
            ModelState.Clear();
            Input = new();
            return Page();
        }
        catch (Exception ex) when (ex.Message.Contains("Timeout") || ex.Message.Contains("route") || ex.Message.Contains("stream"))
        {
            StatusMessage = "Unable to reach the database. Please try again in a moment.";
            return Page();
        }
        catch (Exception)
        {
            StatusMessage = "An unexpected error occurred. Please try again.";
            return Page();
        }
    }
}
