using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Data;
using PCBuilder.Services;
using System.IO;
using PCBuilder;
using PCBuilder.Domain;
using PCBuilder.ViewModels;

internal static class Program
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    [STAThread]
    private static void Main()
    {
        try { Run(); } catch (Exception ex) { Console.WriteLine($"::error::{ex.ToString().Replace("\r", " ").Replace("\n", " ")}"); Environment.ExitCode = 1; }
    }

    private static void Run()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        app.MainWindow = window;
        window.Show();
        Pump();
        try
        {
            var vm = (MainViewModel)window.DataContext;
            Assert(vm.Slots.Count == 8, "Eight slots should be shown");
            var cpu = vm.Slots.First(s => s.Category == HardwareCategory.CPU);
            var combo = Descendants<ComboBox>(window).First(c => ReferenceEquals(c.DataContext, cpu));
            combo.SelectedItem = cpu.Choices.First(p => p.Name.Contains("1600X"));
            Pump();
            Assert(vm.Total.Contains("24,000"), "CPU selection updates total");
            Assert(vm.Estimated == "155 W", "CPU selection updates power");
            var priceBox = Descendants<TextBox>(window).First(t => ReferenceEquals(t.DataContext, cpu));
            priceBox.Text = "23000";
            Pump();
            Assert(vm.Total.Contains("23,000"), "Price override updates total");
            priceBox.Text = "-1"; Pump();
            Assert(cpu.HasInvalidPrice && vm.Total == "Invalid price", "Invalid price is not silently included in the total");
            priceBox.Text = "23000"; Pump();
            vm.Search = "7600X";
            Pump();
            Assert(cpu.Selected?.Name.Contains("1600X") == true, "Filtering must preserve the selected part");
            Assert(vm.Total.Contains("23,000"), "Filtering must preserve the override");
            priceBox.Text = "";
            Pump();
            Assert(vm.Total.Contains("24,000"), "Clearing an override restores the catalog price");
            cpu.Selected = null;
            Pump();
            Assert(vm.Total == "PKR 0", "Clearing a slot updates total");
            var file = Path.GetTempFileName();
            try {
                var repository = new CatalogService();
                var saved = new PCBuild { Cpu = new CpuComponent { Name = "Saved", Brand = "Test", Socket = "AM4", PricePKR = 24000, CustomPricePKR = 21000, TdpWatts = 65 } };
                repository.SaveBuildToJson(saved, file);
                vm.ApplyBuild(repository.LoadBuildFromJson(file));
                Pump();
                Assert(cpu.Selected?.Name == "Saved" && vm.Total.Contains("21,000"), "Loading restores the selected item and override in the window");
                Assert(ReferenceEquals(combo.SelectedItem, cpu.Selected), "Loaded selection is visible in its dropdown");
            } finally { File.Delete(file); }
            vm.TargetBudget = "200000";
            vm.ConfirmReplace = () => false;
            var oldPart = cpu.Selected;
            vm.GenerateBudgetBuildAsync().GetAwaiter().GetResult();
            Assert(ReferenceEquals(cpu.Selected, oldPart), "Declining replacement preserves the current build");
            vm.ConfirmReplace = () => true;
            var staleGeneration = vm.GenerateBudgetBuildAsync();
            vm.BuildName = "Changed during search";
            while (!staleGeneration.IsCompleted) { Pump(); Thread.Sleep(5); }
            staleGeneration.GetAwaiter().GetResult();
            Assert(ReferenceEquals(cpu.Selected, oldPart) && vm.BudgetMessage.Contains("changed during"), "Stale budget result does not overwrite edits");
            var generation = vm.GenerateBudgetBuildAsync();
            while (!generation.IsCompleted) { Pump(); Thread.Sleep(5); }
            generation.GetAwaiter().GetResult();
            Pump();
            Assert(vm.Slots.All(s => s.Selected != null), "Budget builder fills all slots");
            Assert(vm.Status == "COMPATIBLE", "Generated build is compatible");
            Assert(vm.BudgetMessage.Contains("PKR"), "Budget result is displayed: " + vm.BudgetMessage);
            void AssertVisibleSelections(string phase)
            {
                foreach (var slot in vm.Slots)
                {
                    var selector = Descendants<ComboBox>(window).First(c => ReferenceEquals(c.DataContext, slot));
                    Assert(slot.Selected != null && ReferenceEquals(selector.SelectedItem, slot.Selected), $"{phase}: {slot.Label} dropdown shows its selected part");
                    Assert(slot.Choices.Contains(slot.Selected!), $"{phase}: {slot.Label} contains selection in options");
                }
            }
            AssertVisibleSelections("first suggestion");
            var firstNames = string.Join("|", vm.Slots.Select(s => s.Selected?.Id));
            var budgetBox = Descendants<TextBox>(window).First(t => BindingOperations.GetBinding(t, TextBox.TextProperty)?.Path?.Path == "TargetBudget");
            var generateButton = Descendants<Button>(window).First(b => ReferenceEquals(b.Command, vm.BudgetBuildCommand));
            budgetBox.Text = "350000"; Pump();
            Assert(vm.TargetBudget == "350000", "Budget textbox updates the target");
            generateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(vm.IsGenerating, "Generate button starts a new search");
            while (vm.IsGenerating) { Pump(); Thread.Sleep(5); }
            Pump();
            Assert(vm.BudgetMessage.Contains("PKR") && vm.Status == "COMPATIBLE", "Second budget suggestion succeeds: " + vm.BudgetMessage);
            AssertVisibleSelections("second suggestion");
            Assert(firstNames != string.Join("|", vm.Slots.Select(s => s.Selected?.Id)), "Different target can produce a different suggestion");
            vm.Brand = "AMD"; vm.Socket = "AM4"; vm.Budget = "50000";
            vm.ConfirmClear = () => false; vm.NewCommand.Execute(null);
            Assert(vm.Slots.Any(s => s.Selected != null), "Cancelled clear preserves build");
            vm.ConfirmClear = () => true;
            var clearButton = Descendants<Button>(window).First(b => ReferenceEquals(b.Command, vm.NewCommand));
            clearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Assert(vm.Slots.All(s => s.Selected == null), "Clear all clears every slot");
            Assert(vm.Search == "" && vm.Brand == "" && vm.Socket == "" && vm.Budget == "" && vm.TargetBudget == "200000", "Clear all resets filters and budget");
            Assert(vm.Total == "PKR 0" && vm.Estimated == "0 W" && vm.SlotProgress == "0 of 8 parts selected", "Clear all resets totals, power and progress");
            Assert(vm.Slots.All(s => s.Choices.Count >= 20), "Clear all restores unfiltered catalog options");
            Console.WriteLine("WPF window and selection smoke tests passed.");
        }
        finally { window.Close(); app.Shutdown(); }
    }
}
