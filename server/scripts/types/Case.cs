using Newtonsoft.Json;

namespace Max35050;

public class Case
{
    public string id { get; set; } = "";
    public string transaction { get; set; } = "";
    public string handler { get; set; } = "";
    public string taxLog { get; set; } = "";
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public bool assigned = false;
    public bool illegal = false;
    public string reason = "";
    public static Case? FromJson(string json)
    {
        dynamic? decoded = JsonConvert.DeserializeObject<dynamic>(json);

        if (decoded == null)
            return null;
        
        return new Case
        {
            id = decoded.id.ToString(),
            transaction = decoded.transaction.ToString() ?? "",
            handler = decoded.handler.ToString() ?? "",
            taxLog = decoded.tax_log.ToString() ?? "",
            created = DateTime.Parse(decoded.created != null ? decoded.created.ToString() : new DateTime(0).ToString()),
            updated = DateTime.Parse(decoded.updated != null ? decoded.updated.ToString() : new DateTime(0).ToString()),
            assigned = decoded.collectionName.ToString() == "assigned_cases",
            illegal = decoded.illegal != null ? bool.Parse(decoded.illegal.ToString()) : false,
            reason = decoded.reason != null ? decoded.reason.ToString() : ""
        };
    }
}