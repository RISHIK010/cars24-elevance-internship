using Cars24API.Models;

namespace Cars24API.Services
{
    // Deterministic maintenance estimate for a car listing, computed at read
    // time from age, kilometers driven, brand and model. These are internship
    // estimates and are NOT live service quotes.
    public record MaintenanceEstimate(
        double EstimatedMonthlyMaintenance,
        string MaintenanceStatus,
        IReadOnlyList<string> MaintenanceInsights);

    // Computes a deterministic maintenance-cost estimate for a car.
    public interface IMaintenanceService
    {
        MaintenanceEstimate Estimate(Car car, DateTime? asOf = null);
    }
}
