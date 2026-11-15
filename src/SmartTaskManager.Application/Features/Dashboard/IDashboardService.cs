namespace SmartTaskManager.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
}
