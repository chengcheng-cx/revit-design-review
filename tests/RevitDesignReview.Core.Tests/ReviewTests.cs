using RevitDesignReview.Core;

namespace RevitDesignReview.Core.Tests;

public sealed class ReviewTests
{
    [Fact]
    public void Create_trims_values_and_starts_open()
    {
        var review = Review.Create(
            " project ",
            "  Duct intersects beam  ",
            " User A ",
            Array.Empty<ReviewElement>(),
            Viewpoint());

        Assert.Equal("project", review.ProjectId);
        Assert.Equal("Duct intersects beam", review.Title);
        Assert.Equal("User A", review.AuthorName);
        Assert.Equal(ReviewStatus.Open, review.Status);
        Assert.Equal(ReviewSource.Manual, review.Source);
    }

    [Fact]
    public void Create_rejects_blank_title()
    {
        Assert.Throws<ArgumentException>(() => Review.Create(
            "project",
            "  ",
            "User A",
            Array.Empty<ReviewElement>(),
            Viewpoint()));
    }

    private static ReviewViewpoint Viewpoint() =>
        new("view-1", "3D", true, false, null, null, null, null);
}
