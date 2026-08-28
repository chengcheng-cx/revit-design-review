namespace RevitDesignReview.Core;

public sealed record ReviewViewpoint(
    string ViewUniqueId,
    string ViewName,
    bool Is3D,
    bool IsPerspective,
    Vector3Data? EyePosition,
    Vector3Data? ForwardDirection,
    Vector3Data? UpDirection,
    Box3Data? SectionBox);
