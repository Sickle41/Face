using System;

namespace WarmachineClient.Api.Dto;

public class Army
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid FactionId { get; set; }
    public int PointLimit { get; set; }
    public Guid? OwnerAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
