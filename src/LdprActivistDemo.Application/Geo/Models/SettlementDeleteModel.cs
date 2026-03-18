namespace LdprActivistDemo.Application.Geo.Models;

public sealed record SettlementDeleteModel(string RegionName, string SettlementName, string? TargetRegionName, string? TargetSettlementName);