namespace _66044011_Tatsunori.Models;

public partial class Pointhistory
{
    public int      PointId   { get; set; }
    public int      UserId    { get; set; }
    public int?     OrderId   { get; set; }
    public int      Points    { get; set; }
    public string   Type      { get; set; } = null!; // earn / redeem
    public DateTime CreatedAt { get; set; }
    public virtual User?  User  { get; set; }
    public virtual Order? Order { get; set; }
}
