using Max35050.Net;

namespace Max35050.Helper;

public static class OrderManager
{
    public static async Task<Order?> GetOrder(string orderID)
    {
        try
        {
            PocketBase.Parameters parameters = new PocketBase.Parameters();
            parameters.expand = "company,transaction,items,items.item";
            dynamic? result = await Server.db.Collections("orders").GetFirstListItem($"id=\"{orderID}\"", parameters);
    
            if (result == null)
            {
                return null;
            }
            Order order = new Order
            {
                id = result.id.ToString(),
                company = result.company.ToString(),
                companyName = result.expand.company.companyName.ToString(),
                items = new List<OrderItem>(),
                transaction = result.transaction.ToString(),
                paid = result.paid.ToObject<bool>(),
                status = result.status.ToString(),
                cost = result.cost.ToObject<decimal>(),
                euroCost = result.euroCost.ToObject<decimal>(),
                extraCost = result.extraCost.ToObject<decimal>(),
                extraEuroCost = result.extraEuroCost.ToObject<decimal>()
            };
            try
            {
                order.items = ((IEnumerable<dynamic>)result.expand.items.ToObject<IEnumerable<dynamic>>()).Select((item) => new OrderItem
                {
                    id = item.id.ToString(),
                    item = new StockItem
                    {
                        id = item.expand.item.id.ToString(),
                        name = item.expand.item.name.ToString(),
                        price = item.expand.item.price.ToObject<decimal>(),
                        max = (long)item.expand.item.max.ToObject<decimal>(),
                        euroPrice = item.expand.item.euroPrice.ToObject<decimal>(),
                        description = item.expand.item.description.ToString()
                    },
                    company = item.company.ToString(),
                    order = item.order.ToString(),
                    amount = (long)item.amount.ToObject<decimal>(),
                    available = (long)item.available.ToObject<decimal>()
                }).ToList();
            }
            catch
            {
                // Ignore cause it is an empty order, admin stuff yk
            }
            return order;
        }
        catch
        {
            return null;
        }
    }
    public static async Task<StockItem?> GetStockItem(string id)
    {
        try
        {
            dynamic? result = await Server.db.Collections("stock").GetOne(id);

            if (result == null)
            {
                return null;
            }

            StockItem stockItem = new StockItem
            {
                id = result.id.ToString(),
                name = result.name.ToString(),
                price = result.price.ToObject<decimal>(),
                max = (long)result.max.ToObject<decimal>(),
                euroPrice = result.euroPrice.ToObject<decimal>(),
                description = result.description.ToString()
            };

            return stockItem;
        }
        catch
        {
            return null;
        }
    }
    public static void SetStatus(string orderID, string status)
    {
        Server.db.Collections("orders").Update(orderID, new { status } );
    }
    public static void SetTransaction(string orderID, string transactionID)
    {
        Server.db.Collections("orders").Update(orderID, new { transaction = transactionID } );
    }
    public static void SetPaid(string orderID, bool paid)
    {
        Server.db.Collections("orders").Update(orderID, new { paid } );
    }
    public static void Purge(string orderID)
    {
        Server.db.Collections("orders").Delete(orderID);
    }
}