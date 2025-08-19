using System.ComponentModel.DataAnnotations;

namespace weblamchoi.Models
{
    public class RegisterViewModel
    {
        [Required]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string Phone { get; set; }

        public string Address { get; set; }
    }
}
