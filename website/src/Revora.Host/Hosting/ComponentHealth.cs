namespace Revora.Host.Hosting;

public sealed record ComponentHealth(string Component, HealthState State, string Detail);

