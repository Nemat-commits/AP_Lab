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
b.Cpu.PricePKR=24000;b.Cpu.CustomPricePKR=23500;var path=Path.GetTempFileName();try{var service=new CatalogService();service.SaveBuildToJson(b,path);var restored=service.LoadBuildFromJson(path);Assert(restored.TotalPricePKR()==23500 && restored.Cpu?.CustomPricePKR==23500,"TC-10 persistence");}finally{File.Delete(path);} Console.WriteLine("All tests passed.");
