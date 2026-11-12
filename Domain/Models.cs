using System.Text.Json.Serialization;
namespace PCBuilder.Domain;
public enum HardwareCategory { CPU, Motherboard, GPU, RAM, Storage, PSU, Case, CpuCooler }
public enum CompatibilityStatus { Compatible, Warning, Incompatible }
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CpuComponent), "cpu")][JsonDerivedType(typeof(MotherboardComponent), "motherboard")][JsonDerivedType(typeof(GpuComponent), "gpu")][JsonDerivedType(typeof(RamComponent), "ram")][JsonDerivedType(typeof(StorageComponent), "storage")][JsonDerivedType(typeof(PsuComponent), "psu")][JsonDerivedType(typeof(CaseComponent), "case")][JsonDerivedType(typeof(CpuCoolerComponent), "cooler")]
public abstract class HardwareComponent {
 public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
 public string Name { get; set; } = ""; public string Brand { get; set; } = "";
 [JsonIgnore] public abstract HardwareCategory Category { get; }
 public decimal PricePKR { get; set; } public decimal? CustomPricePKR { get; set; }
 [JsonIgnore] public decimal EffectivePricePKR => CustomPricePKR ?? PricePKR;
 [JsonIgnore] public bool HasCustomPrice => CustomPricePKR.HasValue;
 public int Wattage { get; set; }
 public abstract string GetSummary();
 public override string ToString() => $"{Brand} {Name} — PKR {EffectivePricePKR:N0}";
}
public sealed class CpuComponent : HardwareComponent { public override HardwareCategory Category => HardwareCategory.CPU; public string Socket {get;set;}=""; public int CoreCount {get;set;} public double BaseClockGHz {get;set;} public int TdpWatts {get=>Wattage;set=>Wattage=value;} public override string GetSummary()=> $"{Socket} · {CoreCount} cores · {TdpWatts}W"; }
public sealed class MotherboardComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.Motherboard; public string Socket {get;set;}=""; public string Chipset {get;set;}=""; public string FormFactor {get;set;}=""; public string RamType {get;set;}=""; public int RamSlots {get;set;} public int MaxRamGB {get;set;} public int MaxRamSpeedMHz {get;set;} public int M2Slots {get;set;} public int SataPorts {get;set;} public override string GetSummary()=> $"{Socket} · {FormFactor} · {RamType}"; }
public sealed class GpuComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.GPU; public string Chipset {get;set;}=""; public int VramGB {get;set;} public int LengthMm {get;set;} public int TdpWatts {get=>Wattage;set=>Wattage=value;} public override string GetSummary()=> $"{VramGB}GB · {LengthMm}mm · {TdpWatts}W"; }
public sealed class RamComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.RAM; public string RamType {get;set;}=""; public int CapacityGB {get;set;} public int ModuleCount {get;set;} public int SpeedMHz {get;set;} public override string GetSummary()=> $"{CapacityGB}GB · {RamType} · {SpeedMHz}MHz"; }
public sealed class StorageComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.Storage; public string StorageType {get;set;}=""; public int CapacityGB {get;set;} public string InterfaceType {get;set;}=""; public override string GetSummary()=> $"{CapacityGB}GB · {StorageType} · {InterfaceType}"; }
public sealed class PsuComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.PSU; public int RatedWattage {get;set;} public string EfficiencyRating {get;set;}=""; public bool IsModular {get;set;} public override string GetSummary()=> $"{RatedWattage}W · {EfficiencyRating}"; }
public sealed class CaseComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.Case; public List<string> SupportedFormFactors {get;set;}=[]; public int MaxGpuLengthMm {get;set;} public int MaxCoolerHeightMm {get;set;} public override string GetSummary()=> $"GPU {MaxGpuLengthMm}mm · cooler {MaxCoolerHeightMm}mm"; }
public sealed class CpuCoolerComponent : HardwareComponent { public override HardwareCategory Category=>HardwareCategory.CpuCooler; public string CoolerType {get;set;}=""; public int HeightMm {get;set;} public List<string> SupportedSockets {get;set;}=[]; public int MaxTdpWatts {get;set;} public override string GetSummary()=> $"{CoolerType} · {HeightMm}mm · {MaxTdpWatts}W"; }
public sealed class PCBuild {
 public string BuildName {get;set;}="Custom Pakistani PC Build";
 public CpuComponent? Cpu {get;set;} public MotherboardComponent? Motherboard {get;set;} public GpuComponent? Gpu {get;set;} public RamComponent? Ram {get;set;} public StorageComponent? Storage {get;set;} public PsuComponent? Psu {get;set;} public CaseComponent? Case {get;set;} public CpuCoolerComponent? Cooler {get;set;}
 public IEnumerable<HardwareComponent> GetSelectedComponents()=>new HardwareComponent?[]{Cpu,Motherboard,Gpu,Ram,Storage,Psu,Case,Cooler}.OfType<HardwareComponent>();
 public decimal TotalPricePKR()=>GetSelectedComponents().Sum(x=>x.EffectivePricePKR);
 public void Set(HardwareCategory c, HardwareComponent? p) { switch(c) { case HardwareCategory.CPU: Cpu=(CpuComponent?)p; break; case HardwareCategory.Motherboard: Motherboard=(MotherboardComponent?)p; break; case HardwareCategory.GPU: Gpu=(GpuComponent?)p; break; case HardwareCategory.RAM: Ram=(RamComponent?)p; break; case HardwareCategory.Storage: Storage=(StorageComponent?)p; break; case HardwareCategory.PSU: Psu=(PsuComponent?)p; break; case HardwareCategory.Case: Case=(CaseComponent?)p; break; case HardwareCategory.CpuCooler: Cooler=(CpuCoolerComponent?)p; break; } }
 public void SetCustomPrice(HardwareCategory c, decimal? price) { var p=GetSelectedComponents().FirstOrDefault(x=>x.Category==c); if(p!=null) p.CustomPricePKR=price; }
}
public record CompatibilityResult(CompatibilityStatus Status, IReadOnlyList<string> Issues, IReadOnlyList<string> Warnings, int EstimatedPowerWatts, int RecommendedPsuWatts);
