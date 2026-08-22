using Max35050.Auth;
using Max35050.Helper;
using Max35050.Net;

namespace Max35050.API;

public static class OrderAPI
{
    public static async Task<IResult> CreateOrder(dynamic body, string authToken)
    {
        try
        {
            // --- Security Checks ---
            User user = await AuthManager.GetUserFromToken(authToken);
            bool isSuperuser = await AuthManager.IsTokenSuperuser(authToken);
            if (!isSuperuser && user.isCompany == false && SettingsManager.warehouseEnabled == false)
            {
                return Results.Unauthorized();
            } else if (isSuperuser && body.user != null)
            {
                user = await UserManager.GetUser(body.user.ToString());
            }
            UserData? userData = await UserManager.GetUserData(user.id);
            if (userData == null)
            {
                return Results.Unauthorized();
            }

            // --- Create Order ---
            dynamic createOrderRequest = new {
                status = "requested",
                extras = body.extras != null ? body.extras.ToString() : "",
                company = user.id,
                transaction = "",
                paid = false,
                cost = 0.0m,
                extraCost = 0.0m,
                extraEuroCost = 0.0m,
            };
            List<CreateOrderItem> createOrderItemRequests = new List<CreateOrderItem>();
            decimal totalCost = 0.0m;
            decimal totalEuroCost = 0.0m;
            if (body.items != null)
            {
                foreach (var item in body.items)
                {
                    StockItem? stockItem = await OrderManager.GetStockItem(item.item.ToString());
                    if (stockItem == null)
                    {
                        continue;
                    }
                    if (item.amount == null || item.amount <= 0)
                    {
                        continue;
                    }
                    totalCost += stockItem.price * (int)item.amount;
                    totalEuroCost += stockItem.euroPrice * (int)item.amount;
                    createOrderItemRequests.Add(new CreateOrderItem
                    {
                        item = item.item.ToString(),
                        company = user.id,
                        order = "NULL_FOR_NOW",
                        amount = (int)item.amount,
                        available = (int)item.amount
                    });
                }
            }
            Account account = await AccountManager.GetAccountByUserID(user.id);
            if (account == null)
            {
                return Results.BadRequest(new { valid = false, message = "User does not have an account." });
            }
            if (account.balance < totalCost && !isSuperuser)
            {
                return Results.BadRequest(new { valid = false, message = "Insufficient funds." });
            }
            if(userData.euroAvailable != -1 && userData.euroAvailable < userData.euroSpent + totalEuroCost && !isSuperuser)
            {
                return Results.BadRequest(new { valid = false, message = "Insufficient euro funds." });
            }
            dynamic result = await Server.db.Collections("orders").Create(createOrderRequest);
            string orderID = result.id.ToString();
            List<string> itemIDs = new List<string>();
            for (int i = 0; i < createOrderItemRequests.Count; i++)
            {
                CreateOrderItem item = createOrderItemRequests[i];
                item.order = orderID;
                dynamic itemResult = await Server.db.Collections("order_items").Create(item);
                itemIDs.Add(itemResult.id.ToString());
            }
            // Update the order with the correct item IDs and move money
            TransactionData transactionData = new TransactionData
            {
                sender = account.id,
                receiver = SettingsManager.warehouseCollectionAccountID,
                amount = totalCost,
                description = $"Zahlung für Bestellung: {orderID}",
                system = "##Order_Handshake_Created##",
                status = TransactionStatus.Approved,
                applyTaxes = false, // No taxes for internal transactions
                taxResult = null,
            };
            await UserManager.AddEuroSpent(user.id, totalEuroCost);
            string transactionID = await TransactionAPI.MoveMoney(transactionData);
            await Server.db.Collections("orders").Update(orderID, new { items = itemIDs, transaction = transactionID, paid = true, cost = totalCost, euroCost = totalEuroCost });

            return Results.Ok(new { valid = true, message = "Order created successfully.", id = orderID });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in CreateOrder: {e.Message}\n{e.StackTrace}");
            return Results.Problem("An error occurred while processing the request.", statusCode: 500);
        }
        
    }
    public static async Task<IResult> UpdateOrder(dynamic body, string authToken)
    {
        try
        {
            User user = await AuthManager.GetUserFromToken(authToken);
            bool isSuperuser = await AuthManager.IsTokenSuperuser(authToken);
    
            // --- Security Checks ---
            if (!isSuperuser && user.isCompany == false && SettingsManager.warehouseEnabled == false)
            {
                return Results.Unauthorized();
            } else if (isSuperuser && body.user != null)
            {
                user = await UserManager.GetUser(body.user.ToString());
            }
            UserData? userData = await UserManager.GetUserData(user.id);
            if (userData == null)
            {
                return Results.Unauthorized();
            }
    
            Order order = await OrderManager.GetOrder(body.id.ToString());
            if (order == null)
            {
                return Results.BadRequest(new { valid = false, message = "Order not found." });
            }

            decimal oldEuroCost = order.euroCost;

            if (!isSuperuser && order.company != user.id)
            {
                return Results.Unauthorized();
            }
            if (!isSuperuser && order.status != "requested")
            {
                return Results.BadRequest(new { valid = false, message = "Order cannot be updated in its current state." });
            }
    
            string? extras = body.extras != null ? body.extras.ToString() : null;
            List<UpdateOrderItemRequest>? items = new List<UpdateOrderItemRequest>();
    
            List<OrderItem> updatedOrderItems = new List<OrderItem>();
            List<OrderItem> itemsToDelete = new List<OrderItem>();
            foreach (OrderItem oi in order.items)
            {
                OrderItem oldItem = new OrderItem
                {
                    id = oi.id,
                    item = oi.item,
                    company = oi.company,
                    order = oi.order,
                    amount = oi.amount,
                    available = oi.available
                };
                updatedOrderItems.Add(oldItem);
            }
    
            foreach (var item in body.items)
            {
                string action = item.action.ToString();
                if (action == "delete")
                {
                    itemsToDelete.Add(updatedOrderItems.FirstOrDefault(oi => oi.id == item.id.ToString())!);
                    updatedOrderItems.RemoveAll(oi => oi.id == item.id.ToString());
                    continue;
                }
                if (action == "update")
                {
                    OrderItem? existingItem = updatedOrderItems.FirstOrDefault(oi => oi.id == item.id.ToString());
                    if (existingItem != null)
                    {
                        if (item.amount != null && item.amount > 0)
                        {
                            existingItem.amount = (int)item.amount;
                            existingItem.available = (int)item.amount; // Reset available to the new amount
                        }
                    }
                    continue;
                }
                if (action == "create")
                {
                    StockItem? stockItem = await OrderManager.GetStockItem(item.item.ToString());
                    if (stockItem == null)
                    {
                        return Results.BadRequest(new { valid = false, message = $"Stock item with ID {item.item} not found." });
                    }
                    if (item.amount == null || item.amount <= 0)
                    {
                        return Results.BadRequest(new { valid = false, message = "Amount must be greater than 0 for new items." });
                    }
                    OrderItem newOrderItem = new OrderItem
                    {
                        id = "", // Will be set by the database
                        item = stockItem,
                        company = user.id,
                        order = order.id,
                        amount = (int)item.amount,
                        available = (int)item.amount
                    };
                    updatedOrderItems.Add(newOrderItem);
                }
            }
            foreach (var item in updatedOrderItems)
            {
                Debug.Log($"Updated Order Item: ID={item.id}, ItemID={item.item.id}, Amount={item.amount}, Available={item.available}, Company={item.company}, Order={item.order}, Price={item.item.price}, EuroPrice={item.item.euroPrice}");
            }
    
            // Recount all the costs
            decimal totalCost = updatedOrderItems.Sum(oi => oi.item.price * oi.amount);
            TransactionRecord? transaction = await TransactionManager.GetTransaction(order.transaction);
            if (transaction == null)
            {
                return Results.BadRequest(new { valid = false, message = "Transaction not found." });
            }
            decimal difference = totalCost - transaction.amount;
            Account account = await AccountManager.GetAccountByUserID(user.id);
            if (account == null)
            {
                return Results.BadRequest(new { valid = false, message = "User does not have an account." });
            }
            if (difference > 0 && account.balance < difference)
            {
                return Results.BadRequest(new { valid = false, message = "Insufficient funds to cover the updated order cost." });
            }

            decimal transactionEuroCost = updatedOrderItems.Sum(oi => oi.item.euroPrice * oi.amount);
            Debug.Log($"Old Euro Cost: {oldEuroCost}, New Euro Cost: {transactionEuroCost}, Difference: {transactionEuroCost - oldEuroCost}");

            decimal euroWithoutTransaction = await UserManager.SubtractEuroSpent(user.id, oldEuroCost);
            if (userData.euroAvailable != -1 && difference > 0 && userData.euroAvailable < euroWithoutTransaction + transactionEuroCost && !isSuperuser)
            {
                await UserManager.AddEuroSpent(user.id, oldEuroCost); // Revert the euroSpent change
                return Results.BadRequest(new { valid = false, message = "Insufficient euro funds to cover the updated order cost." });
            }
            await UserManager.AddEuroSpent(user.id, transactionEuroCost); // Add the new euro cost to the user's spent amount


            await TransactionManager.UpdateTransactionAmount(order.transaction, totalCost, totalCost);
            await AccountManager.SubtractBalance(account.id, difference);
            await AccountManager.AddBalance(SettingsManager.warehouseCollectionAccountID, difference);

            // Apply changes
            List<string> itemIDs = new List<string>();
            for (int i = 0; i < updatedOrderItems.Count; i++)
            {
                dynamic oi = updatedOrderItems[i];
                if (string.IsNullOrEmpty(oi.id))
                {
                    dynamic createOrderItemRequest = new
                    {
                        item = oi.item.id,
                        company = user.id,
                        order = order.id,
                        amount = oi.amount,
                        available = oi.available
                    };
                    dynamic itemResult = await Server.db.Collections("order_items").Create(createOrderItemRequest);
                    itemIDs.Add(itemResult.id.ToString());
                }
                else
                {
                    dynamic updateOrderItemRequest = new
                    {
                        amount = oi.amount,
                        available = oi.available
                    };
                    dynamic result = await Server.db.Collections("order_items").Update(oi.id, updateOrderItemRequest);
                    itemIDs.Add(oi.id.ToString());
                }
            }
            foreach (OrderItem oi in itemsToDelete)
            {
                if (!string.IsNullOrEmpty(oi.id))
                {
                    await Server.db.Collections("order_items").Delete(oi.id);
                }
            }
            await Server.db.Collections("orders").Update(order.id, new { items = itemIDs, extras = extras, cost = totalCost, euroCost = transactionEuroCost });
            return Results.Ok(new { valid = true, message = "Order updated successfully." });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in UpdateOrder: {e.Message}\n{e.StackTrace}");
            return Results.Problem("An error occurred while processing the request.", statusCode: 500);
        }
    }
    public static async Task<IResult> DeleteOrder(dynamic body, string authToken)
    {
        try
        {
            User user = await AuthManager.GetUserFromToken(authToken);
            bool isSuperuser = await AuthManager.IsTokenSuperuser(authToken);
            if (!isSuperuser && user.isCompany == false && SettingsManager.warehouseEnabled == false)
            {
                return Results.Unauthorized();
            } else if (isSuperuser && body.user != null)
            {
                user = await UserManager.GetUser(body.user.ToString());
            }
    
            Order order = await OrderManager.GetOrder(body.id.ToString());
            if (order == null)
            {
                return Results.BadRequest(new { valid = false, message = "Order not found." });
            }
            if (!isSuperuser && order.company != user.id)
            {
                return Results.Unauthorized();
            }
            if (order.status == "collected")
            {
                await Server.db.Collections("orders").Delete(order.id);
                return Results.Ok(new { valid = true, message = "Order deleted successfully." });
            }
            if (!isSuperuser && order.status != "requested")
            {
                return Results.BadRequest(new { valid = false, message = "Order cannot be deleted in its current state." });
            }
            TransactionRecord? transaction = await TransactionManager.GetTransaction(order.transaction);
            if (transaction != null)
            {
                await AccountManager.AddBalance(transaction.sender, transaction.amount);
                await AccountManager.SubtractBalance(transaction.receiver, transaction.amount);
                await Server.db.Collections("transactions").Delete(transaction.id);
            }
            await UserManager.SubtractEuroSpent(user.id, order.euroCost);
            await Server.db.Collections("orders").Delete(order.id);
            return Results.Ok(new { valid = true, message = "Order deleted successfully." });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in DeleteOrder: {e.Message}\n{e.StackTrace}");
            return Results.Problem("An error occurred while processing the request.", statusCode: 500);
        }
    }
    public static async Task<IResult> WarehouseUpdateOrder(dynamic body, string authToken)
    {
        try
        {
            User user = await AuthManager.GetUserFromToken(authToken);
            bool isSuperuser = await AuthManager.IsTokenSuperuser(authToken);
            if (!isSuperuser)
            {
                UserData? userData = await UserManager.GetUserData(user.id);
                if (userData == null || userData.warehouseAccess == false)
                {
                    return Results.Unauthorized();
                }
            }

            Order? order = await OrderManager.GetOrder(body.id.ToString());
            if (order == null)
            {
                return Results.BadRequest(new { valid = false, message = "Order not found." });
            }

            // Store the old totals before applying any warehouse changes.
            decimal oldCost = order.cost;
            decimal oldEuroCost = order.euroCost;
            decimal oldExtraCost = order.extraCost;
            decimal oldExtraEuroCost = order.extraEuroCost;

            string? newStatus = body.status != null ? body.status.ToString() : null;
            decimal? extraCost = body.extraCost != null ? Convert.ToDecimal(body.extraCost) : null;
            decimal? extraEuroCost = body.extraEuroCost != null ? Convert.ToDecimal(body.extraEuroCost) : null;
            string? newExtras = body.extras != null ? body.extras.ToString() : null;

            // --- Status updates ---
            if (order.status == "requested" && newStatus == "approved")
            {
                await Server.db.Collections("orders").Update(order.id, new { status = "approved" });
                await NotificationManager.CreateNotification(
                    order.company,
                    "Bestellung genehmigt",
                    $"Deine Bestellung ({order.id}) wurde vom Warenlager genehmigt. Wir informieren dich, sobald die Bestellung abgeholt werden kann.",
                    $"order#{order.id}"
                );
            }

            if (order.status == "requested" && newStatus == "rejected")
            {
                await Server.db.Collections("orders").Update(order.id, new { status = "rejected" });
                await NotificationManager.CreateNotification(
                    order.company,
                    "Bestellung abgelehnt",
                    $"Deine Bestellung ({order.id}) wurde vom Warenlager abgelehnt.",
                    $"order#{order.id}"
                );

                await UserManager.SubtractEuroSpent(order.company, oldEuroCost + oldExtraEuroCost);

                TransactionRecord? transaction = await TransactionManager.GetTransaction(order.transaction);
                if (transaction != null)
                {
                    await AccountManager.AddBalance(transaction.sender, transaction.amount);
                    await AccountManager.SubtractBalance(transaction.receiver, transaction.amount);
                    await Server.db.Collections("transactions").Delete(transaction.id);
                }

                return Results.Ok(new { valid = true, message = "Order rejected successfully." });
            }

            if (order.status == "approved" && newStatus == "collectable")
            {
                await Server.db.Collections("orders").Update(order.id, new { status = "collectable" });
                await NotificationManager.CreateNotification(
                    order.company,
                    "Bestellung abholbereit",
                    $"Deine Bestellung ({order.id}) kann nun im Warenlager abgeholt werden.",
                    $"order#{order.id}"
                );
            }

            if (order.status == "collectable" && newStatus == "collected")
            {
                await Server.db.Collections("orders").Update(order.id, new { status = "collected" });
            }

            // --- Warehouse-editable order metadata ---
            if (newExtras != null)
            {
                await Server.db.Collections("orders").Update(order.id, new { extras = newExtras });
            }

            bool costsChanged = false;

            if (extraCost.HasValue)
            {
                await Server.db.Collections("orders").Update(order.id, new { extraCost = extraCost.Value });
                costsChanged = true;
            }

            if (extraEuroCost.HasValue)
            {
                await Server.db.Collections("orders").Update(order.id, new { extraEuroCost = extraEuroCost.Value });
                costsChanged = true;
            }

            // --- Item updates ---
            bool itemsChanged = false;
            List<string> itemIDs = order.items.Select(oi => oi.id).ToList();

            if (body.items != null)
            {
                foreach (var itemRequest in body.items)
                {
                    string action = itemRequest.action != null
                        ? itemRequest.action.ToString()
                        : "update";

                    if (action == "create")
                    {
                        if (itemRequest.item == null)
                        {
                            return Results.BadRequest(new { valid = false, message = "A stock item ID is required." });
                        }

                        StockItem? stockItem = await OrderManager.GetStockItem(itemRequest.item.ToString());
                        if (stockItem == null)
                        {
                            return Results.BadRequest(new
                            {
                                valid = false,
                                message = $"Stock item with ID {itemRequest.item} not found."
                            });
                        }

                        int amount = itemRequest.amount != null
                            ? Convert.ToInt32(itemRequest.amount)
                            : 0;

                        if (amount <= 0)
                        {
                            return Results.BadRequest(new
                            {
                                valid = false,
                                message = "Amount must be greater than 0 for warehouse-added items."
                            });
                        }

                        dynamic createOrderItemRequest = new
                        {
                            item = stockItem.id,
                            company = order.company,
                            order = order.id,
                            amount,
                            available = amount,
                            warehouseAdded = true
                        };

                        dynamic createdItem = await Server.db
                            .Collections("order_items")
                            .Create(createOrderItemRequest);

                        itemIDs.Add(createdItem.id.ToString());
                        itemsChanged = true;
                        continue;
                    }

                    if (itemRequest.id == null)
                    {
                        return Results.BadRequest(new { valid = false, message = "Order item ID is required." });
                    }

                    string orderItemID = itemRequest.id.ToString();
                    OrderItem? orderItem = order.items.FirstOrDefault(oi => oi.id == orderItemID);
                    if (orderItem == null)
                    {
                        return Results.BadRequest(new
                        {
                            valid = false,
                            message = $"Order item with ID {orderItemID} was not found in this order."
                        });
                    }

                    dynamic? storedItem = await Server.db
                        .Collections("order_items")
                        .GetOne(orderItemID);

                    bool warehouseAdded = storedItem?.warehouseAdded != null
                        && Convert.ToBoolean(storedItem!.warehouseAdded);

                    if (action == "delete")
                    {
                        if (!warehouseAdded)
                        {
                            return Results.BadRequest(new
                            {
                                valid = false,
                                message = "Only warehouse-added items can be removed by warehouse staff."
                            });
                        }

                        await Server.db.Collections("order_items").Delete(orderItemID);
                        itemIDs.Remove(orderItemID);
                        itemsChanged = true;
                        continue;
                    }

                    if (warehouseAdded)
                    {
                        // For warehouse-added items, amount and available are permanently linked.
                        int amount = itemRequest.amount != null
                            ? Convert.ToInt32(itemRequest.amount)
                            : itemRequest.available != null
                                ? Convert.ToInt32(itemRequest.available)
                                : orderItem.amount;

                        if (amount <= 0)
                        {
                            return Results.BadRequest(new
                            {
                                valid = false,
                                message = "Amount must be greater than 0 for warehouse-added items."
                            });
                        }

                        await Server.db.Collections("order_items").Update(orderItemID, new
                        {
                            amount,
                            available = amount
                        });

                        itemsChanged = true;
                        continue;
                    }

                    // Customer-added items keep their original amount. Warehouse staff only set availability.
                    if (itemRequest.available != null)
                    {
                        int available = Convert.ToInt32(itemRequest.available);

                        if (available < 0)
                        {
                            available = 0;
                        }

                        if (available > orderItem.amount)
                        {
                            available = (int)orderItem.amount;
                        }

                        await Server.db.Collections("order_items").Update(orderItemID, new { available });
                        itemsChanged = true;
                    }
                }
            }

            if (itemsChanged)
            {
                await Server.db.Collections("orders").Update(order.id, new { items = itemIDs });
                costsChanged = true;
            }

            // Recalculate the complete order after item or surcharge changes.
            if (costsChanged)
            {
                order = (await OrderManager.GetOrder(order.id))!;

                decimal newCost = order.items.Sum(oi => oi.item.price * oi.available);
                decimal newEuroCost = order.items.Sum(oi => oi.item.euroPrice * oi.available);
                decimal newExtraCost = order.extraCost;
                decimal newExtraEuroCost = order.extraEuroCost;

                decimal oldTotalCost = oldCost + oldExtraCost;
                decimal newTotalCost = newCost + newExtraCost;
                decimal costDifference = newTotalCost - oldTotalCost;

                decimal oldTotalEuroCost = oldEuroCost + oldExtraEuroCost;
                decimal newTotalEuroCost = newEuroCost + newExtraEuroCost;

                await Server.db.Collections("orders").Update(order.id, new
                {
                    cost = newCost,
                    euroCost = newEuroCost
                });

                TransactionRecord? transaction = await TransactionManager.GetTransaction(order.transaction);
                if (transaction != null)
                {
                    await TransactionManager.UpdateTransactionAmount(
                        order.transaction,
                        newTotalCost,
                        newTotalCost
                    );
                }

                if (costDifference != 0)
                {
                    Account? senderAccount = await AccountManager.GetAccountByUserID(order.company);
                    if (senderAccount != null)
                    {
                        await AccountManager.SubtractBalance(senderAccount.id, costDifference);
                    }

                    await AccountManager.AddBalance(
                        SettingsManager.warehouseCollectionAccountID,
                        costDifference
                    );
                }

                if (oldTotalEuroCost != newTotalEuroCost)
                {
                    await UserManager.SubtractEuroSpent(order.company, oldTotalEuroCost);
                    await UserManager.AddEuroSpent(order.company, newTotalEuroCost);
                }
            }

            return Results.Ok(new { valid = true, message = "Order updated successfully." });
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in WarehouseUpdateOrder: {e.Message} {e.StackTrace}");
            return Results.Problem("An error occurred while processing the request.", statusCode: 500);
        }
    }
    public static async Task<IResult> AdminOrderAPI(dynamic body, string authToken)
    {
        if (!await AuthManager.IsTokenSuperuser(authToken))
        {
            return Results.Unauthorized();
        }
        AdminOrderRequest request = await AdminOrderRequest.FromDynamic(body);
        switch (request.action)
        {
            case "create":
                return await AdminCreateOrder(request);
            case "update":
                return await AdminUpdateOrder(request);
            case "delete":
                return await AdminDeleteOrder(request);
            default:
                return Results.BadRequest(new { valid = false, message = "Invalid action." });
        }
    }
    private static async Task<IResult> AdminCreateOrder(AdminOrderRequest request)
    {
        if (string.IsNullOrEmpty(request.userID))
        {
            return Results.BadRequest(new { valid = false, message = "User ID is required for creating an order." });
        }
        if (string.IsNullOrEmpty(request.status))
        {
            request.status = "requested"; // Default status
        }

        dynamic createOrderRequest = new
        {
            status = request.status,
            extras = request.extras ?? "",
            company = request.userID,
            transaction = "",
            paid = false,
            cost = 0.0m,
            extraCost = request.extraCost ?? 0.0m,
            extraEuroCost = request.extraEuroCost ?? 0.0m,
        };
        dynamic result = await Server.db.Collections("orders").Create(createOrderRequest);
        Order order = await OrderManager.GetOrder(result.id.ToString());
        if (order == null)
        {
            await Server.db.Collections("orders").Delete(result.id.ToString());
            return Results.BadRequest(new { valid = false, message = "Failed to create order." });
        }
        List<string> itemIDs = new List<string>();
        foreach (var item in request.items ?? new List<AdminOrderItemRequest>())
        {
            if (item.action == "create")
            {
                StockItem? stockItem = await OrderManager.GetStockItem(item.item);
                if (stockItem == null)
                {
                    continue; // Skip invalid stock items
                }
                dynamic createOrderItemRequest = new
                {
                    item = stockItem.id,
                    company = request.userID,
                    order = order.id,
                    amount = item.amount ?? 0,
                    available = item.available ?? 0
                };
                dynamic itemResult = await Server.db.Collections("order_items").Create(createOrderItemRequest);
                itemIDs.Add(itemResult.id.ToString());
            }
        }
        await Server.db.Collections("orders").Update(order.id, new { items = itemIDs });
        order = (await OrderManager.GetOrder(order.id))!;
        TransactionData transactionData = new TransactionData
        {
            sender = (await AccountManager.GetAccountByUserID(request.userID))?.id ?? "",
            receiver = SettingsManager.warehouseCollectionAccountID,
            amount = order.items.Sum(i => i.item.price * i.available) + order.extraCost,
            description = $"Zahlung für Bestellung: {order.id}",
            system = "##Order_Handshake_Created##",
            status = TransactionStatus.Approved,
            applyTaxes = false,
            taxResult = null,
            moveMoney = request.applyMoneyChanges
        };
        string transactionID = await TransactionAPI.MoveMoney(transactionData);
        if (request.applyEuroChanges)
        {
            await UserManager.AddEuroSpent(request.userID, order.items.Sum(i => i.item.euroPrice * i.available) + order.extraEuroCost);
        }
        await Server.db.Collections("orders").Update(order.id, new { transaction = transactionID, paid = true, cost = transactionData.amount, euroCost = order.items.Sum(i => i.item.euroPrice * i.available) });
        return Results.Ok(new { valid = true, message = "Order created successfully.", id = order.id });
    }
    private static async Task<IResult> AdminUpdateOrder(AdminOrderRequest request)
    {
        if (string.IsNullOrEmpty(request.orderID))
        {
            return Results.BadRequest(new { valid = false, message = "Order ID is required for updating an order." });
        }
        Order? order = await OrderManager.GetOrder(request.orderID);
        if (order == null)
        {
            return Results.BadRequest(new { valid = false, message = "Order not found." });
        }
        if (!string.IsNullOrEmpty(request.status))
        {
            await Server.db.Collections("orders").Update(order.id, new { status = request.status });
        }
        if (!string.IsNullOrEmpty(request.extras))
        {
            await Server.db.Collections("orders").Update(order.id, new { extras = request.extras });
        }
        if (request.extraCost.HasValue)
        {
            await Server.db.Collections("orders").Update(order.id, new { extraCost = request.extraCost.Value });
        }
        if (request.extraEuroCost.HasValue)
        {
            await Server.db.Collections("orders").Update(order.id, new { extraEuroCost = request.extraEuroCost.Value });
        }
        if (request.items != null)
        {
            List<OrderItem> currentOrderItems = order.items;
            foreach (var item in request.items)
            {
                if (item.action == "create")
                {
                    StockItem? stockItem = await OrderManager.GetStockItem(item.item);
                    if (stockItem == null)
                    {
                        continue; // Skip invalid stock items
                    }
                    dynamic createOrderItemRequest = new
                    {
                        item = stockItem.id,
                        company = order.company,
                        order = order.id,
                        amount = item.amount ?? 0,
                        available = item.available != null ? item.available : item.amount ?? 0
                    };
                    dynamic itemResult = await Server.db.Collections("order_items").Create(createOrderItemRequest);
                    currentOrderItems.Add(new OrderItem
                    {
                        id = itemResult.id.ToString(),
                        item = stockItem,
                        company = order.company,
                        order = order.id,
                        amount = item.amount ?? 0,
                        available = item.available != null ? item.available.Value : item.amount ?? 0
                    });
                }
                else if (item.action == "update")
                {
                    OrderItem? existingItem = currentOrderItems.FirstOrDefault(oi => oi.id == item.id);
                    if (existingItem != null)
                    {
                        dynamic updateOrderItemRequest = new
                        {
                            amount = item.amount ?? existingItem.amount,
                            available = item.available ?? existingItem.available
                        };
                        await Server.db.Collections("order_items").Update(existingItem.id, updateOrderItemRequest);
                        existingItem.amount = item.amount ?? existingItem.amount;
                        existingItem.available = item.available ?? existingItem.available;
                    } else {
                        existingItem = currentOrderItems.FirstOrDefault(oi => oi.item.id == item.item);
                        if (existingItem != null)
                        {
                            dynamic updateOrderItemRequest = new
                            {
                                amount = item.amount ?? existingItem.amount,
                                available = item.available ?? existingItem.available
                            };
                            await Server.db.Collections("order_items").Update(existingItem.id, updateOrderItemRequest);
                            existingItem.amount = item.amount ?? existingItem.amount;
                            existingItem.available = item.available ?? existingItem.available;
                        } else {
                            continue;
                        }
                    }
                }
                else if (item.action == "delete")
                {
                    OrderItem? existingItem = currentOrderItems.FirstOrDefault(oi => oi.id == item.id);
                    if (existingItem != null)
                    {
                        await Server.db.Collections("order_items").Delete(existingItem.id);
                        currentOrderItems.Remove(existingItem);
                    } else
                    {
                        existingItem = currentOrderItems.FirstOrDefault(oi => oi.item.id == item.item);
                        if (existingItem != null)
                        {
                            await Server.db.Collections("order_items").Delete(existingItem.id);
                            currentOrderItems.Remove(existingItem);
                        } else
                        {
                            continue;
                        }
                    }
                }
            }
            List<string> updatedItemIDs = currentOrderItems.Select(oi => oi.id).ToList();
            await Server.db.Collections("orders").Update(order.id, new { items = updatedItemIDs });
            
        }
        
        decimal oldCost = order.cost;
        decimal oldEuroCost = order.euroCost;
        decimal oldExtraCost = order.extraCost;
        decimal oldExtraEuroCost = order.extraEuroCost;
        if (request.items != null)
        {
            order = (await OrderManager.GetOrder(order.id))!;
        }
        decimal newCost = order.items.Sum(oi => oi.item.price * oi.available);
        decimal newEuroCost = order.items.Sum(oi => oi.item.euroPrice * oi.available);
        decimal newExtraCost = order.extraCost;
        decimal newExtraEuroCost = order.extraEuroCost;

        await TransactionManager.UpdateTransactionAmount(order.transaction, newCost + newExtraCost, newCost + newExtraCost);

        if (request.applyMoneyChanges)
        {
            string accountID = (await AccountManager.GetAccountByUserID(order.company))?.id ?? "";
            // Add the old money back and remove the new money
            await AccountManager.AddBalance(accountID, oldCost + oldExtraCost);
            await AccountManager.SubtractBalance(accountID, newCost + newExtraCost);
            await AccountManager.SubtractBalance(SettingsManager.warehouseCollectionAccountID, oldCost + oldExtraCost);
            await AccountManager.AddBalance(SettingsManager.warehouseCollectionAccountID, newCost + newExtraCost);
            await Server.db.Collections("orders").Update(order.id, new { cost = newCost, euroCost = newEuroCost, extraCost = newExtraCost });
        }
        if (request.applyEuroChanges)
        {
            await UserManager.SubtractEuroSpent(order.company, oldEuroCost + oldExtraEuroCost);
            await UserManager.AddEuroSpent(order.company, newEuroCost + newExtraEuroCost);
        }
        return Results.Ok(new { valid = true, message = "Order updated successfully." });
    }
    private static async Task<IResult> AdminDeleteOrder(AdminOrderRequest request)
    {
        if (string.IsNullOrEmpty(request.orderID))
        {
            return Results.BadRequest(new { valid = false, message = "Order ID is required for deleting an order." });
        }
        Order? order = await OrderManager.GetOrder(request.orderID);
        if (order == null)
        {
            return Results.BadRequest(new { valid = false, message = "Order not found." });
        }
        if (request.applyMoneyChanges)
        {
            await AccountManager.AddBalance((await AccountManager.GetAccountByUserID(order.company))?.id ?? "", order.cost + order.extraCost);
            await AccountManager.SubtractBalance(SettingsManager.warehouseCollectionAccountID, order.cost + order.extraCost);
        }
        if (request.transactionAction == "delete")
        {
            TransactionRecord? transaction = await TransactionManager.GetTransaction(order.transaction);
            if (transaction != null)
            {
                await Server.db.Collections("transactions").Delete(transaction.id);
            }
        }
        else if (request.transactionAction == "update")
        {
            await TransactionManager.UpdateTransactionAmount(order.transaction, 0.0m, 0.0m);
        }
        if (request.applyEuroChanges)
        {
            await UserManager.SubtractEuroSpent(order.company, order.euroCost + order.extraEuroCost);
        }
        await Server.db.Collections("orders").Delete(order.id);
        
        return Results.Ok(new { valid = true, message = "Order deleted successfully." });
    }
}

