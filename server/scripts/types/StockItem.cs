namespace Max35050;

public class StockItem
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public decimal price { get; set; }
    public long max { get; set; } = -1;
    public decimal euroPrice { get; set; }
    public string description { get; set; } = "";
}