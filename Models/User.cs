using System;
using System.Collections.Generic;

namespace _66044011_Tatsunori.Models;

public partial class User
{
    public int       Id           { get; set; }
    public string    Username     { get; set; } = null!;
    public string    Password     { get; set; } = null!;
    public string    Email        { get; set; } = null!;
    public string?   FullName     { get; set; }
    public int?      RoleId       { get; set; }
    public DateTime? CreatedAt    { get; set; }
    public int       Points       { get; set; } = 0;
    public string    MemberLevel  { get; set; } = "Bronze";
    public bool      IsMember     { get; set; } = false;
    public DateTime? MemberExpiry { get; set; }

    public virtual ICollection<Order>        Orders         { get; set; } = new List<Order>();
    public virtual ICollection<Couponusage>  Couponusages   { get; set; } = new List<Couponusage>();
    public virtual ICollection<Pointhistory> Pointhistories { get; set; } = new List<Pointhistory>();
    public virtual Role?        Role        { get; set; }
    public virtual Userprofile? Userprofile { get; set; }
}
