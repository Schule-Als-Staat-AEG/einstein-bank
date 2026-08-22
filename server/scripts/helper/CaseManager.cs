using Max35050.Net;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

namespace Max35050.Helper;

public static class CaseManager
{
    public static List<Case> cases = new List<Case>();
    public static async Task GetPotentialCases()
    {
        try
        {
            cases.Clear();
            PocketBase.Parameters parameters = new PocketBase.Parameters
            {
                filter = "status=\"flagged\""
            };
            List<dynamic> potentialCases = await Server.db.Collections("transactions").GetFullList(500, parameters);
            foreach (var caseItem in potentialCases)
            {
                try
                {
                    cases.Add(new Case
                    {
                        id = "NONE",
                        transaction = caseItem.id,
                        handler = "NONE",
                        taxLog = (await Server.db.Collections("tax_logs").GetFirstListItem($"transaction=\"{caseItem.id}\""))!.id.ToString() ?? "",
                        created = DateTime.Parse(caseItem.created.ToString()),
                        updated = DateTime.Parse(caseItem.created.ToString()),
                        assigned = false
                    });
                }
                catch { }
            }
    
            List<dynamic> existingCases = await Server.db.Collections("assigned_cases").GetFullList(500);
            foreach (var existingCase in existingCases)
            {
                Case? match = cases.Find(c => c.transaction == existingCase.transaction);
                if (match != null)
                {
                    match.id = existingCase.id.ToString();
                    match.handler = existingCase.handler.ToString();
                    match.taxLog = existingCase.tax_log.ToString();
                    match.updated = DateTime.Parse(existingCase.updated.ToString());
                    match.created = DateTime.Parse(existingCase.created.ToString());
                    match.assigned = true;
                }
                else
                {
                    // Delete case from assigned_cases if it no longer matches the filter
                    await Server.db.Collections("assigned_cases").Delete(existingCase.id);
                }
            }
    
            // Set list
            cases = cases.OrderBy(c => c.created).ToList(); // Sort by created date, oldest first
    
            // Broadcast available job to employees
            if(cases.Exists(c => c.assigned == false))
            {
                if (Server.jobHubContext != null)
                {
                    await Server.jobHubContext.Clients.All.SendAsync("NewCaseAvailable");
                }
            }
        }
        catch { }
    }
    public static async Task AddCase(TransactionData td)
    {
        try
        {
            Case @case = new Case
            {
                id = "NONE",
                transaction = td.transactionID!,
                handler = "NONE",
                taxLog = td.taxReport,
                created = DateTime.UtcNow,
                updated = DateTime.UtcNow,
                assigned = false
            };
            cases.Add(@case);
            await Server.jobHubContext!.Clients.All.SendAsync("NewCaseAvailable");
        }
        catch 
        {
            
        }
    }
    public static Case GetFreeCase()
    {
        return cases.FirstOrDefault(c => c.assigned == false) ?? new Case { id = "NONE" };
    }
}