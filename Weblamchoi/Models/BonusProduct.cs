using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using weblamchoi.Models;

[Table("BonusProduct")]
public class BonusProduct : IValidatableObject
{
    [Key]
    public int BonusProductID { get; set; }
    public int ProductID { get; set; }

    public string? Name { get; set; } = string.Empty;
    public string? ImageURL { get; set; }
    public decimal Price { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [ForeignKey("ProductID")]
    public Product Product { get; set; }

    // ✅ Ràng buộc ngày
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate < StartDate)
        {
            yield return new ValidationResult(
                "Ngày kết thúc không được trước ngày bắt đầu.",
                new[] { nameof(EndDate) }
            );
        }
    }
}
