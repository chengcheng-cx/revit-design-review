using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitDesignReview.Core;

namespace RevitDesignReview.Revit2026;

[Transaction(TransactionMode.Manual)]
public sealed class CreateReviewCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiDocument = commandData.Application.ActiveUIDocument;
            var document = uiDocument.Document;
            var selectedIds = uiDocument.Selection.GetElementIds();
            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Design Review", "Select one or more model elements before creating a review.");
                return Result.Cancelled;
            }

            var dialog = new QuickReviewWindow();
            new WindowInteropHelper(dialog).Owner = commandData.Application.MainWindowHandle;
            if (dialog.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            var modelReference = ReviewEnvironment.GetModelReference(document);
            var reviewElements = selectedIds
                .Select(document.GetElement)
                .Where(element => element is not null)
                .Select(element => new ReviewElement(
                    modelReference,
                    element!.UniqueId,
                    element.Id.Value,
                    element.Category?.Id.Value,
                    element.Category?.Name,
                    element.Name))
                .ToArray();

            var review = Review.Create(
                ReviewEnvironment.GetProjectId(document),
                dialog.ReviewTitle,
                commandData.Application.Application.Username,
                reviewElements,
                ViewpointMapper.Capture(document.ActiveView));
            var repository = ReviewEnvironment.CreateRepository(document);
            var stored = repository.AddAsync(review).GetAwaiter().GetResult();

            TaskDialog.Show(
                "Design Review",
                $"{stored.DisplayId} created with {stored.Elements.Count} related element(s).\n\n{stored.Title}");
            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return Result.Failed;
        }
    }
}
