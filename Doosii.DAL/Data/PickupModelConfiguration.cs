using Doosii.DAL.Models;
using Doosii.DAL.Models.Order;
using Doosii.DAL.Models.Store;
using Microsoft.EntityFrameworkCore;

namespace Doosii.DAL.Data;

internal static class PickupModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PickupReservation>(e =>
        {
            e.HasAlternateKey(r => new { r.Id, r.ProductId });
            e.HasOne<User>().WithMany().HasForeignKey(r => r.BuyerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.SellerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(r => r.ClosedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Store>().WithMany().HasForeignKey(r => r.StoreId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(r => new { r.ProductId, r.StoreId })
                .HasPrincipalKey(p => new { p.Id, p.StoreId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.BuyerId, r.ClientRequestId }).IsUnique();
            e.HasIndex(r => new { r.BuyerId, r.ProductId }).IsUnique()
                .HasFilter("[Status] IN ('PENDING','ACCEPTED')");
            e.HasIndex(r => r.ProductId).IsUnique().HasFilter("[Status] = 'ACCEPTED'");
            e.HasIndex(r => new { r.BuyerId, r.Status, r.CreatedAt, r.Id });
            e.HasIndex(r => new { r.StoreId, r.Status, r.CreatedAt, r.Id });
            e.HasIndex(r => r.ResponseDeadline).HasFilter("[Status] = 'PENDING'");
            e.HasIndex(r => r.ExpiresAt).HasFilter("[Status] = 'ACCEPTED'");
            e.ToTable("PickupReservations", "store", t =>
            {
                t.HasCheckConstraint("CK_Pickup_Status", "[Status] IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','EXPIRED','COMPLETED')");
                t.HasCheckConstraint("CK_Pickup_Request", "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64 AND [RequestHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%' AND DATALENGTH([ProductVersionSnapshot]) = 8");
                t.HasCheckConstraint("CK_Pickup_BuyerSeller", "[BuyerId] <> [SellerId]");
                t.HasCheckConstraint("CK_Pickup_TextPrice", "[PriceSnapshot] > 0 AND LEN(LTRIM(RTRIM([ContactName]))) > 0 AND LEN(LTRIM(RTRIM([ContactPhone]))) > 0 AND LEN(LTRIM(RTRIM([ProductTitleSnapshot]))) > 0");
                t.HasCheckConstraint("CK_Pickup_Schedule", "[ScheduledAt] >= DATEADD(minute,30,[CreatedAt]) AND [ScheduledAt] <= DATEADD(hour,24,[CreatedAt]) AND [ResponseDeadline] > [CreatedAt] AND [ResponseDeadline] <= [ScheduledAt] AND [ResponseDeadline] <= DATEADD(hour,2,[CreatedAt])");
                t.HasCheckConstraint("CK_Pickup_Acceptance", "([AcceptedAt] IS NULL AND [ExpiresAt] IS NULL AND [StoreNameSnapshot] IS NULL AND [StoreAddressSnapshot] IS NULL AND [PickupCodeHash] IS NULL AND [ProtectedPickupCode] IS NULL AND [Status] IN ('PENDING','REJECTED','CANCELLED','EXPIRED')) OR ([AcceptedAt] IS NOT NULL AND [AcceptedAt] >= [CreatedAt] AND [AcceptedAt] < [ResponseDeadline] AND [ExpiresAt] IS NOT NULL AND [ExpiresAt] = DATEADD(minute,30,[ScheduledAt]) AND [StoreNameSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreNameSnapshot]))) > 0 AND [StoreAddressSnapshot] IS NOT NULL AND LEN(LTRIM(RTRIM([StoreAddressSnapshot]))) > 0 AND [PickupCodeHash] IS NOT NULL AND DATALENGTH([PickupCodeHash]) = 32 AND [ProtectedPickupCode] IS NOT NULL AND LEN([ProtectedPickupCode]) > 0 AND [Status] IN ('ACCEPTED','CANCELLED','EXPIRED','COMPLETED'))");
                t.HasCheckConstraint("CK_Pickup_Closure", "([Status] IN ('PENDING','ACCEPTED') AND [ClosedAt] IS NULL AND [ClosedById] IS NULL AND [CloseReason] IS NULL) OR ([Status] IN ('REJECTED','CANCELLED','EXPIRED','COMPLETED') AND [ClosedAt] IS NOT NULL AND [ClosedAt] >= [CreatedAt] AND ([AcceptedAt] IS NULL OR [ClosedAt] >= [AcceptedAt]) AND [CloseReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CloseReason]))) > 0 AND (([Status] = 'EXPIRED' AND [ClosedById] IS NULL AND (([AcceptedAt] IS NULL AND [ClosedAt] >= [ResponseDeadline]) OR ([AcceptedAt] IS NOT NULL AND [ClosedAt] >= [ExpiresAt]))) OR ([Status] = 'COMPLETED' AND [ClosedById] IS NOT NULL AND [ClosedById] = [SellerId] AND [ClosedAt] < [ExpiresAt]) OR ([Status] = 'REJECTED' AND [ClosedById] IS NOT NULL AND [ClosedById] = [SellerId]) OR ([Status] = 'CANCELLED' AND [ClosedById] IS NOT NULL AND [ClosedById] IN ([BuyerId],[SellerId]))))");
                t.HasCheckConstraint("CK_Pickup_CodeAttempts", "[FailedCodeAttempts] BETWEEN 0 AND 5 AND ([CodeLockedUntil] IS NULL OR ([AcceptedAt] IS NOT NULL AND [CodeLockedUntil] >= [AcceptedAt]))");
                t.HasCheckConstraint("CK_Pickup_Coordinates", "([StoreLatitudeSnapshot] IS NULL AND [StoreLongitudeSnapshot] IS NULL) OR ([StoreLatitudeSnapshot] IS NOT NULL AND [StoreLongitudeSnapshot] IS NOT NULL AND [StoreLatitudeSnapshot] BETWEEN -90 AND 90 AND [StoreLongitudeSnapshot] BETWEEN -180 AND 180)");
            });
        });
        modelBuilder.Entity<Product>(e =>
        {
            // Id also participates in this FK; retain identity generation explicitly.
            e.Property(p => p.Id).UseIdentityColumn();
            // Composite FK prevents pointing at another product's reservation.
            e.HasOne<PickupReservation>().WithMany().HasForeignKey(p => new { p.ReservedPickupReservationId, p.Id })
                .HasPrincipalKey(r => new { r.Id, r.ProductId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("Products", "store", t => t.HasCheckConstraint("CK_Products_ReservationOwner",
                "[ReservedPickupReservationId] IS NULL OR ([ReservedOrderId] IS NULL AND [Status] = 'LOCKED')"));
        });
    }
}
