using System.ComponentModel.DataAnnotations;

namespace weblamchoi.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Họ tên bắt buộc nhập")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email bắt buộc nhập")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu bắt buộc nhập")]
        [MinLength(8, ErrorMessage = "Mật khẩu phải ít nhất 8 ký tự")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$",
            ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ cái và 1 số")]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Xác nhận mật khẩu không khớp")]
        public string ConfirmPassword { get; set; }

        public string Phone { get; set; }
        public string Address { get; set; }
    }
}
