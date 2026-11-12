using PCBuilder.Domain;
using PCBuilder.Services;
var check=new CompatibilityChecker();
void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
var b=new PCBuild { Cpu=new(){Socket="AM4",TdpWatts=90}, Motherboard=new(){Socket="AM4",RamType="DDR4",RamSlots=2,MaxRamGB=32,MaxRamSpeedMHz=3200,FormFactor="mATX",M2Slots=1,SataPorts=1}, Gpu=new(){TdpWatts=220,LengthMm=200}, Ram=new(){RamType="DDR4",ModuleCount=2,CapacityGB=16,SpeedMHz=3200}, Case=new(){SupportedFormFactors=["mATX"],MaxGpuLengthMm=320,MaxCoolerHeightMm=165}, Cooler=new(){SupportedSockets=["AM4"],HeightMm=152,MaxTdpWatts=180}, Storage=new(){StorageType="NVMe"}, Psu=new(){RatedWattage=500} };
Assert(check.EvaluateBuild(b).Status==CompatibilityStatus.Compatible,"TC-01, 06, 07, 08, 09 baseline");
b.Cpu.Socket="AM5";Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-01")),"TC-02");b.Cpu.Socket="AM4";
b.Ram.RamType="DDR5";Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-02")),"TC-03");b.Ram.RamType="DDR4";
b.Ram.ModuleCount=4;Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-03")),"TC-04");b.Ram.ModuleCount=2;
b.Ram.SpeedMHz=3600;Assert(check.EvaluateBuild(b).Warnings.Any(x=>x.Contains("R-04")),"TC-05");b.Ram.SpeedMHz=3200;
Assert(check.EvaluateBuild(b).RecommendedPsuWatts==481,"TC-09 power");b.Psu.RatedWattage=400;Assert(check.EvaluateBuild(b).Status==CompatibilityStatus.Warning,"TC-09 warning");b.Psu.RatedWattage=350;Assert(check.EvaluateBuild(b).Status==CompatibilityStatus.Incompatible,"TC-09 error");
b.Psu.RatedWattage=500;
b.Case.SupportedFormFactors=["ATX"];Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-05")),"R-05");b.Case.SupportedFormFactors=["mATX"];
b.Gpu.LengthMm=321;Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-06")),"R-06");b.Gpu.LengthMm=200;
b.Cooler.HeightMm=166;Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-07")),"R-07 height");b.Cooler.HeightMm=152;
b.Cooler.SupportedSockets=["AM5"];Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-07")),"R-07 socket");b.Cooler.SupportedSockets=["AM4"];
b.Cooler.MaxTdpWatts=89;Assert(check.EvaluateBuild(b).Warnings.Any(x=>x.Contains("R-08")),"R-08");b.Cooler.MaxTdpWatts=180;
b.Motherboard.M2Slots=0;Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-09")),"R-09 NVMe");b.Motherboard.M2Slots=1;
b.Storage.StorageType="SATA";b.Motherboard.SataPorts=0;Assert(check.EvaluateBuild(b).Issues.Any(x=>x.Contains("R-09")),"R-09 SATA");b.Motherboard.SataPorts=1;b.Storage.StorageType="NVMe";
Assert(check.EvaluateBuild(new PCBuild()).EstimatedPowerWatts==0,"empty power");
Assert(check.EvaluateBuild(new PCBuild{Psu=new(){RatedWattage=1}}).EstimatedPowerWatts==0,"partial power");
b.Cpu.PricePKR=24000;b.Cpu.CustomPricePKR=23500;var path=Path.GetTempFileName();try{var service=new CatalogService();service.SaveBuildToJson(b,path);var restored=service.LoadBuildFromJson(path);Assert(restored.TotalPricePKR()==23500 && restored.Cpu?.CustomPricePKR==23500,"TC-10 persistence");}finally{File.Delete(path);} Console.WriteLine("All tests passed.");

var catalog = new CatalogService();
catalog.LoadCatalogFromJson(Path.Combine(AppContext.BaseDirectory,"Data","hardware_catalog.json"));
Assert(Enum.GetValues<HardwareCategory>().All(c=>catalog.FilterByCategory(c).Any()),"all catalog categories load");
Assert(catalog.FilterByCategory(HardwareCategory.CPU, "1600X", "AMD", 25000, "AM4").Count()==1,"combined filters");
var selectedCpu = (CpuComponent)CatalogService.Clone(catalog.FilterByCategory(HardwareCategory.CPU, "1600X").First());
selectedCpu.CustomPricePKR = 1;
Assert(catalog.FilterByCategory(HardwareCategory.CPU, "1600X").First().EffectivePricePKR==24000,"clone isolates price overrides");
var reference = new PCBuild();
foreach(var category in Enum.GetValues<HardwareCategory>()) {
 string name=category switch { HardwareCategory.CPU=>"1600X", HardwareCategory.Motherboard=>"B450M", HardwareCategory.GPU=>"RX 6600", HardwareCategory.RAM=>"Vengeance", HardwareCategory.Storage=>"NV2", HardwareCategory.PSU=>"CV550", HardwareCategory.Case=>"Matrexx", _=>"Hyper 212" };
 reference.Set(category,CatalogService.Clone(catalog.FilterByCategory(category,name).First()));
}
Assert(reference.TotalPricePKR()==169000,"reference catalog total");
Assert(check.EvaluateBuild(reference).Status==CompatibilityStatus.Compatible,"reference build compatibility");
Console.WriteLine("Catalog and reference build tests passed.");

