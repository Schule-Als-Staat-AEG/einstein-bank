using Max35050.Helper;

namespace Max35050.Cmd;

public static class CaseCommands
{
    public static void PrintCases()
    {
        Debug.Log("Current cases in memory:\n", true);
        foreach (Case c in CaseManager.cases)
        {
            Debug.Log($"Case ID: {c.id}, Transaction ID: {c.transaction}, Handler: {c.handler}, Assigned: {c.assigned}", true);
        }
        Debug.Log("", true);
        Debug.Log($"Total cases: {CaseManager.cases.Count}, Free cases: {CaseManager.cases.Count(c => c.assigned == false)}", true);
    }
    public static void RetryCaseFetch()
    {
        Debug.Log("Retrying fetch of potential cases from database...", true);
        _ = CaseManager.GetPotentialCases();
    }
}