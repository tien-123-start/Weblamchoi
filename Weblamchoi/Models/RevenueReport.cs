using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace weblamchoi.Models
{

    public class RevenueReport
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
    }


}
