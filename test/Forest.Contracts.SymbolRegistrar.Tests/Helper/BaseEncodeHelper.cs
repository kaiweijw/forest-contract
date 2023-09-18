namespace Forest.Contracts.SymbolRegistrar.Helper;

public class BaseEncodeHelper
{
    
    public static readonly char[] Base26Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
    public static readonly char[] Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
    
    public static string Base26(long value)
    {
        return BaseEncode(value, Base26Chars);
    }
    
    public static string Base62(long value)
    {
        return BaseEncode(value, Base62Chars);
    }
    
    public static string BaseEncode(long value, char[] baseChars)
    {
        if (value == 0)
            return baseChars[0].ToString();
        var result = string.Empty;
        while (value > 0)
        {
            int remainder = (int)(value % baseChars.Length);
            result = baseChars[remainder] + result;
            value /= baseChars.Length;
        }

        return result;
    }
}