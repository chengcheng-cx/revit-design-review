using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitDesignReview.Revit2026;

[Transaction(TransactionMode.Manual)]
public sealed class OpenLatestReviewCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiDocument = commandData.Application.ActiveUIDocument;
            var document = uiDocument.Document;
            var repository = ReviewEnvironment.CreateRepository(document);
            var review = repository
                .GetLatestAsync(ReviewEnvironment.GetProjectId(document))
                .GetAwaiter()
                .GetResult();
            if (review is null)
            {
                TaskDialog.Show("Design Review", "No reviews have been created for this model.");
                return Result.Cancelled;
            }

            var savedView = document.GetElement(review.Viewpoint.ViewUniqueId) as View;
            if (savedView is not null && !savedView.IsTemplate)
            {
                uiDocument.ActiveView = savedView;
                if (savedView is View3D view3D && review.Viewpoint.Is3D)
                {
                    using var transaction = new Transaction(document, "Restore review viewpoint");
                    transaction.Start();
                    ViewpointMapper.Restore(view3D, review.Viewpoint);
                    transaction.Commit();
                }
            }

            var foundIds = review.Elements
                .Select(item => document.GetElement(item.ElementUniqueId))
                .Where(element => element is not null)
                .Select(element => element!.Id)
                .ToList();
            uiDocument.Selection.SetElementIds(foundIds);
            if (foundIds.Count > 0)
            {
                uiDocument.ShowElements(foundIds);
            }

            var missingCount = review.Elements.Count - foundIds.Count;
            var resultText = $"Opened {review.DisplayId}: {review.Title}\n\nSelected {foundIds.Count} element(s).";
            if (missingCount > 0)
            {
                resultText += $"\n{missingCount} associated element(s) are no longer available.";
            }

            TaskDialog.Show("Design Review", resultText);
            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return Result.Failed;
        }
    }
}
