using System;
using System.Collections.Generic;

namespace _66044011_Tatsunori.Models;

public partial class Orderdetail
{
    public int OrderDetailId { get; set; }

    public int? OrderId { get; set; }

    public int? Pid { get; set; }

    public int Qty { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Product? PidNavigation { get; set; }
}
