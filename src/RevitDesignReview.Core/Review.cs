namespace RevitDesignReview.Core;

public sealed record Review
{
    public required Guid Id { get; init; }
    public int SequenceNumber { get; init; }
    public required string ProjectId { get; init; }
    public required string Title { get; init; }
    public required string AuthorName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ModifiedAt { get; init; }
    public ReviewStatus Status { get; init; }
    public ReviewSource Source { get; init; }
    public required IReadOnlyList<ReviewElement> Elements { get; init; }
    public required ReviewViewpoint Viewpoint { get; init; }

    public string DisplayId => $"REV-{SequenceNumber:000000}";

    public static Review Create(
        string projectId,
        string title,
        string authorName,
        IReadOnlyList<ReviewElement> elements,
        ReviewViewpoint viewpoint,
        DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(viewpoint);

        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId.Trim(),
            Title = title.Trim(),
            AuthorName = authorName.Trim(),
            CreatedAt = timestamp,
            ModifiedAt = timestamp,
            Status = ReviewStatus.Open,
            Source = ReviewSource.Manual,
            Elements = elements.ToArray(),
            Viewpoint = viewpoint
        };
    }
}
