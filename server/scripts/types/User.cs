using Max35050.Auth;
using Newtonsoft.Json;

namespace Max35050;

public class User
{
    public string id { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public bool emailVisibility { get; set; } = false;
    public bool verified { get; set; } = false;
    public string name { get; set; } = string.Empty;
    public bool isCompany { get; set; } = false;
    public string companyName { get; set; } = string.Empty;
    public bool locked { get; set; } = false;
    public bool _hidden { get; set; } = false;
    public string data { get; set; } = string.Empty;
    public DateTime created { get; set; } = DateTime.UtcNow;
    public DateTime updated { get; set; } = DateTime.UtcNow;
    public AccountType GetAccountType()
    {
        if (isCompany)
            return AccountType.Business;
        else
            return AccountType.Personal;
    }
    public static User FromJsonApi(string json)
    {
        dynamic? data = JsonConvert.DeserializeObject<dynamic>(json);
        if (data == null)
        {
            return new User();
        }
        return new User
        {
            id = data!.id.ToString() ?? string.Empty,
            email = data!.email.ToString() ?? string.Empty,
            emailVisibility = data!.emailVisibility.ToObject<bool>(),
            verified = data!.verified.ToObject<bool>(),
            name = data!.name.ToString() ?? string.Empty,
            isCompany = data!.isCompany.ToObject<bool>(),
            companyName = data!.companyName.ToString() ?? string.Empty,
            locked = data!.locked.ToObject<bool>(),
            _hidden = data!._hidden.ToObject<bool>(),
            data = data!.data.ToString() ?? string.Empty,
            created = data!.created.ToObject<DateTime>(),
            updated = data!.updated.ToObject<DateTime>()
        };
    }
    public static User FromDynamic(dynamic data)
    {
        return new User
        {
            id = data!.id.ToString() ?? string.Empty,
            email = data!.email.ToString() ?? string.Empty,
            emailVisibility = data!.emailVisibility.ToObject<bool>(),
            verified = data!.verified.ToObject<bool>(),
            name = data!.name.ToString() ?? string.Empty,
            isCompany = data!.isCompany.ToObject<bool>(),
            companyName = data!.companyName.ToString() ?? string.Empty,
            locked = data!.locked.ToObject<bool>(),
            _hidden = data!._hidden.ToObject<bool>(),
            data = data!.data.ToString() ?? string.Empty,
            created = data!.created.ToObject<DateTime>(),
            updated = data!.updated.ToObject<DateTime>()
        };
    }
    public async Task<UserData> GetUserData()
    {
        return await UserManager.GetUserData(id);
    }
}