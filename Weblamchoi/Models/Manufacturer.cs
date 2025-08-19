namespace weblamchoi.Models
{
    public class Manufacturer
    {
        public int ManufacturerID { get; set; }
        public string ManufacturerName { get; set; }
        public string Country { get; set; }
        public string Website { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Product> Products { get; set; }
    }

}
