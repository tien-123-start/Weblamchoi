namespace weblamchoi.Models
{
    public class ProductBonusProduct
    {
        public int ProductID { get; set; }
        public Product Product { get; set; }

        public int BonusProductID { get; set; }
        public BonusProduct BonusProduct { get; set; }
    }

}
