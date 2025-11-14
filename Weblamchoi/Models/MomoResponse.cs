namespace weblamchoi.Models
{
    public class MomoResponse
    {
        public int resultCode { get; set; }
        public string message { get; set; } = null!;
        public string payUrl { get; set; } = null!;
    }
}
