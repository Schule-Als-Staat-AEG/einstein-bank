using Max35050.API;
using Max35050.Helper;

namespace Max35050.Cmd;

public static class OrderCommands
{
    public static void PrintOrder(string orderID)
    {
        Order order = OrderManager.GetOrder(orderID).Result!;
        Debug.LogOK($"Order ID: {order.id}", true);
        Debug.LogOK($"Company: {order.company}", true);
        Debug.LogOK($"Company Name: {order.companyName}", true);
        Debug.LogOK($"Transaction ID: {order.transaction}", true);
        Debug.LogOK($"Paid: {order.paid}", true);
        Debug.LogOK($"Status: {order.status}", true);
        Debug.LogOK($"Cost: {order.cost}", true);
        Debug.LogOK($"------ Items ------", true);
        foreach (OrderItem item in order.items)
        {
            Debug.LogOK($"  - Item ID (order_item): {item.id}", true);
            Debug.LogOK($"    Item ID (stocks): {item.item.id}", true);
            Debug.LogOK($"    Name: {item.item.name}", true);
            Debug.LogOK($"    Price: {item.item.price}", true);
            Debug.LogOK($"    Amount: {item.amount}", true);
            Debug.LogOK($"    Available: {item.available}", true);
            Debug.LogOK($"    Total: {item.item.price * item.available}", true);
        }
        Debug.LogOK($"-------------------------------------------------", true);
        Debug.LogOK($"Total Amount: {order.items.Sum(item => item.item.price * item.available)}", true);
        Debug.LogOK($"-------------------------------------------------", true);
        Debug.LogOK($"Note: Total Amount is calculated based on available items, not the original order amount.", true);
    }
    public static void PrintOrderHelp()
    {
        Debug.LogOK($"Order Command Overview:", true);
        Debug.LogOK($"  order <id>                                              - Show Details about an order", true);
        Debug.LogOK($"  order.pay <id> [Log Transaction - true|false|yes|no]    - Force the deduction on an order", true);
        Debug.LogOK($"  order.paid <id> [true|false|yes|no]                    - Toggle/Mark an Order as paid", true);
        Debug.LogOK($"  order.status <id> <status>                              - Set the status of an order", true);
        Debug.LogOK($"  order.purge <id>                                        - Deletes/Purges and order", true);
    }
    public static void PayOrder(string id, bool logTransaction = false)
    {
        Order order = OrderManager.GetOrder(id).Result!;
        if(order == null)
        {
            Debug.LogError($"Order with ID {id} not found.", true);
            return;
        }
        decimal totalAmount = order.items.Sum(item => item.item.price * item.available); // Calculate total amount based on available items
        TransactionData transactionData = new TransactionData
        {
            sender = AccountManager.GetAccountID(order.company).Result,
            receiver = SettingsManager.warehouseCollectionAccountID,
            amount = totalAmount,
            description = $"Payment for order {order.id}",
            status = TransactionStatus.Approved,
            applyTaxes = false, // No taxes for internal transactions
            taxResult = null,
            logTransaction = logTransaction
        };
        if (logTransaction)
        {
            order.transaction = TransactionAPI.MoveMoney(transactionData).Result; // Wait for the transaction to complete before updating the order status
            order.status = "collected";
            OrderManager.SetStatus(order.id, order.status);
            OrderManager.SetTransaction(order.id, order.transaction);
            OrderManager.SetPaid(order.id, true);
            Debug.LogOK($"Transaction ID: {order.transaction}", true);
            Debug.LogOK($"Status: {order.status}", true);
            Debug.LogOK($"Paid: {order.paid}", true);
        }
        Debug.LogOK($"Order {id} has been marked as paid. Transaction logged: {logTransaction}.", true);
    }
    public static void SetOrderAsPaid(string id, bool? value = null)
    {
        if(value == null)
        {
            Order order = OrderManager.GetOrder(id).Result!;
            value = !order.paid;
        }
        OrderManager.SetPaid(id, value ?? false);
    }
    public static void SetOrderStatus(string id, string status)
    {
        OrderManager.SetStatus(id, status);
    }
    public static void PurgeOrder(string id)
    {
        OrderManager.Purge(id);
    }
}