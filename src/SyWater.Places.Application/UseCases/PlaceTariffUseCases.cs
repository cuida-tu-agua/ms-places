using SyWater.Places.Application.Ports.In;
using SyWater.Places.Application.Ports.Out;
using SyWater.Places.Application.Tariffs;
using SyWater.Places.Domain.Places;
using SyWater.Places.Domain.Tariffs;

namespace SyWater.Places.Application.UseCases;

public sealed class GetPlaceTariffsUseCase(IPlaceRepository places, IPlaceTariffRepository tariffs)
    : IGetPlaceTariffsUseCase
{
    public async Task<PlaceTariffsView> ExecuteAsync(Guid ownerId, Guid placeId, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(placeId, ownerId, ct)
                    ?? throw new PlaceNotFoundException(placeId);

        var history = (await tariffs.ListAsync(place.Id, ct))
            .Select(t => TariffView.From(t, place.Currency))
            .ToList();

        return new PlaceTariffsView(history.FirstOrDefault(), history);
    }
}

public sealed class SetManualTariffUseCase(IPlaceRepository places, IPlaceTariffRepository tariffs, TimeProvider clock)
    : ISetManualTariffUseCase
{
    public async Task<TariffView> ExecuteAsync(SetManualTariffCommand command, CancellationToken ct)
    {
        var place = await places.GetActiveAsync(command.PlaceId, command.OwnerId, ct)
                    ?? throw new PlaceNotFoundException(command.PlaceId);

        var now = clock.GetUtcNow().UtcDateTime;
        var tariff = PlaceTariff.Manual(place.Id, command.UnitPricePerM3, command.FixedMonthlyCharge, command.OwnerId, now);

        // Same prices as the tariff in force: do not fill the history with duplicates
        var current = await tariffs.GetInForceAsync(place.Id, now, ct);
        if (current is not null && current.HasSameManualPrices(command.UnitPricePerM3, command.FixedMonthlyCharge))
            return TariffView.From(current, place.Currency);

        var saved = await tariffs.AddAsync(tariff, ct);
        return TariffView.From(saved, place.Currency);
    }
}
