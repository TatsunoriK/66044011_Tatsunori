using System;
using System.Collections.Generic;

namespace _66044011_Tatsunori.Models;

public partial class Userprofile
{
    public int UserId { get; set; }

    public string? Gender { get; set; }

    public DateOnly? Birthday { get; set; }

    public string? Tel { get; set; }

    public string? Address { get; set; }

    public string? Photo { get; set; }

    public virtual User User { get; set; } = null!;
}
