namespace Revora.Host.Hosting;

public enum HealthState
{
    Starting,
    Ready,
    Degraded,
    Draining,
    Stopped,
    Failed,
}

