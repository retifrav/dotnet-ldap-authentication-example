using System.ComponentModel.DataAnnotations;

namespace decovar.dev.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "[{0}] required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "[{0}] required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
