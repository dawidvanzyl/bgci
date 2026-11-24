using Hangfire.Dashboard;

namespace Jobs.Worker;

// Hangfire's default filter refuses requests that arrive through a Docker port mapping; the 127.0.0.1 host binding is the access rule.
public sealed class DashboardAccessFilter : IDashboardAuthorizationFilter
{
	public bool Authorize(DashboardContext context) => true;
}