using System.ComponentModel.DataAnnotations;

namespace weblamchoi.Models
{
    public class Admin
    {
        public int AdminID { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string PasswordHash { get; set; }

        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


}