var tempDir=Path.Combine(Path.GetTempPath(), "pcbuilder-test-"+Guid.NewGuid());
try {
 var customPath=Path.Combine(tempDir,"custom.json");
 var customCatalog=new CatalogService(customPath);
 customCatalog.LoadCatalogFromJson(Path.Combine(AppContext.BaseDirectory,"Data","hardware_catalog.json"));
 customCatalog.AddCustom(new CpuComponent { Name="Local CPU", Brand="Local", Socket="AM4", CoreCount=4, BaseClockGHz=3, TdpWatts=65, PricePKR=5000 });
 var reopened=new CatalogService(customPath); reopened.LoadCatalogFromJson(Path.Combine(AppContext.BaseDirectory,"Data","hardware_catalog.json"));
 Assert(reopened.FilterByCategory(HardwareCategory.CPU,"Local CPU").Single() is CpuComponent { TdpWatts: 65, PricePKR: 5000 },"custom component persists across sessions");
} finally { if(Directory.Exists(tempDir)) Directory.Delete(tempDir,true); }

Assert(Enum.GetValues<HardwareCategory>().All(c=>catalog.FilterByCategory(c).Count()>=20),"at least 20 parts per category");
var generator=new BudgetBuildService();
foreach(var target in new decimal[]{150000,200000,350000}) {
 var suggestion=generator.Suggest(catalog.Parts,target);
 Assert(suggestion!=null && suggestion.GetSelectedComponents().Count()==8,"budget builder returns eight parts");
 Assert(check.EvaluateBuild(suggestion!).Status==CompatibilityStatus.Compatible,"budget builder avoids errors and warnings");
 Assert(Math.Abs(suggestion!.TotalPricePKR()-target)<=target*0.1m,"budget result within 10 percent for typical targets");
}
Assert(generator.Suggest(Array.Empty<HardwareComponent>(),200000)==null,"incomplete catalog returns no suggestion");
var generated=generator.Suggest(catalog.Parts,200000)!;
var originalPrice=catalog.Parts.First(p=>p.Id==generated.Cpu!.Id).EffectivePricePKR;
generated.Cpu!.CustomPricePKR=1;
Assert(catalog.Parts.First(p=>p.Id==generated.Cpu.Id).EffectivePricePKR==originalPrice,"budget build does not mutate catalog");
Console.WriteLine("Budget and catalog size tests passed.");

void Reject(Action action,string description) { try { action(); throw new Exception("Expected rejection: "+description); } catch(InvalidDataException) {} }
Reject(()=>ComponentValidator.Validate(new CpuComponent{PricePKR=-1}),"negative price");
Reject(()=>ComponentValidator.Validate(new CpuComponent{PricePKR=ComponentValidator.MaxPrice+1}),"excessive price");
Reject(()=>ComponentValidator.Validate(new CpuComponent{BaseClockGHz=double.NaN}),"NaN clock");
Reject(()=>ComponentValidator.Validate(new CaseComponent{SupportedFormFactors=null!}),"null form factors");
Reject(()=>ComponentValidator.Validate(new StorageComponent{Name="Drive",Brand="Test",StorageType="unknown",CapacityGB=500},true),"unknown storage type");
Reject(()=>ComponentValidator.Validate(new PsuComponent{Name="PSU",Brand="Test",RatedWattage=0},true),"zero PSU wattage");
Reject(()=>ComponentValidator.Validate(new CpuCoolerComponent{Name="Cooler",Brand="Test",HeightMm=100,MaxTdpWatts=100},true),"empty cooler sockets");
var invalidPath=Path.GetTempFileName();
try {
 var service=new CatalogService();
 File.WriteAllText(invalidPath,"{\"Cpu\":{\"Name\":\"Test\",\"Brand\":\"A\",\"PricePKR\":-10}}");
 Reject(()=>service.LoadBuildFromJson(invalidPath),"negative saved price");
 var before=File.ReadAllText(invalidPath);
 Reject(()=>service.SaveBuildToJson(new PCBuild{Cpu=new CpuComponent{PricePKR=-1}},invalidPath),"invalid build save");
 Assert(File.ReadAllText(invalidPath)==before,"invalid save preserves existing file");
} finally {File.Delete(invalidPath);}
var psuBoundary=new PCBuild{Cpu=new(){TdpWatts=90},Gpu=new(){TdpWatts=220},Psu=new(){RatedWattage=370}};
Assert(check.EvaluateBuild(psuBoundary).Status==CompatibilityStatus.Warning,"PSU exactly at estimated load warns");
psuBoundary.Psu.RatedWattage=481; Assert(check.EvaluateBuild(psuBoundary).Status==CompatibilityStatus.Compatible,"PSU exactly at recommendation passes");
Console.WriteLine("Validation and boundary tests passed.");