// All classes to keep stuff organized
public class UpdateOrderRequest
{
    public string id { get; set; } = "";
    public string status { get; set; } = "";
    public List<UpdateOrderItemRequest> items { get; set; } = new List<UpdateOrderItemRequest>();
}
public class UpdateOrderItemRequest
{
    public string id { get; set; } = "";
    public string action { get; set; } = "create"; // create, update, delete
    public int? amount { get; set; } = null;
}
public class CreateOrderItemsRequest
{
    public string id { get; set; } = "";
    public int amount { get; set; } = 0;
}
public class UpdateAvailableRequest
{
    public string id { get; set; } = "";
    public long available { get; set; } = 0;
    public UpdateAvailableRequest(string id, long available)
    {
        this.id = id;
        this.available = available;
    }
}
public class CreateOrderItem
{
    public string item { get; set; } = "";
    public string company { get; set; } = "";
    public string order { get; set; } = "";
    public int amount { get; set; } = 0;
    public int available { get; set; } = 0;
}

public class AdminOrderRequest
{
    public string action { get; set; } = "create"; // create, update, delete
    public string? orderID { get; set; } = null;
    public string? userID { get; set; } = null;
    public string? status { get; set; } = null;
    public string? extras { get; set; } = null;
    public decimal? extraCost { get; set; } = null;
    public decimal? extraEuroCost { get; set; } = null;
    public bool applyMoneyChanges { get; set; } = true;
    public bool applyEuroChanges { get; set; } = true;
    public string transactionAction { get; set; } = "keep"; // keep, delete, update
    public List<AdminOrderItemRequest>? items { get; set; } = null;
    public static async Task<AdminOrderRequest> FromDynamic(dynamic body)
    {
        AdminOrderRequest request = new AdminOrderRequest
        {
            action = body.action != null ? body.action.ToString() : "create",
            orderID = body.orderID != null ? body.orderID.ToString() : null,
            userID = body.userID != null ? body.userID.ToString() : null,
            status = body.status != null ? body.status.ToString() : null,
            extras = body.extras != null ? body.extras.ToString() : null,
            extraCost = body.extraCost != null ? (decimal?)body.extraCost : null,
            extraEuroCost = body.extraEuroCost != null ? (decimal?)body.extraEuroCost : null,
            applyMoneyChanges = body.applyMoneyChanges != null ? (bool)body.applyMoneyChanges : true,
            applyEuroChanges = body.applyEuroChanges != null ? (bool)body.applyEuroChanges : true,
            transactionAction = body.transactionAction != null ? body.transactionAction.ToString() : "keep"
        };
        if (body.items != null)
        {
            request.items = new List<AdminOrderItemRequest>();
            foreach (var item in body.items)
            {
                if (item.id != null)
                {
                    dynamic existingItem = await Server.db.Collections("order_items").GetOne(item.id.ToString());
                    AdminOrderItemRequest oir = new AdminOrderItemRequest
                    {
                        id = existingItem.id.ToString(),
                        item = existingItem.item.ToString(),
                        action = item.action != null ? item.action.ToString() : "update",
                        amount = item.amount != null ? (int?)item.amount : existingItem.amount != null ? (int?)existingItem.amount : 0,
                        available = item.available != null ? (int?)item.available : existingItem.available != null ? (int?)existingItem.available : 0
                    };
                    request.items.Add(oir);
                    continue;
                }
                AdminOrderItemRequest orderItemRequest = new AdminOrderItemRequest
                {
                    id = item.id != null ? item.id.ToString() : "",
                    item = item.item != null ? item.item.ToString() : "",
                    action = item.action != null ? item.action.ToString() : "create",
                    amount = item.amount != null ? (int?)item.amount : 0,
                    available = item.available != null ? (int?)item.available : item.amount != null ? (int?)item.amount : 0
                };
                request.items.Add(orderItemRequest);
            }
        }
        return request;
    }
}
public class AdminOrderItemRequest
{
    public string id { get; set; } = "";
    public string action { get; set; } = "create"; // create, update, delete
    public string item { get; set; } = ""; // Only used for create
    public int? amount { get; set; } = null;
    public int? available { get; set; } = null;
}