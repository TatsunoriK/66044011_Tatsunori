namespace _66044011_Tatsunori.Models;

public partial class Coupon
{
    public int      CouponId    { get; set; }
    public string   Code        { get; set; } = null!;
    public decimal  DiscountPct { get; set; }
    public decimal  MinAmount   { get; set; }
    public int      UsageLimit  { get; set; }
    public int      UsedCount   { get; set; }
    public DateOnly? ExpireDate { get; set; }
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
    public virtual ICollection<Couponusage> Couponusages { get; set; } = new List<Couponusage>();
}
