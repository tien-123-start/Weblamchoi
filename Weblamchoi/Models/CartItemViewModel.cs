namespace weblamchoi.Models
{
    public class CartItemViewModel
    {
        public int CartID { get; set; }
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string ImageURL { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

}
