namespace Max35050;

public class OrderItem
{
    public string id { get; set; } = "";
    public StockItem item { get; set; } = new StockItem();
    public string company { get; set; } = "";
    public string order { get; set; } = "";
    public long amount { get; set; } = 0;
    public long available { get; set; } = 0;
}