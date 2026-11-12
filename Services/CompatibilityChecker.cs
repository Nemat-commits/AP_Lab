using PCBuilder.Domain;
namespace PCBuilder.Services;
public sealed class CompatibilityChecker {
 static bool Eq(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
 public int CalculateEstimatedPower(PCBuild b)=> b.Cpu is null && b.Gpu is null ? 0 : (b.Cpu?.TdpWatts??0)+(b.Gpu?.TdpWatts??0)+60;
 public int CalculateRecommendedPsu(int watts)=>(int)Math.Ceiling(watts*1.30m);
 public CompatibilityResult EvaluateBuild(PCBuild b) {
  List<string> e=[], w=[]; var cpu=b.Cpu; var m=b.Motherboard; var ram=b.Ram; var c=b.Case; var cooler=b.Cooler;
  if(cpu!=null && m!=null && !Eq(cpu.Socket,m.Socket)) e.Add("[R-01 / ERR_SOCKET] CPU and motherboard sockets do not match.");
  if(ram!=null && m!=null) { if(!Eq(ram.RamType,m.RamType)) e.Add("[R-02 / ERR_RAM_GEN] RAM generation does not match the motherboard."); if(ram.ModuleCount>m.RamSlots || ram.CapacityGB>m.MaxRamGB) e.Add("[R-03 / ERR_DIMM_CAP] RAM exceeds motherboard slot or capacity limits."); if(ram.SpeedMHz>m.MaxRamSpeedMHz) w.Add($"[R-04 / WRN_RAM_CLK] RAM may downclock to {m.MaxRamSpeedMHz} MHz."); }
  if(m!=null && c!=null && !c.SupportedFormFactors.Any(x=>Eq(x,m.FormFactor))) e.Add("[R-05 / ERR_FORM_FAC] Motherboard form factor does not fit the case.");
  if(b.Gpu!=null && c!=null && b.Gpu.LengthMm>c.MaxGpuLengthMm) e.Add("[R-06 / ERR_GPU_LEN] GPU exceeds case clearance.");
  if(cooler!=null && ((c!=null && cooler.HeightMm>c.MaxCoolerHeightMm) || (cpu!=null && !cooler.SupportedSockets.Any(x=>Eq(x,cpu.Socket))))) e.Add("[R-07 / ERR_CLR_FIT] Cooler height or socket is incompatible.");
  if(cooler!=null && cpu!=null && cooler.MaxTdpWatts<cpu.TdpWatts) w.Add("[R-08 / WRN_CLR_TDP] Cooler rating is below CPU TDP.");
  if(b.Storage!=null && m!=null && ((Eq(b.Storage.StorageType,"NVMe") && m.M2Slots<1)||(Eq(b.Storage.StorageType,"SATA") && m.SataPorts<1))) e.Add("[R-09 / ERR_STR_PORT] Motherboard lacks the required storage port.");
  int est=CalculateEstimatedPower(b), rec=CalculateRecommendedPsu(est);
  if(b.Psu!=null && est>0) { if(b.Psu.RatedWattage<est) e.Add($"[R-10 / PSU_HEADRM] PSU is below estimated load ({est} W)."); else if(b.Psu.RatedWattage<rec) w.Add($"[R-10 / PSU_HEADRM] PSU is below the recommended {rec} W."); }
  return new(e.Count>0?CompatibilityStatus.Incompatible:w.Count>0?CompatibilityStatus.Warning:CompatibilityStatus.Compatible,e,w,est,rec);
 }
}
