using System;
using System.Collections.Generic;

namespace _66044011_Tatsunori.Models;

public partial class Product
{
    public int     Pid         { get; set; }
    public string  Pname       { get; set; } = null!;
    public decimal Price       { get; set; }
    public string? Description { get; set; }
    public int?    CatId       { get; set; }
    public int?    BrandId     { get; set; }
    public string? ImagePath   { get; set; }

    public virtual Brand?    Brand      { get; set; }
    public virtual Category? Cat        { get; set; }
    public virtual ICollection<Orderdetail> Orderdetails { get; set; } = new List<Orderdetail>();
    public virtual Productstock? Productstock { get; set; }
}
