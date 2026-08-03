namespace StrikeLauncher.Models;

public sealed class WorkshopAddonsResponse
{
    public List<WorkshopAddon> WorkshopAddons { get; set; } = new();
}

public sealed class WorkshopAddon
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
