public class UserData
{
    public string id { get; set; } = "";
    public string user { get; set; } = "";
    public bool registered { get; set; }
    public bool firstTimeLogin { get; set; }
    public bool warehouseAccess { get; set; }
    public bool financeTrackingAccess { get; set; }
    public decimal euroSpent { get; set; }
    public decimal euroAvailable { get; set; }
    public static UserData FromDynamic(dynamic data)
    {
        return new UserData
        {
            id = data.id.ToString(),
            user = data.user.ToString(),
            registered = data.registered.ToObject<bool>(),
            firstTimeLogin = data.firstTimeLogin.ToObject<bool>(),
            warehouseAccess = data.warehouseAccess.ToObject<bool>(),
            financeTrackingAccess = data.financeTrackingAccess.ToObject<bool>(),
            euroSpent = data.euroSpent.ToObject<decimal>(),
            euroAvailable = data.euroAvailable.ToObject<decimal>()
        };
    }
}