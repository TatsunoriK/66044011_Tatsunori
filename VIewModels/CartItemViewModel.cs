namespace _66044011_Tatsunori.ViewModels;

public class CartItemViewModel
{
    public int     Pid      { get; set; }
    public string  Pname    { get; set; } = "";
    public decimal Price    { get; set; }
    public int     Qty      { get; set; }
    public decimal Subtotal { get; set; }
}
