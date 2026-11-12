using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
            cpu.Selected = cpu.Choices.First(p => p.Name.Contains("1600X"));
            Pump();
            Assert(vm.Total.Contains("24,000"), "CPU selection updates total");
            Assert(vm.Estimated == "155 W", "CPU selection updates power");
            cpu.Price = "23000";
            Pump();
            Assert(vm.Total.Contains("23,000"), "Price override updates total");
            vm.Search = "7600X";
            Pump();
            Assert(cpu.Selected?.Name.Contains("1600X") == true, "Filtering must preserve the selected part");
            Assert(vm.Total.Contains("23,000"), "Filtering must preserve the override");
            cpu.Price = "";
            Assert(vm.Total.Contains("24,000"), "Clearing an override restores the catalog price");
            cpu.Selected = null;
            Pump();
            Assert(vm.Total.EndsWith("0"), "Clearing a slot updates total");
            vm.NewCommand.Execute(null);
            Assert(vm.Slots.All(s => s.Selected == null), "New build clears slots");
            Console.WriteLine("WPF window and selection smoke tests passed.");
        }
        finally { window.Close(); app.Shutdown(); }
    }
}
