namespace _66044011_Tatsunori.Models;

public partial class Couponusage
{
    public int      UsageId  { get; set; }
    public int      CouponId { get; set; }
    public int      UserId   { get; set; }
    public int      OrderId  { get; set; }
    public DateTime UsedAt   { get; set; }
    public virtual Coupon? Coupon { get; set; }
    public virtual User?   User   { get; set; }
    public virtual Order?  Order  { get; set; }
}
