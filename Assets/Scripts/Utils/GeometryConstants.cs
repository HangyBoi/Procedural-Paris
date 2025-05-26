public static class GeometryConstants
{
    /// <summary>
    /// A small tolerance value for floating-point comparisons in geometric calculations.
    /// </summary>
    public const float GeometricEpsilon = 1e-5f;

    /// <summary>
    /// A very small tolerance value, closer to machine epsilon, for cases where high precision is needed
    /// and accumulation error is less of a concern (e.g., checking for exact collinearity in circumcenter).
    /// </summary>
    public const double HighPrecisionEpsilon = 1e-9;

    /// <summary>
    /// Squared epsilon, useful for avoiding square roots in distance comparisons.
    /// </summary>
    public const float GeometricEpsilonSqr = GeometricEpsilon * GeometricEpsilon;
}