namespace SmartTaskManager.Domain.Common;

/// <summary>Raised when an operation would violate a domain rule.</summary>
public sealed class DomainException(string message) : Exception(message);
