using System.Security.Cryptography;
using System.Text;

namespace TransactionsIngest.Services;

public static class CardNumberHelper
{
    public static string Hash(string cardNumber)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(cardNumber));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Last4(string cardNumber)
    {
        return cardNumber.Length >= 4
            ? cardNumber.Substring(cardNumber.Length - 4)
            : cardNumber;
    }
}
