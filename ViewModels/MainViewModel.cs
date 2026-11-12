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
 public bool HasInvalidPrice => selected!=null && !string.IsNullOrWhiteSpace(price) && (!decimal.TryParse(price,out var amount) || amount<0 || amount>ComponentValidator.MaxPrice);
 public string PriceHint=>HasInvalidPrice?"Enter 0–1,000,000,000 PKR":"Blank = catalog price";
 public string Summary=>selected?.GetSummary()??"No component selected"; public string CatalogPrice=>selected is null?"—":$"PKR {selected.PricePKR:N0}";
 public string Price {get=>price;set {if(price==value)return; price=value; owner.MarkChanged(); Notify(); Notify(nameof(PriceHint)); Notify(nameof(HasInvalidPrice)); if(selected==null)return; if(string.IsNullOrWhiteSpace(value))selected.CustomPricePKR=null; else if(decimal.TryParse(value,out var n)&&n>=0 && n<=ComponentValidator.MaxPrice)selected.CustomPricePKR=n; owner.Recalculate(); }}
 public void Restore(HardwareComponent? value){selected=value;price=value?.CustomPricePKR?.ToString()??"";Notify(nameof(Price));Notify(nameof(Summary));Notify(nameof(CatalogPrice));Notify(nameof(PriceHint));Notify(nameof(HasInvalidPrice));}
 public void ClearPrice(){Price="";} public void NotifySelection()=>Notify(nameof(Selected));
}
public sealed class MainViewModel : Observable {
 readonly CatalogService catalog=new(); readonly CompatibilityChecker checker=new(); PCBuild build=new(); string search="",brand="",socket="",budget=""; CompatibilityResult result;
 public ObservableCollection<SlotSelectorViewModel> Slots {get;}=[];
 public ICommand ClearFiltersCommand {get;} public ICommand BudgetBuildCommand {get;} public ICommand NewCommand {get;} public ICommand SaveCommand {get;} public ICommand LoadCommand {get;} public ICommand CustomCommand {get;}
 public MainViewModel(){catalog.LoadCatalogFromJson(Path.Combine(AppContext.BaseDirectory,"Data","hardware_catalog.json")); foreach(var c in Enum.GetValues<HardwareCategory>()) Slots.Add(new(this,c)); result=checker.EvaluateBuild(build); ClearFiltersCommand=new RelayCommand(ClearFilters); BudgetBuildCommand=new RelayCommand(async ()=>await GenerateBudgetBuildAsync()); NewCommand=new RelayCommand(New); SaveCommand=new RelayCommand(Save); LoadCommand=new RelayCommand(Load); CustomCommand=new RelayCommand(Custom); RefreshChoices();}
 public string Search {get=>search;set{search=value;Notify();RefreshChoices();}} public string Brand {get=>brand;set{brand=value;Notify();RefreshChoices();}} public string Socket {get=>socket;set{socket=value;Notify();RefreshChoices();}} public string Budget {get=>budget;set{budget=value;Notify();RefreshChoices();}}
 string targetBudget="200000", budgetMessage="Uses all catalog parts, ignoring filters. Finds a compatible build close to your target.";
 public string TargetBudget {get=>targetBudget;set{if(targetBudget==value)return;targetBudget=value;MarkChanged();Notify();if(!IsGenerating) BudgetMessage="Target changed. Generate a new suggestion to update your build.";}}
 public string BudgetMessage {get=>budgetMessage;set{budgetMessage=value;Notify();}}
 int stateVersion; bool generating;
 public bool IsGenerating {get=>generating;private set{generating=value;Notify();Notify(nameof(GenerateButtonText));Notify(nameof(CanGenerate));}}
 public bool CanGenerate=>!IsGenerating;
 public string GenerateButtonText=>IsGenerating?"Searching…":"Generate compatible build";
 public void MarkChanged()=>stateVersion++;
 public Func<bool> ConfirmClear {get;set;} = () => MessageBox.Show("Clear all selected parts, price overrides, filters and the budget? Saved files and custom catalog parts will not be deleted.", "Clear everything?", MessageBoxButton.YesNo, MessageBoxImage.Question)==MessageBoxResult.Yes;
 public Func<bool> ConfirmReplace {get;set;} = () => MessageBox.Show("Generating a build replaces your current selections. Save them first if needed. Continue?", "Replace current build?", MessageBoxButton.YesNo, MessageBoxImage.Question)==MessageBoxResult.Yes;
 public async Task GenerateBudgetBuildAsync(){
  if(IsGenerating)return;
  if(!decimal.TryParse(TargetBudget,out var target) || target<1 || target>ComponentValidator.MaxPrice){BudgetMessage="Enter a budget between 1 and 1,000,000,000 PKR.";return;}
  if(Slots.Any(s=>s.HasInvalidPrice)){BudgetMessage="Correct invalid prices before generating a build.";return;}
  if(build.GetSelectedComponents().Any() && !ConfirmReplace())return;
  int version=stateVersion; IsGenerating=true; BudgetMessage="Searching compatible combinations…";
  try {
   var parts=catalog.Parts.Select(CatalogService.Clone).ToArray();
   var suggestion=await Task.Run(()=>new BudgetBuildService().Suggest(parts,target)).ConfigureAwait(false);
   await Application.Current.Dispatcher.InvokeAsync(()=>{
    if(version!=stateVersion){BudgetMessage="Your build or target changed during the search. No changes were applied.";return;}
    if(suggestion==null){BudgetMessage="No fully compatible eight-part build was found. Your current build was not changed.";return;}
    ApplyBuild(suggestion); var delta=suggestion.TotalPricePKR()-target; var proximity=Math.Abs(delta)<=target*0.10m?"Within 10%":"Outside 10%; closest sampled match";
    BudgetMessage=$"{proximity}: PKR {suggestion.TotalPricePKR():N0} ({(delta>=0?"+":"−")}PKR {Math.Abs(delta):N0}). Prices are estimates; check local quotes.";
   });
  } catch(Exception ex){await Application.Current.Dispatcher.InvokeAsync(()=>BudgetMessage=$"Could not generate a build: {ex.Message}");} finally {await Application.Current.Dispatcher.InvokeAsync(()=>IsGenerating=false);}
 }

