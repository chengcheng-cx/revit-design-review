using RevitDesignReview.Core;
using RevitDesignReview.Data;

namespace RevitDesignReview.Data.Tests;

public sealed class SqliteReviewRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"rdr-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Round_trip_preserves_elements_and_viewpoint()
    {
        var repository = Repository();
        var review = Review.Create(
            "project-a",
            "Duct intersects beam",
            "User A",
            [new ReviewElement("model-a", "element-1", 42, 7, "Ducts", "Duct 42")],
            new ReviewViewpoint(
                "view-1",
                "Coordination 3D",
                true,
                false,
                new Vector3Data(1, 2, 3),
                new Vector3Data(0, 1, 0),
                new Vector3Data(0, 0, 1),
                new Box3Data(
                    new Vector3Data(-1, -2, -3),
                    new Vector3Data(4, 5, 6),
                    new TransformData(
                        new Vector3Data(10, 20, 30),
                        new Vector3Data(1, 0, 0),
                        new Vector3Data(0, 1, 0),
                        new Vector3Data(0, 0, 1)))));

        var stored = await repository.AddAsync(review);
        var loaded = await repository.GetAsync(stored.Id);

        Assert.NotNull(loaded);
        Assert.Equal("REV-000001", loaded.DisplayId);
        Assert.Equal(review.Title, loaded.Title);
        Assert.Equal(review.Elements, loaded.Elements);
        Assert.Equal(review.Viewpoint, loaded.Viewpoint);
    }

    [Fact]
    public async Task Sequence_numbers_are_scoped_to_project()
    {
        var repository = Repository();
        var first = await repository.AddAsync(Create("project-a", "First"));
        var second = await repository.AddAsync(Create("project-a", "Second"));
        var otherProject = await repository.AddAsync(Create("project-b", "Other"));

        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal(2, second.SequenceNumber);
        Assert.Equal(1, otherProject.SequenceNumber);
        Assert.Equal(second.Id, (await repository.GetLatestAsync("project-a"))?.Id);
    }

    [Fact]
    public async Task Initialize_is_idempotent()
    {
        var repository = Repository();
        await repository.InitializeAsync();
        await repository.InitializeAsync();
    }

    private SqliteReviewRepository Repository() =>
        new(Path.Combine(_directory, "reviews.db"));

    private static Review Create(string projectId, string title) => Review.Create(
        projectId,
        title,
        "Test User",
        Array.Empty<ReviewElement>(),
        new ReviewViewpoint("view", "3D", true, false, null, null, null, null));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
