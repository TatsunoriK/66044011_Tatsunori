namespace _66044011_Tatsunori.Models;

public partial class Stockhistory
{
    public int       Id        { get; set; }
    public int       Pid       { get; set; }
    public int       OldQty    { get; set; }
    public int       NewQty    { get; set; }
    public string?   ChangedBy { get; set; }
    public DateTime? ChangedAt { get; set; }
    public string?   Note      { get; set; }

    public virtual Product? PidNavigation { get; set; }
}