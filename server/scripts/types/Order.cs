namespace Max35050;

public class Order
{
    public string id { get; set; } = "";
    public string company { get; set; } = "";
    public string companyName { get; set; } = "";
    public string transaction { get; set; } = "";
    public bool paid { get; set; }
    public string status { get; set; } = "";
    public decimal cost { get; set; }
    public decimal euroCost { get; set; }
    public decimal extraCost { get; set; }
    public decimal extraEuroCost { get; set; }
    public List<OrderItem> items { get; set; } = new List<OrderItem>();
}