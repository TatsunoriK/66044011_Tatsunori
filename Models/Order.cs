using System;
using System.Collections.Generic;

namespace _66044011_Tatsunori.Models;

public partial class Order
{
    public int      OrderId        { get; set; }
    public int?     UserId         { get; set; }
    public DateTime? OrderDate     { get; set; }
    public decimal  TotalAmount    { get; set; }
    public string?  Status         { get; set; }
    public string?  ShippingAddress{ get; set; }
    public decimal  DiscountAmount { get; set; } = 0;
    public int      PointsUsed     { get; set; } = 0;
    public string?  CouponCode     { get; set; }

    public virtual ICollection<Orderdetail>  Orderdetails  { get; set; } = new List<Orderdetail>();
    public virtual ICollection<Couponusage>  Couponusages  { get; set; } = new List<Couponusage>();
    public virtual ICollection<Pointhistory> Pointhistories{ get; set; } = new List<Pointhistory>();
    public virtual User? User { get; set; }
}
