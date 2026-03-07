using System;
using System.Collections.Generic;

namespace _66044011_Tatsunori.Models;

public partial class Productstock
{
    public int Pid { get; set; }

    public int? Quantity { get; set; }

    public DateTime? LastUpdate { get; set; }

    public virtual Product PidNavigation { get; set; } = null!;
}
