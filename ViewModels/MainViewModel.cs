using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PCBuilder.Domain;
using PCBuilder.Services;
using PCBuilder.Views;
namespace PCBuilder.ViewModels;
public abstract class Observable : INotifyPropertyChanged { public event PropertyChangedEventHandler? PropertyChanged; protected void Notify([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name)); }
public sealed class RelayCommand(Action action) : ICommand { public event EventHandler? CanExecuteChanged { add {} remove {} } public bool CanExecute(object? p)=>true; public void Execute(object? p)=>action(); }
public sealed class SlotSelectorViewModel : Observable {
 readonly MainViewModel owner; HardwareComponent? selected; string price=""; internal bool Refreshing;
 public HardwareCategory Category {get;} public string Label => Category==HardwareCategory.CpuCooler?"CPU cooler":Category.ToString();
 public ObservableCollection<HardwareComponent> Choices {get;}=[];
 public SlotSelectorViewModel(MainViewModel owner,HardwareCategory category){this.owner=owner;Category=category;}
 public HardwareComponent? Selected {get=>selected;set { if(Refreshing || ReferenceEquals(selected,value))return; selected=value; price=selected?.CustomPricePKR?.ToString()??""; Notify();Notify(nameof(Price));Notify(nameof(Summary));Notify(nameof(CatalogPrice));Notify(nameof(PriceHint));Notify(nameof(HasInvalidPrice)); owner.UpdateSlot(this); }}
 public bool HasInvalidPrice => selected!=null && !string.IsNullOrWhiteSpace(price) && (!decimal.TryParse(price,out var amount) || amount<0);
 public string PriceHint=>HasInvalidPrice?"Enter a non-negative amount":"Blank = catalog price";
 public string Summary=>selected?.GetSummary()??"No component selected"; public string CatalogPrice=>selected is null?"—":$"PKR {selected.PricePKR:N0}";
 public string Price {get=>price;set {price=value; Notify(); Notify(nameof(PriceHint)); Notify(nameof(HasInvalidPrice)); if(selected==null)return; if(string.IsNullOrWhiteSpace(value)){selected.CustomPricePKR=null; owner.Recalculate();} else if(decimal.TryParse(value,out var n)&&n>=0){selected.CustomPricePKR=n;owner.Recalculate();} }}
 public void Restore(HardwareComponent? value){selected=value;price=value?.CustomPricePKR?.ToString()??"";Notify(nameof(Selected));Notify(nameof(Price));Notify(nameof(Summary));Notify(nameof(CatalogPrice));Notify(nameof(PriceHint));Notify(nameof(HasInvalidPrice));}
 public void ClearPrice(){Price="";} public void NotifySelection()=>Notify(nameof(Selected));
}
public sealed class MainViewModel : Observable {
 readonly CatalogService catalog=new(); readonly CompatibilityChecker checker=new(); PCBuild build=new(); string search="",brand="",socket="",budget=""; CompatibilityResult result;
 public ObservableCollection<SlotSelectorViewModel> Slots {get;}=[];
 public ICommand NewCommand {get;} public ICommand SaveCommand {get;} public ICommand LoadCommand {get;} public ICommand CustomCommand {get;}
 public MainViewModel(){catalog.LoadCatalogFromJson(Path.Combine(AppContext.BaseDirectory,"Data","hardware_catalog.json")); foreach(var c in Enum.GetValues<HardwareCategory>()) Slots.Add(new(this,c)); result=checker.EvaluateBuild(build); NewCommand=new RelayCommand(New); SaveCommand=new RelayCommand(Save); LoadCommand=new RelayCommand(Load); CustomCommand=new RelayCommand(Custom); RefreshChoices();}
 public string Search {get=>search;set{search=value;Notify();RefreshChoices();}} public string Brand {get=>brand;set{brand=value;Notify();RefreshChoices();}} public string Socket {get=>socket;set{socket=value;Notify();RefreshChoices();}} public string Budget {get=>budget;set{budget=value;Notify();RefreshChoices();}}
 public string BuildName {get=>build.BuildName;set{build.BuildName=value;Notify();}}
 public string Status=>result.Status.ToString().ToUpperInvariant(); public string StatusColor=>result.Status switch {CompatibilityStatus.Compatible=>"#059669",CompatibilityStatus.Warning=>"#D97706",_=>"#DC2626"};
 public string StatusDetail=>result.Issues.Count+result.Warnings.Count==0 ? (build.GetSelectedComponents().Count()==8?"No compatibility issues found.":"No issues found so far · select all eight components for a complete build.") : $"{result.Issues.Count} issue(s) · {result.Warnings.Count} advisory(s)";
 public IEnumerable<string> Diagnostics=>result.Issues.Concat(result.Warnings); public string Estimated=>$"{result.EstimatedPowerWatts} W"; public string Recommended=>$"{result.RecommendedPsuWatts} W"; public string Total=>$"PKR {build.TotalPricePKR():N0}"; public int PowerPercent=>build.Psu is null||build.Psu.RatedWattage<=0?0:Math.Min(100,(int)(100.0*result.EstimatedPowerWatts/build.Psu.RatedWattage));
 public void UpdateSlot(SlotSelectorViewModel slot){build.Set(slot.Category,slot.Selected);Recalculate();}
 public void Recalculate(){result=checker.EvaluateBuild(build); foreach(var n in new[]{nameof(Status),nameof(StatusColor),nameof(StatusDetail),nameof(Diagnostics),nameof(Estimated),nameof(Recommended),nameof(Total),nameof(PowerPercent)})Notify(n);}
 void RefreshChoices(){decimal? max=decimal.TryParse(budget,out var n)&&n>=0?n:null; foreach(var s in Slots){s.Refreshing=true; s.Choices.Clear(); if(s.Selected!=null) s.Choices.Add(s.Selected); foreach(var p in catalog.FilterByCategory(s.Category,search,brand,max,socket)) if(p.Id!=s.Selected?.Id) s.Choices.Add(CatalogService.Clone(p)); s.Refreshing=false; s.NotifySelection();}}
 public void ApplyBuild(PCBuild loaded){build=loaded;foreach(var s in Slots){s.Refreshing=true;s.Restore(build.GetSelectedComponents().FirstOrDefault(p=>p.Category==s.Category));}Notify(nameof(BuildName));RefreshChoices();Recalculate();}
 void New()=>ApplyBuild(new PCBuild());
 void Save(){try{if(Slots.Any(s=>s.HasInvalidPrice)){MessageBox.Show("Correct invalid price entries before saving.","Invalid price");return;}var d=new SaveFileDialog{Filter="Build JSON (*.json)|*.json",FileName="pc-build.json"};if(d.ShowDialog()==true)catalog.SaveBuildToJson(build,d.FileName);}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save build");}}
 void Load(){try{var d=new OpenFileDialog{Filter="Build JSON (*.json)|*.json"};if(d.ShowDialog()!=true)return;var loaded=catalog.LoadBuildFromJson(d.FileName);ApplyBuild(loaded);}catch(Exception ex){MessageBox.Show(ex.Message,"Could not load build");}}
 void Custom(){try{var dialog=new CustomPartWindow{Owner=Application.Current.MainWindow};if(dialog.ShowDialog()==true && dialog.Part!=null){catalog.AddCustom(dialog.Part);var slot=Slots.First(x=>x.Category==dialog.Part.Category);var part=CatalogService.Clone(dialog.Part);slot.Choices.Add(part);slot.Selected=part;RefreshChoices();}}catch(Exception ex){MessageBox.Show(ex.Message,"Could not add part");}}
}
