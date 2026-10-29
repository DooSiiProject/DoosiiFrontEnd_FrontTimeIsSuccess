using Microsoft.AspNetCore.DataProtection;

namespace Doosii.API.Configuration;

public static class PickupDataProtection
{
    // Shared by the application and the isolated restart smoke host.
    public static IServiceCollection AddPickupDataProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var keyPath = configuration["Pickup:DataProtectionKeyPath"];
        var certificate = configuration["Pickup:DataProtectionCertificateThumbprint"];
        if (!string.IsNullOrWhiteSpace(keyPath) && !Path.IsPathFullyQualified(keyPath))
            throw new InvalidOperationException("Pickup:DataProtectionKeyPath must be absolute.");
        if (!environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuration["Pickup:CodePepper"]) &&
            (string.IsNullOrWhiteSpace(keyPath) || (!OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(certificate))))
            throw new InvalidOperationException("Pickup requires a persistent protected key ring outside Development.");

        var protection = services.AddDataProtection().SetApplicationName("DooSii");
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            Directory.CreateDirectory(keyPath);
            protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
            if (!string.IsNullOrWhiteSpace(certificate)) protection.ProtectKeysWithCertificate(certificate);
            else if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        }
        return services;
    }
}