 public string BuildName {get=>build.BuildName;set{if(build.BuildName==value)return;build.BuildName=value;MarkChanged();Notify();}}
 public string SlotProgress=>$"{build.GetSelectedComponents().Count()} of 8 parts selected";
 public string Status=>result.Status.ToString().ToUpperInvariant(); public string StatusColor=>result.Status switch {CompatibilityStatus.Compatible=>"#059669",CompatibilityStatus.Warning=>"#D97706",_=>"#DC2626"};
 public string StatusDetail=>result.Issues.Count+result.Warnings.Count==0 ? (build.GetSelectedComponents().Count()==8?"No compatibility issues found.":"No issues found so far · select all eight components for a complete build.") : $"{result.Issues.Count} issue(s) · {result.Warnings.Count} advisory(s)";
 public IEnumerable<string> Diagnostics=>result.Issues.Concat(result.Warnings); public string Estimated=>$"{result.EstimatedPowerWatts} W"; public string Recommended=>$"{result.RecommendedPsuWatts} W"; public string Total=>Slots.Any(s=>s.HasInvalidPrice)?"Invalid price":$"PKR {build.TotalPricePKR():N0}"; public int PowerPercent=>build.Psu is null||build.Psu.RatedWattage<=0?0:Math.Min(100,(int)(100.0*result.EstimatedPowerWatts/build.Psu.RatedWattage));
 public void UpdateSlot(SlotSelectorViewModel slot){build.Set(slot.Category,slot.Selected);Recalculate();}
 public void Recalculate(){MarkChanged(); result=checker.EvaluateBuild(build); foreach(var n in new[]{nameof(SlotProgress),nameof(Status),nameof(StatusColor),nameof(StatusDetail),nameof(Diagnostics),nameof(Estimated),nameof(Recommended),nameof(Total),nameof(PowerPercent)})Notify(n);}
 void RefreshChoices(){decimal? max=decimal.TryParse(budget,out var n)&&n>=0?n:null; foreach(var s in Slots){s.Refreshing=true; s.Choices.Clear(); if(s.Selected!=null) s.Choices.Add(s.Selected); foreach(var p in catalog.FilterByCategory(s.Category,search,brand,max,socket)) if(p.Id!=s.Selected?.Id) s.Choices.Add(CatalogService.Clone(p)); s.Refreshing=false; s.NotifySelection();}}
 public void ApplyBuild(PCBuild loaded){build=loaded;foreach(var s in Slots){s.Refreshing=true;s.Restore(build.GetSelectedComponents().FirstOrDefault(p=>p.Category==s.Category));}Notify(nameof(BuildName));RefreshChoices();Recalculate();}
 void ClearFilters(){search="";brand="";socket="";budget="";foreach(var name in new[]{nameof(Search),nameof(Brand),nameof(Socket),nameof(Budget)})Notify(name);RefreshChoices();}
 void New(){if(!ConfirmClear())return; ClearFilters(); TargetBudget="200000"; BudgetMessage="Enter your budget and generate a compatible starting build."; ApplyBuild(new PCBuild());}
 void Save(){try{if(Slots.Any(s=>s.HasInvalidPrice)){MessageBox.Show("Correct invalid price entries before saving.","Invalid price");return;}var d=new SaveFileDialog{Filter="Build JSON (*.json)|*.json",FileName="pc-build.json"};if(d.ShowDialog()==true)catalog.SaveBuildToJson(build,d.FileName);}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save build");}}
 void Load(){try{var d=new OpenFileDialog{Filter="Build JSON (*.json)|*.json"};if(d.ShowDialog()!=true)return;var loaded=catalog.LoadBuildFromJson(d.FileName);ApplyBuild(loaded);}catch(Exception ex){MessageBox.Show(ex.Message,"Could not load build");}}
 void Custom(){try{var dialog=new CustomPartWindow{Owner=Application.Current.MainWindow};if(dialog.ShowDialog()==true && dialog.Part!=null){catalog.AddCustom(dialog.Part);var slot=Slots.First(x=>x.Category==dialog.Part.Category);var part=CatalogService.Clone(dialog.Part);slot.Choices.Add(part);slot.Selected=part;RefreshChoices();}}catch(Exception ex){MessageBox.Show(ex.Message,"Could not add part");}}
}
