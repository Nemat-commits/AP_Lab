using System.Windows;
using PCBuilder.ViewModels;
namespace PCBuilder;
public partial class MainWindow : Window { public MainWindow(){InitializeComponent(); try {DataContext=new MainViewModel();} catch(Exception ex){MessageBox.Show(ex.Message,"Catalog error");Close();}} private void ClearSlot_Click(object sender,RoutedEventArgs e){if(((FrameworkElement)sender).DataContext is SlotSelectorViewModel slot)slot.Selected=null;} }
