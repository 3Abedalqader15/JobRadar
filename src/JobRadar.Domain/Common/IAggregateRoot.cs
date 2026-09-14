namespace JobRadar.Domain.Common;

/// <summary>
/// Marker interface for aggregate roots. Applied to entities that serve
/// as the consistency boundary for a cluster of objects.
/// </summary>
public interface IAggregateRoot { }
