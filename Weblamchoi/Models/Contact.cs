using System;
using System.ComponentModel.DataAnnotations;

namespace weblamchoi.Models
{
    public class Contact
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung")]
        [StringLength(1000)]
        public string Message { get; set; }

        public DateTime SubmittedAt { get; set; }
    }
}