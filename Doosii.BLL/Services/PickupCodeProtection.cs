using System.Security.Cryptography;
using System.Text;
using Doosii.BLL.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace Doosii.BLL.Services;

// Code material is always bound to the reservation, protected at rest and never logged.
public sealed class PickupCodeProtection(IDataProtectionProvider provider, IConfiguration configuration)
{
    public (byte[] Hash, string Protected) Issue(int reservationId)
    {
        var pepper = Pepper();
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var payload = $"{reservationId}:{code}";
        try
        {
            return (HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes(payload)),
                provider.CreateProtector("DooSii.Pickup.Code.v1").Protect(payload));
        }
        finally { CryptographicOperations.ZeroMemory(pepper); }
    }
    public string Read(int reservationId, byte[] hash, string protectedCode)
    {
        try
        {
            var payload = provider.CreateProtector("DooSii.Pickup.Code.v1").Unprotect(protectedCode);
            var prefix = reservationId + ":";
            if (!payload.StartsWith(prefix, StringComparison.Ordinal)) throw Unavailable();
            var code = payload[prefix.Length..];
            if (code.Length != 6 || !code.All(char.IsAsciiDigit) || !Verify(reservationId, code, hash)) throw Unavailable();
            return code;
        }
        catch (CryptographicException) { throw Unavailable(); }
    }
    public bool Verify(int reservationId, string code, byte[] hash)
    {
        var pepper = Pepper();
        try { return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes($"{reservationId}:{code}")), hash); }
        finally { CryptographicOperations.ZeroMemory(pepper); }
    }
    private byte[] Pepper()
    {
        byte[] pepper;
        try { pepper = Convert.FromBase64String(configuration["Pickup:CodePepper"] ?? ""); }
        catch (FormatException) { throw Unavailable(); }
        if (pepper.Length < 32) { CryptographicOperations.ZeroMemory(pepper); throw Unavailable(); }
        return pepper;
    }
    private static ServiceException Unavailable() => new(503, "Chưa cấu hình bảo vệ mã nhận hàng. Vui lòng thử lại sau.", "PICKUP_CODE_UNAVAILABLE");
}
