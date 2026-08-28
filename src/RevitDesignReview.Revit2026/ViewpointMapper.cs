using Autodesk.Revit.DB;
using RevitDesignReview.Core;

namespace RevitDesignReview.Revit2026;

internal static class ViewpointMapper
{
    public static ReviewViewpoint Capture(View view)
    {
        if (view is not View3D view3D)
        {
            return new ReviewViewpoint(view.UniqueId, view.Name, false, false, null, null, null, null);
        }

        var orientation = view3D.GetOrientation();
        var sectionBox = view3D.IsSectionBoxActive ? ToData(view3D.GetSectionBox()) : null;
        return new ReviewViewpoint(
            view.UniqueId,
            view.Name,
            true,
            view3D.IsPerspective,
            ToData(orientation.EyePosition),
            ToData(orientation.ForwardDirection),
            ToData(orientation.UpDirection),
            sectionBox);
    }

    public static void Restore(View3D view, ReviewViewpoint viewpoint)
    {
        if (viewpoint.EyePosition is not null &&
            viewpoint.ForwardDirection is not null &&
            viewpoint.UpDirection is not null)
        {
            view.SetOrientation(new ViewOrientation3D(
                ToXyz(viewpoint.EyePosition),
                ToXyz(viewpoint.UpDirection),
                ToXyz(viewpoint.ForwardDirection)));
        }

        if (viewpoint.SectionBox is not null)
        {
            view.SetSectionBox(ToBoundingBox(viewpoint.SectionBox));
            view.IsSectionBoxActive = true;
        }
    }

    private static Vector3Data ToData(XYZ value) => new(value.X, value.Y, value.Z);

    private static Box3Data ToData(BoundingBoxXYZ value) => new(
        ToData(value.Min),
        ToData(value.Max),
        new TransformData(
            ToData(value.Transform.Origin),
            ToData(value.Transform.BasisX),
            ToData(value.Transform.BasisY),
            ToData(value.Transform.BasisZ)));

    private static XYZ ToXyz(Vector3Data value) => new(value.X, value.Y, value.Z);

    private static BoundingBoxXYZ ToBoundingBox(Box3Data value)
    {
        var transform = Transform.Identity;
        transform.Origin = ToXyz(value.Transform.Origin);
        transform.BasisX = ToXyz(value.Transform.BasisX);
        transform.BasisY = ToXyz(value.Transform.BasisY);
        transform.BasisZ = ToXyz(value.Transform.BasisZ);
        return new BoundingBoxXYZ
        {
            Min = ToXyz(value.Min),
            Max = ToXyz(value.Max),
            Transform = transform
        };
    }
}
