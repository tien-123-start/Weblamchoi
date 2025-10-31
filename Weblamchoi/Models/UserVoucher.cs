using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace weblamchoi.Models
{
    [Table("UserVouchers")]
    public class UserVoucher
    {
        [Key]
        public int Id { get; set; }

        public int UserID { get; set; }
        [ForeignKey("UserID")]
        public User User { get; set; }

        public int VoucherID { get; set; }
        [ForeignKey("VoucherID")]
        public Voucher Voucher { get; set; }

        public DateTime UsedAt { get; set; } = DateTime.Now;
    }

}
