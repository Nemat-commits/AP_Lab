using System.IO;
using PCBuilder.Domain;
namespace PCBuilder.Services;

/// <summary>Rejects corrupt or unsafe data before it reaches the rule engine or JSON store.</summary>
public static class ComponentValidator
{
    public const decimal MaxPrice = 1_000_000_000m;
    public static void Validate(HardwareComponent p, bool requireIdentity = false)
    {
        if (p is null) throw new InvalidDataException("A component is missing.");
        if (p.Name is null || p.Brand is null || p.Id is null ||
            (requireIdentity && (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Brand))))
            throw new InvalidDataException("A component needs a valid name, brand and ID.");
        if (p.PricePKR < 0 || p.PricePKR > MaxPrice || p.CustomPricePKR is < 0 or > MaxPrice)
            throw new InvalidDataException("A component price must be between 0 and 1,000,000,000 PKR.");
        static bool Bad(int n) => n < 0 || n > 1_000_000;
        static bool BadList(List<string>? list) => list == null || list.Any(string.IsNullOrWhiteSpace);
        bool invalid = Bad(p.Wattage) || (p switch {
            CpuComponent x => x.Socket is null || Bad(x.CoreCount) || !double.IsFinite(x.BaseClockGHz) || x.BaseClockGHz < 0 || x.BaseClockGHz > 1000,
            MotherboardComponent x => x.Socket is null || x.Chipset is null || x.FormFactor is null || x.RamType is null || Bad(x.RamSlots) || Bad(x.MaxRamGB) || Bad(x.MaxRamSpeedMHz) || Bad(x.M2Slots) || Bad(x.SataPorts),
            GpuComponent x => x.Chipset is null || Bad(x.VramGB) || Bad(x.LengthMm),
            RamComponent x => x.RamType is null || Bad(x.CapacityGB) || Bad(x.ModuleCount) || Bad(x.SpeedMHz),
            StorageComponent x => x.StorageType is null || x.InterfaceType is null || Bad(x.CapacityGB),
            PsuComponent x => x.EfficiencyRating is null || Bad(x.RatedWattage),
            CaseComponent x => BadList(x.SupportedFormFactors) || Bad(x.MaxGpuLengthMm) || Bad(x.MaxCoolerHeightMm),
            CpuCoolerComponent x => x.CoolerType is null || BadList(x.SupportedSockets) || Bad(x.HeightMm) || Bad(x.MaxTdpWatts),
            _ => true
        });
        if (invalid) throw new InvalidDataException($"Invalid specifications for {p.Name}.");
        if (requireIdentity && p is StorageComponent storage && !new[] { "NVMe", "SATA" }.Contains(storage.StorageType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Storage type must be NVMe or SATA.");
    }
}
