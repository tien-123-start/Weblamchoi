using System.ComponentModel.DataAnnotations;

namespace weblamchoi.Models
{
    public class ChangePasswordViewModel
    {
        public int UserID { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (NewPassword != ConfirmPassword)
            {
                yield return new ValidationResult("Mật khẩu xác nhận không khớp.", new[] { nameof(ConfirmPassword) });
            }
        }
    }
}