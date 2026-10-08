namespace QuietMSI.Core;
public static class WmiResultRules
{
    public static bool SetterSucceeded(object? returnValue,byte[]? packet) => returnValue is not false && packet is {Length:32} && packet[0]==1;
}
