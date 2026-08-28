using System.Globalization;
using Microsoft.Data.Sqlite;
using RevitDesignReview.Core;

namespace RevitDesignReview.Data;

public sealed class SqliteReviewRepository : IReviewRepository
{
    public const int CurrentSchemaVersion = 1;
    private readonly string _connectionString;

    public SqliteReviewRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_info (
                version INTEGER NOT NULL
            );

            INSERT INTO schema_info(version)
            SELECT 1
            WHERE NOT EXISTS (SELECT 1 FROM schema_info);

            CREATE TABLE IF NOT EXISTS reviews (
                id TEXT PRIMARY KEY,
                sequence_number INTEGER NOT NULL,
                project_id TEXT NOT NULL,
                title TEXT NOT NULL,
                author_name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                modified_at TEXT NOT NULL,
                status INTEGER NOT NULL,
                source INTEGER NOT NULL,
                UNIQUE(project_id, sequence_number)
            );

            CREATE INDEX IF NOT EXISTS ix_reviews_project_modified
                ON reviews(project_id, modified_at DESC);

            CREATE TABLE IF NOT EXISTS review_elements (
                review_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                model_reference TEXT NOT NULL,
                element_unique_id TEXT NOT NULL,
                element_id_at_creation INTEGER NOT NULL,
                category_id INTEGER NULL,
                category_name TEXT NULL,
                display_name TEXT NOT NULL,
                link_instance_unique_id TEXT NULL,
                PRIMARY KEY(review_id, ordinal),
                FOREIGN KEY(review_id) REFERENCES reviews(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS review_viewpoints (
                review_id TEXT PRIMARY KEY,
                view_unique_id TEXT NOT NULL,
                view_name TEXT NOT NULL,
                is_3d INTEGER NOT NULL,
                is_perspective INTEGER NOT NULL,
                eye_x REAL NULL, eye_y REAL NULL, eye_z REAL NULL,
                forward_x REAL NULL, forward_y REAL NULL, forward_z REAL NULL,
                up_x REAL NULL, up_y REAL NULL, up_z REAL NULL,
                box_min_x REAL NULL, box_min_y REAL NULL, box_min_z REAL NULL,
                box_max_x REAL NULL, box_max_y REAL NULL, box_max_z REAL NULL,
                transform_origin_x REAL NULL, transform_origin_y REAL NULL, transform_origin_z REAL NULL,
                transform_basis_x_x REAL NULL, transform_basis_x_y REAL NULL, transform_basis_x_z REAL NULL,
                transform_basis_y_x REAL NULL, transform_basis_y_y REAL NULL, transform_basis_y_z REAL NULL,
                transform_basis_z_x REAL NULL, transform_basis_z_y REAL NULL, transform_basis_z_z REAL NULL,
                FOREIGN KEY(review_id) REFERENCES reviews(id) ON DELETE CASCADE
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var versionCommand = connection.CreateCommand();
        versionCommand.Transaction = transaction;
        versionCommand.CommandText = "SELECT version FROM schema_info LIMIT 1;";
        var version = Convert.ToInt32(await versionCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema {version} is newer than supported schema {CurrentSchemaVersion}.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Review> AddAsync(Review review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var sequenceCommand = connection.CreateCommand();
        sequenceCommand.Transaction = transaction;
        sequenceCommand.CommandText =
            "SELECT COALESCE(MAX(sequence_number), 0) + 1 FROM reviews WHERE project_id = $projectId;";
        sequenceCommand.Parameters.AddWithValue("$projectId", review.ProjectId);
        var sequence = Convert.ToInt32(
            await sequenceCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        var stored = review with { SequenceNumber = sequence };

        var reviewCommand = connection.CreateCommand();
        reviewCommand.Transaction = transaction;
        reviewCommand.CommandText = """
            INSERT INTO reviews(
                id, sequence_number, project_id, title, author_name,
                created_at, modified_at, status, source)
            VALUES(
                $id, $sequence, $projectId, $title, $authorName,
                $createdAt, $modifiedAt, $status, $source);
            """;
        reviewCommand.Parameters.AddWithValue("$id", stored.Id.ToString("D"));
        reviewCommand.Parameters.AddWithValue("$sequence", stored.SequenceNumber);
        reviewCommand.Parameters.AddWithValue("$projectId", stored.ProjectId);
        reviewCommand.Parameters.AddWithValue("$title", stored.Title);
        reviewCommand.Parameters.AddWithValue("$authorName", stored.AuthorName);
        reviewCommand.Parameters.AddWithValue("$createdAt", stored.CreatedAt.ToString("O"));
        reviewCommand.Parameters.AddWithValue("$modifiedAt", stored.ModifiedAt.ToString("O"));
        reviewCommand.Parameters.AddWithValue("$status", (int)stored.Status);
        reviewCommand.Parameters.AddWithValue("$source", (int)stored.Source);
        await reviewCommand.ExecuteNonQueryAsync(cancellationToken);

        for (var index = 0; index < stored.Elements.Count; index++)
        {
            await InsertElementAsync(connection, transaction, stored.Id, index, stored.Elements[index], cancellationToken);
        }

        await InsertViewpointAsync(connection, transaction, stored.Id, stored.Viewpoint, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return stored;
    }

    public async Task<Review?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadReviewAsync(connection, "WHERE id = $value", id.ToString("D"), cancellationToken);
    }

    public async Task<Review?> GetLatestAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadReviewAsync(
            connection,
            "WHERE project_id = $value ORDER BY modified_at DESC, sequence_number DESC LIMIT 1",
            projectId,
            cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task InsertElementAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid reviewId,
        int ordinal,
        ReviewElement element,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO review_elements(
                review_id, ordinal, model_reference, element_unique_id,
                element_id_at_creation, category_id, category_name, display_name,
                link_instance_unique_id)
            VALUES(
                $reviewId, $ordinal, $modelReference, $elementUniqueId,
                $elementId, $categoryId, $categoryName, $displayName, $linkUniqueId);
            """;
        command.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        command.Parameters.AddWithValue("$ordinal", ordinal);
        command.Parameters.AddWithValue("$modelReference", element.ModelReference);
        command.Parameters.AddWithValue("$elementUniqueId", element.ElementUniqueId);
        command.Parameters.AddWithValue("$elementId", element.ElementIdAtCreation);
        command.Parameters.AddWithValue("$categoryId", (object?)element.CategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$categoryName", (object?)element.CategoryName ?? DBNull.Value);
        command.Parameters.AddWithValue("$displayName", element.DisplayNameAtCreation);
        command.Parameters.AddWithValue("$linkUniqueId", (object?)element.LinkInstanceUniqueId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertViewpointAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid reviewId,
        ReviewViewpoint viewpoint,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO review_viewpoints(
                review_id, view_unique_id, view_name, is_3d, is_perspective,
                eye_x, eye_y, eye_z, forward_x, forward_y, forward_z, up_x, up_y, up_z,
                box_min_x, box_min_y, box_min_z, box_max_x, box_max_y, box_max_z,
                transform_origin_x, transform_origin_y, transform_origin_z,
                transform_basis_x_x, transform_basis_x_y, transform_basis_x_z,
                transform_basis_y_x, transform_basis_y_y, transform_basis_y_z,
                transform_basis_z_x, transform_basis_z_y, transform_basis_z_z)
            VALUES(
                $reviewId, $viewUniqueId, $viewName, $is3d, $isPerspective,
                $eyeX, $eyeY, $eyeZ, $forwardX, $forwardY, $forwardZ, $upX, $upY, $upZ,
                $boxMinX, $boxMinY, $boxMinZ, $boxMaxX, $boxMaxY, $boxMaxZ,
                $originX, $originY, $originZ,
                $basisXX, $basisXY, $basisXZ,
                $basisYX, $basisYY, $basisYZ,
                $basisZX, $basisZY, $basisZZ);
            """;
        command.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        command.Parameters.AddWithValue("$viewUniqueId", viewpoint.ViewUniqueId);
        command.Parameters.AddWithValue("$viewName", viewpoint.ViewName);
        command.Parameters.AddWithValue("$is3d", viewpoint.Is3D ? 1 : 0);
        command.Parameters.AddWithValue("$isPerspective", viewpoint.IsPerspective ? 1 : 0);
        AddVector(command, "eye", viewpoint.EyePosition);
        AddVector(command, "forward", viewpoint.ForwardDirection);
        AddVector(command, "up", viewpoint.UpDirection);
        AddVector(command, "boxMin", viewpoint.SectionBox?.Min);
        AddVector(command, "boxMax", viewpoint.SectionBox?.Max);
        AddVector(command, "origin", viewpoint.SectionBox?.Transform.Origin);
        AddVector(command, "basisX", viewpoint.SectionBox?.Transform.BasisX);
        AddVector(command, "basisY", viewpoint.SectionBox?.Transform.BasisY);
        AddVector(command, "basisZ", viewpoint.SectionBox?.Transform.BasisZ);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddVector(SqliteCommand command, string prefix, Vector3Data? value)
    {
        command.Parameters.AddWithValue($"${prefix}X", (object?)value?.X ?? DBNull.Value);
        command.Parameters.AddWithValue($"${prefix}Y", (object?)value?.Y ?? DBNull.Value);
        command.Parameters.AddWithValue($"${prefix}Z", (object?)value?.Z ?? DBNull.Value);
    }

    private static async Task<Review?> ReadReviewAsync(
        SqliteConnection connection,
        string whereClause,
        string value,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT id, sequence_number, project_id, title, author_name,
                   created_at, modified_at, status, source
            FROM reviews {whereClause};
            """;
        command.Parameters.AddWithValue("$value", value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = Guid.Parse(reader.GetString(0));
        var review = new Review
        {
            Id = id,
            SequenceNumber = reader.GetInt32(1),
            ProjectId = reader.GetString(2),
            Title = reader.GetString(3),
            AuthorName = reader.GetString(4),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ModifiedAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Status = (ReviewStatus)reader.GetInt32(7),
            Source = (ReviewSource)reader.GetInt32(8),
            Elements = Array.Empty<ReviewElement>(),
            Viewpoint = new ReviewViewpoint(string.Empty, string.Empty, false, false, null, null, null, null)
        };
        await reader.DisposeAsync();

        return review with
        {
            Elements = await ReadElementsAsync(connection, id, cancellationToken),
            Viewpoint = await ReadViewpointAsync(connection, id, cancellationToken)
        };
    }

    private static async Task<IReadOnlyList<ReviewElement>> ReadElementsAsync(
        SqliteConnection connection,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT model_reference, element_unique_id, element_id_at_creation,
                   category_id, category_name, display_name, link_instance_unique_id
            FROM review_elements
            WHERE review_id = $reviewId
            ORDER BY ordinal;
            """;
        command.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        var elements = new List<ReviewElement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            elements.Add(new ReviewElement(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return elements;
    }

    private static async Task<ReviewViewpoint> ReadViewpointAsync(
        SqliteConnection connection,
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM review_viewpoints WHERE review_id = $reviewId;";
        command.Parameters.AddWithValue("$reviewId", reviewId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException($"Review {reviewId:D} has no viewpoint.");
        }

        var eye = ReadVector(reader, "eye_x", "eye_y", "eye_z");
        var forward = ReadVector(reader, "forward_x", "forward_y", "forward_z");
        var up = ReadVector(reader, "up_x", "up_y", "up_z");
        var min = ReadVector(reader, "box_min_x", "box_min_y", "box_min_z");
        var max = ReadVector(reader, "box_max_x", "box_max_y", "box_max_z");
        var origin = ReadVector(reader, "transform_origin_x", "transform_origin_y", "transform_origin_z");
        var basisX = ReadVector(reader, "transform_basis_x_x", "transform_basis_x_y", "transform_basis_x_z");
        var basisY = ReadVector(reader, "transform_basis_y_x", "transform_basis_y_y", "transform_basis_y_z");
        var basisZ = ReadVector(reader, "transform_basis_z_x", "transform_basis_z_y", "transform_basis_z_z");
        Box3Data? sectionBox = null;
        if (min is not null && max is not null && origin is not null && basisX is not null && basisY is not null && basisZ is not null)
        {
            sectionBox = new Box3Data(min, max, new TransformData(origin, basisX, basisY, basisZ));
        }

        return new ReviewViewpoint(
            reader.GetString(reader.GetOrdinal("view_unique_id")),
            reader.GetString(reader.GetOrdinal("view_name")),
            reader.GetInt32(reader.GetOrdinal("is_3d")) == 1,
            reader.GetInt32(reader.GetOrdinal("is_perspective")) == 1,
            eye,
            forward,
            up,
            sectionBox);
    }

    private static Vector3Data? ReadVector(SqliteDataReader reader, string xName, string yName, string zName)
    {
        var x = reader.GetOrdinal(xName);
        var y = reader.GetOrdinal(yName);
        var z = reader.GetOrdinal(zName);
        return reader.IsDBNull(x) || reader.IsDBNull(y) || reader.IsDBNull(z)
            ? null
            : new Vector3Data(reader.GetDouble(x), reader.GetDouble(y), reader.GetDouble(z));
    }
}
