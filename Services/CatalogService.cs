using System.Text.Json;
using PCBuilder.Domain;
namespace PCBuilder.Services;
public sealed class CatalogService {
 static readonly JsonSerializerOptions Options=new(){WriteIndented=true, PropertyNameCaseInsensitive=true};
 public List<HardwareComponent> Parts {get;private set;}=[];
 public string CustomPath {get;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PCBuilder","custom_parts.json");
 public void LoadCatalogFromJson(string path) { Parts=JsonSerializer.Deserialize<List<HardwareComponent>>(File.ReadAllText(path),Options) ?? []; if(File.Exists(CustomPath)) Parts.AddRange(JsonSerializer.Deserialize<List<HardwareComponent>>(File.ReadAllText(CustomPath),Options) ?? []); }
 public IEnumerable<HardwareComponent> FilterByCategory(HardwareCategory category,string search="",string brand="",decimal? maxPkr=null,string socket="") => Parts.Where(p=>p.Category==category && (string.IsNullOrWhiteSpace(search)||($"{p.Name} {p.Brand} {p.GetSummary()}".Contains(search,StringComparison.OrdinalIgnoreCase))) && (string.IsNullOrWhiteSpace(brand)||p.Brand.Contains(brand,StringComparison.OrdinalIgnoreCase)) && (!maxPkr.HasValue||p.EffectivePricePKR<=maxPkr) && (string.IsNullOrWhiteSpace(socket) || p switch { CpuComponent x=>x.Socket.Equals(socket,StringComparison.OrdinalIgnoreCase), MotherboardComponent x=>x.Socket.Equals(socket,StringComparison.OrdinalIgnoreCase), CpuCoolerComponent x=>x.SupportedSockets.Contains(socket,StringComparer.OrdinalIgnoreCase), _=>true })).OrderBy(p=>p.EffectivePricePKR);
 public void AddCustom(HardwareComponent p) { var custom=File.Exists(CustomPath)?JsonSerializer.Deserialize<List<HardwareComponent>>(File.ReadAllText(CustomPath),Options) ?? []:[]; custom.Add(p); Directory.CreateDirectory(Path.GetDirectoryName(CustomPath)!); File.WriteAllText(CustomPath,JsonSerializer.Serialize(custom,Options)); Parts.Add(p); }
 public static HardwareComponent Clone(HardwareComponent p)=>JsonSerializer.Deserialize<HardwareComponent>(JsonSerializer.Serialize<HardwareComponent>(p,Options),Options)!;
 public void SaveBuildToJson(PCBuild b,string path)=>File.WriteAllText(path,JsonSerializer.Serialize(b,Options));
 public PCBuild LoadBuildFromJson(string path)=>JsonSerializer.Deserialize<PCBuild>(File.ReadAllText(path),Options) ?? throw new InvalidDataException("Empty build file.");
}
