using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using RevitDesignReview.Data;

namespace RevitDesignReview.Revit2026;

internal static class ReviewEnvironment
{
    public static string GetProjectId(Document document)
    {
        var modelReference = GetModelReference(document);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modelReference))).ToLowerInvariant();
    }

    public static string GetModelReference(Document document)
    {
        var path = document.PathName;
        var location = string.IsNullOrWhiteSpace(path) ? "unsaved" : System.IO.Path.GetFullPath(path);
        return $"{document.ProjectInformation.UniqueId}|{location}";
    }

    public static SqliteReviewRepository CreateRepository(Document document)
    {
        var root = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RevitDesignReview",
            "projects",
            GetProjectId(document));
        return new SqliteReviewRepository(System.IO.Path.Combine(root, "reviews.db"));
    }
}
