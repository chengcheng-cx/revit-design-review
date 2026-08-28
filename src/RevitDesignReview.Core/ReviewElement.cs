namespace RevitDesignReview.Core;

public sealed record ReviewElement(
    string ModelReference,
    string ElementUniqueId,
    long ElementIdAtCreation,
    long? CategoryId,
    string? CategoryName,
    string DisplayNameAtCreation,
    string? LinkInstanceUniqueId = null);
