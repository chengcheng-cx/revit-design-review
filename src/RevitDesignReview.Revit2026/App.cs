using System.Reflection;
using Autodesk.Revit.UI;

namespace RevitDesignReview.Revit2026;

public sealed class App : IExternalApplication
{
    private const string TabName = "Design Review";

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            application.CreateRibbonTab(TabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // The tab already exists, possibly because another module created it.
        }

        var panel = application.CreateRibbonPanel(TabName, "Reviews");
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        panel.AddItem(new PushButtonData(
            "CreateReview",
            "Create\nReview",
            assemblyPath,
            typeof(CreateReviewCommand).FullName));
        panel.AddItem(new PushButtonData(
            "OpenLatestReview",
            "Open Latest\nReview",
            assemblyPath,
            typeof(OpenLatestReviewCommand).FullName));
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
