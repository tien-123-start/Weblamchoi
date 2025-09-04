using System.ComponentModel.DataAnnotations;

namespace weblamchoi.Models
{
    public class UserProfileViewModel
    {
        public int UserID { get; set; }

        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự")]
        public required string FullName { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public required string Email { get; set; }

        public string? Phone { get; set; } // Nullable, as it's optional
        public string? Address { get; set; } // Nullable, as it's optional
        public int Points { get; set; }

    }
}