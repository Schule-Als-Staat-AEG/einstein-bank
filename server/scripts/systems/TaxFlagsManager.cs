using Max35050.IO;
using Max35050.Net;
using Newtonsoft.Json;

namespace Max35050.Systems;

public static class TaxFlagsManager
{
    public static List<TaxScoreRule> Rules { get; set; } = new List<TaxScoreRule>();

    public static void Initialize()
    {
        LoadTaxScoreRules().Wait();
    }

    public static async Task LoadTaxScoreRules()
    {
        try
        {
            List<TaxScoreRule> rules = new List<TaxScoreRule>();
            var items = await Server.db.Collections("tax_flagging_rules").GetFullList();
            if(items.Count == 0)
            {
                Debug.Log("No tax score rules found in database, loading default rules.", true);
                Debug.Log("Default rules path: " + Path.Combine(FileManager.DataPath, "default_tax_rules.json"), true);
                List<TaxScoreRule> defaultRules = JsonConvert.DeserializeObject<List<TaxScoreRule>>(File.ReadAllText(Path.Combine(FileManager.DataPath, "default_tax_rules.json"))) ?? new List<TaxScoreRule>();
                rules.AddRange(defaultRules);
                rules.ForEach(async rule => await WriteRuleToDatabase(rule));
            }
            foreach (var item in items)
            {
                try
                {
                    TaxScoreRule rule = new TaxScoreRule
                    {
                        Type = TaxCheckExtensions.FromString(item.type.ToString()),
                        Score = item.score,
                        Operator = item.@operator,
                        NScore = item.nScore,
                        NOperator = item.nOperator
                    };
                    rules.Add(rule);
                }
                catch { }
            }
            Rules = rules;
        }
        catch { }
    }
    public static async Task WriteRuleToDatabase(TaxScoreRule rule)
    {
        var data = new
        {
            type = rule.Type.ToString(),
            score = rule.Score,
            @operator = rule.Operator,
            nScore = rule.NScore,
            nOperator = rule.NOperator
        };
        Server.db.Collections("tax_flagging_rules").Create(data).Wait();
        Debug.LogOK($"Wrote tax score rule to database: {rule.Type.ToString()}", true);
    }
    public static TaxScoreRule GetRule(TaxCheckType type)
    {
        return Rules.FirstOrDefault(r => r.Type == type) ?? new TaxScoreRule { Type = type, Score = 0, Operator = "+", NScore = 0, NOperator = "+" };
    }
    public static void WriteToJSON()
    {
        string json = JsonConvert.SerializeObject(Rules, Formatting.Indented);
        File.WriteAllText(Path.Combine(FileManager.DataPath, "exported_tax_rules.json"), json);
    }
}