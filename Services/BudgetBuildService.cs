using PCBuilder.Domain;

namespace PCBuilder.Services;

/// <summary>Searches a bounded, repeatable sample of fully compatible catalog combinations.
/// It is not a performance benchmark or a proof of the globally closest price.</summary>
public sealed class BudgetBuildService
{
    private readonly CompatibilityChecker checker = new();
    private static readonly HardwareCategory[] Order = [HardwareCategory.CPU, HardwareCategory.Motherboard,
        HardwareCategory.RAM, HardwareCategory.GPU, HardwareCategory.Case, HardwareCategory.CpuCooler,
        HardwareCategory.Storage, HardwareCategory.PSU];

    public PCBuild? Suggest(IEnumerable<HardwareComponent> catalog, decimal target, int trials = 2500)
    {
        if (target <= 0 || trials <= 0) throw new ArgumentOutOfRangeException(nameof(target));
        var pools = Order.ToDictionary(c => c, c => catalog.Where(p => p.Category == c && p.EffectivePricePKR >= 0).ToArray());
        if (pools.Any(p => p.Value.Length == 0)) return null;
        // Fixed seed makes the same inputs reproducible and keeps the UI responsive without a network service.
        var random = new Random(78123);
        PCBuild? best = null;
        decimal bestDistance = decimal.MaxValue;
        for (int trial = 0; trial < trials; trial++)
        {
            var build = new PCBuild { BuildName = $"Budget build - PKR {target:N0}" };
            foreach (var category in Order)
            {
                var choices = pools[category].Where(part => {
                    build.Set(category, part);
                    return checker.EvaluateBuild(build).Status == CompatibilityStatus.Compatible;
                }).ToArray();
                build.Set(category, null);
                if (choices.Length == 0) break;
                build.Set(category, choices[random.Next(choices.Length)]);
            }
            if (build.GetSelectedComponents().Count() != Order.Length) continue;
            var distance = Math.Abs(build.TotalPricePKR() - target);
            if (distance < bestDistance || (distance == bestDistance && best != null && build.TotalPricePKR() <= target && best.TotalPricePKR() > target))
            {
                bestDistance = distance;
                best = build;
                if (distance == 0) break;
            }
        }
        if (best == null) return null;
        var copy = new PCBuild { BuildName = best.BuildName };
        foreach (var part in best.GetSelectedComponents()) copy.Set(part.Category, CatalogService.Clone(part));
        return copy;
    }
}
