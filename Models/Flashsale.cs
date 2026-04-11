namespace _66044011_Tatsunori.Models;

public partial class Flashsale
{
    public int      FlashSaleId { get; set; }
    public int      Pid         { get; set; }
    public decimal  SalePrice   { get; set; }
    public DateTime StartTime   { get; set; }
    public DateTime EndTime     { get; set; }
    public bool     IsActive    { get; set; } = true;
    public virtual Product? PidNavigation { get; set; }
}
