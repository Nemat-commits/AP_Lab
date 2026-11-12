using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PCBuilder.Domain;
using PCBuilder.Services;
namespace PCBuilder.Views;
public partial class CustomPartWindow : Window {
 public HardwareComponent? Part {get;private set;} readonly Dictionary<PropertyInfo,TextBox> inputs=[];
 static readonly Dictionary<HardwareCategory,Type> Types=new(){[HardwareCategory.CPU]=typeof(CpuComponent),[HardwareCategory.Motherboard]=typeof(MotherboardComponent),[HardwareCategory.GPU]=typeof(GpuComponent),[HardwareCategory.RAM]=typeof(RamComponent),[HardwareCategory.Storage]=typeof(StorageComponent),[HardwareCategory.PSU]=typeof(PsuComponent),[HardwareCategory.Case]=typeof(CaseComponent),[HardwareCategory.CpuCooler]=typeof(CpuCoolerComponent)};
 public CustomPartWindow(){InitializeComponent();CategoryBox.ItemsSource=Enum.GetValues<HardwareCategory>();CategoryBox.SelectedIndex=0;}
 void CategoryChanged(object sender,SelectionChangedEventArgs e){if(CategoryBox.SelectedItem is not HardwareCategory category)return; Fields.Children.Clear();inputs.Clear();foreach(var p in Types[category].GetProperties().Where(p=>p.CanWrite && p.Name is not ("Id" or "CustomPricePKR" or "Wattage"))){Fields.Children.Add(new TextBlock{Text=p.Name+(p.PropertyType==typeof(List<string>)?" (comma separated)":p.PropertyType==typeof(bool)?" (true / false)":""),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,5,0,0)});var box=new TextBox();inputs[p]=box;Fields.Children.Add(box);}}
 void Save_Click(object sender,RoutedEventArgs e){try{var part=(HardwareComponent)Activator.CreateInstance(Types[(HardwareCategory)CategoryBox.SelectedItem])!;foreach(var (p,box) in inputs){string value=box.Text.Trim();if(value.Length==0)throw new ArgumentException($"{p.Name} is required.");object parsed;if(p.PropertyType==typeof(string))parsed=value;else if(p.PropertyType==typeof(List<string>)){var list=value.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();if(list.Count==0)throw new ArgumentException($"{p.Name} is required.");parsed=list;}else if(p.PropertyType==typeof(bool))parsed=bool.Parse(value);else {parsed=Convert.ChangeType(value,p.PropertyType,CultureInfo.InvariantCulture);if(Convert.ToDecimal(parsed)<0)throw new ArgumentException($"{p.Name} cannot be negative.");}p.SetValue(part,parsed);} if(part is StorageComponent storage && !new[]{"NVMe","SATA"}.Contains(storage.StorageType,StringComparer.OrdinalIgnoreCase))throw new ArgumentException("StorageType must be NVMe or SATA.");ComponentValidator.Validate(part,true);Part=part;DialogResult=true;}catch(Exception ex){MessageBox.Show(ex.Message,"Check component details");}}
}
