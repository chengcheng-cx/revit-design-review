namespace RevitDesignReview.Core;

public sealed record Vector3Data(double X, double Y, double Z);

public sealed record TransformData(
    Vector3Data Origin,
    Vector3Data BasisX,
    Vector3Data BasisY,
    Vector3Data BasisZ);

public sealed record Box3Data(
    Vector3Data Min,
    Vector3Data Max,
    TransformData Transform);
