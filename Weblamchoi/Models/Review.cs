namespace weblamchoi.Models
{
    public class Review
    {
        public int ReviewID { get; set; }
        public int ProductID { get; set; }
        public int UserID { get; set; }
        public int Rating { get; set; } // 1-5
        public string Comment { get; set; }
        public DateTime ReviewDate { get; set; } = DateTime.Now;

        public Product Product { get; set; }
        public User User { get; set; }
    }

}
