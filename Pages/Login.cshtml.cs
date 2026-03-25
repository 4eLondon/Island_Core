using IslandCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace IslandCore.Pages;

public class LoginModel(DataService data) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("Username") != null)
            return RedirectToPage("/Dashboard");
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var user = data.ValidateLogin(Input.Username, Input.Password);
            if (user is null)
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            HttpContext.Session.SetString("Username",  user.Username);
            HttpContext.Session.SetString("FirstName", user.FirstName);
            HttpContext.Session.SetString("LastName",  user.LastName);
            return RedirectToPage("/Dashboard");
        }
        catch (Exception ex) when (ex.Message.Contains("Timeout") || ex.Message.Contains("route") || ex.Message.Contains("stream"))
        {
            ErrorMessage = "Unable to reach the database. Please try again in a moment.";
            return Page();
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred. Please try again.";
            return Page();
        }
    }
}
