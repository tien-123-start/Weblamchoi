namespace weblamchoi.Models
{
    public class ProductDetailViewModel
    {
        public Product Product { get; set; }
        public List<BonusProduct> BonusProducts { get; set; } = new();
        public List<int> SelectedBonusProductIds { get; set; } = new();
    }

}
