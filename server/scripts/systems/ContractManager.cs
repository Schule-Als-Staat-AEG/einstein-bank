using Cronos;
using Max35050.API;
using Max35050.Helper;
using Max35050.Net;

namespace Max35050.Systems;

public static class ContractManager
{
    public static List<Contract> Contracts { get; set; } = new List<Contract>();
    public static CancellationTokenSource ContractWorkerCTS { get; set; } = new CancellationTokenSource();

    public static async Task Initialize()
    {
        await LoadContracts();
        _ = ContractWorker(ContractWorkerCTS.Token);
    }

    public static async Task LoadContracts()
    {
        try
        {
            List<Contract> contracts = new List<Contract>();
            List<dynamic> items = await Server.db.Collections("contracts").GetFullList();
            foreach (dynamic item in items)
            {
                try
                {
                    CronExpression cronExpression;
                    try { cronExpression = CronExpression.Parse(item.cron.ToString(), CronFormat.Standard); } catch { cronExpression = CronExpression.Parse(item.cron.ToString(), CronFormat.IncludeSeconds); }
                    Contract contract = new Contract
                    {
                        id = item.id.ToString(),
                        title = item.title.ToString(),
                        description = item.description.ToString(),
                        sender = item.sender.ToString(),
                        receiver = item.receiver.ToString(),
                        amount = item.amount,
                        cron = item.cron.ToString(),
                        maximum = item.maximum,
                        executed = item.executed,
                        completed = item.completed,
                        created = item.created,
                        cronExpression = cronExpression
                    };
                    contract.nextExecution = contract.cronExpression.GetNextOccurrence(DateTime.UtcNow);
                    contracts.Add(contract);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error occurred while processing contract item: {ex.Message}", true);
                }
            }
            Contracts = contracts;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error occurred while loading contracts: {ex.Message}", true);
        }
        Debug.LogOK($"Loaded {Contracts.Count} contracts from database!", true);
    }
    public static async Task<bool> UpdateContract(string id)
    {
        try
        {
            var item = await Server.db.Collections("contracts").GetOne(id);
            if (item != null)
            {
                CronExpression cronExpression;
                try {  cronExpression = CronExpression.Parse(item.cron.ToString(), CronFormat.Standard); } catch { cronExpression = CronExpression.Parse(item.cron.ToString(), CronFormat.IncludeSeconds); }
                Contract contract = new Contract
                {
                    id = item.id.ToString(),
                    title = item.title.ToString(),
                    description = item.description.ToString(),
                    sender = item.sender.ToString(),
                    receiver = item.receiver.ToString(),
                    amount = item.amount,
                    cron = item.cron.ToString(),
                    maximum = item.maximum,
                    executed = item.executed,
                    completed = item.completed,
                    created = item.created,
                    cronExpression = cronExpression
                };
                contract.nextExecution = contract.cronExpression.GetNextOccurrence(DateTime.UtcNow);
                int index = Contracts.FindIndex(c => c.id == id);
                if (index != -1)
                {
                    Contracts[index] = contract;
                }
                else
                {
                    Contracts.Add(contract);
                }
            } else
            {
                // If the contract was not found, remove it from the list
                Contracts.RemoveAll(c => c.id == id);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error occurred while updating contract: {ex.Message}", true);
            return false;
        }
        return true;
    }
    public static async Task ContractWorker(CancellationToken ct)
    {
        while (ct.IsCancellationRequested == false)
        {
            foreach (Contract contract in Contracts)
            {
                if (contract.completed == false && contract.nextExecution != null && contract.nextExecution <= DateTime.UtcNow && (contract.executed < contract.maximum || contract.maximum < 0) && contract.cronExpression != null)
                {
                    // Execute contract
                    TransactionData td = new TransactionData
                    {
                        sender = contract.sender,
                        receiver = contract.receiver,
                        amount = contract.amount,
                        description = $"Vertrag: {contract.title} - {contract.description}",
                        received = contract.amount,
                        system = "##CONTRACT_TRANSACTION##",
                        status = TransactionStatus.Approved,
                        applyTaxes = false,
                        logTransaction = true,
                        moveMoney = true
                    };
                    await TransactionAPI.MoveMoney(td);
                    contract.executed++;
                    if (contract.executed >= contract.maximum && contract.maximum >= 0)
                    {
                        contract.completed = true;
                    }
                    contract.nextExecution = contract.cronExpression.GetNextOccurrence(DateTime.UtcNow);
                    await Server.db.Collections("contracts").Update(contract.id, new
                    {
                        executed = contract.executed,
                        completed = contract.completed
                    });
                }
            }
            await Task.Delay(2000);
        }
    }
}