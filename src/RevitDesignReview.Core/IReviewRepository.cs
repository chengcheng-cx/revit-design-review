namespace RevitDesignReview.Core;

public interface IReviewRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default);
    Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Review?> GetLatestAsync(string projectId, CancellationToken cancellationToken = default);
}
