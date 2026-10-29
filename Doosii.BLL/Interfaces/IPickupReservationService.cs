using Doosii.BLL.DTOs;

namespace Doosii.BLL.Interfaces;

public record PickupCreateResult(PickupReservationDto Reservation, bool Replayed);
public interface IPickupReservationService
{
    Task<PickupCreateResult> CreateAsync(int actorId, CreatePickupReservationRequest request);
    Task<PickupReservationDto> DetailAsync(int actorId, int id);
    Task<PickupReservationPage> ListAsync(int actorId, PickupReservationQuery query, bool shop);
    Task<PickupReservationDto> AcceptAsync(int actorId, int id, PickupVersionRequest request);
    Task<PickupReservationDto> RejectAsync(int actorId, int id, ClosePickupReservationRequest request);
    Task<PickupReservationDto> CancelAsync(int actorId, int id, ClosePickupReservationRequest request);
    Task<PickupCodeDto> CodeAsync(int actorId, int id);
    Task<PickupReservationDto> CompleteAsync(int actorId, int id, CompletePickupReservationRequest request);
}
