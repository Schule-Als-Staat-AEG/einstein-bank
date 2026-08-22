namespace Max35050;

[Flags]
public enum TaxCheckFlags
{
    PaysVAT = 1,
    PaysIncomeTax = 2,
    BusinessToBusiness = 4,
    BusinessToPersonal = 8,
    PersonalToBusiness = 16,
    PersonalToPersonal = 32,
    BusinessToBusinessIncomePaid = 64,
    BusinessToPersonalIncomePaid = 128,
    PersonalToBusinessIncomePaid = 256,
    PersonalToPersonalIncomePaid = 512,
    BusinessToBusinessVATPaid = 1024,
    BusinessToPersonalVATPaid = 2048,
    PersonalToBusinessVATPaid = 4096,
    PersonalToPersonalVATPaid = 8192,
    DescriptionIndicatesCashRegister = 16384,
    DescriptionIndicatesSalary = 32768,
    HighAverageTransactionAmount = 65536,
    HighTransactionFrequencyBusiness = 131072,
    HighTransactionFrequencyPersonal = 262144,
}

public enum TaxCheckType
{
    PaysVAT = 0,
    PaysIncomeTax = 1,
    BusinessToBusiness = 2,
    BusinessToPersonal = 3,
    PersonalToBusiness = 4,
    PersonalToPersonal = 5,
    BusinessToBusinessIncomePaid = 6,
    BusinessToPersonalIncomePaid = 7,
    PersonalToBusinessIncomePaid = 8,
    PersonalToPersonalIncomePaid = 9,
    BusinessToBusinessVATPaid = 10,
    BusinessToPersonalVATPaid = 11,
    PersonalToBusinessVATPaid = 12,
    PersonalToPersonalVATPaid = 13,
    DescriptionIndicatesCashRegister = 14,
    DescriptionIndicatesSalary = 15,
    HighAverageTransactionAmount = 16,
    HighTransactionFrequencyBusiness = 17,
    HighTransactionFrequencyPersonal = 18,
}

public static class TaxCheckExtensions
{
    public static TaxCheckFlags ToFlag(this TaxCheckType type)
    {
        return (TaxCheckFlags)(1 << (int)type);
    }
    public static TaxCheckType ToType(this TaxCheckFlags flag)
    {
        return (TaxCheckType)(Math.Log((double)flag, 2));
    }
    public static bool HasFlag(this TaxCheckFlags flags, TaxCheckType type)
    {
        return flags.HasFlag(type.ToFlag());
    }
    public static TaxCheckType FromString(string str)
    {
        return (TaxCheckType)Enum.Parse(typeof(TaxCheckType), str);
    }
    public static string ToString(this TaxCheckType type)
    {
        return type.ToString();
    }
}