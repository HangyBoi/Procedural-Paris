// @Nichita Cebotari
// *Explanatory Comments and Headers were written with help of AI*
// *General Review, Formatting, Optimization and Code Cleanup were done by AI*
//
// This script defines a set of shared, constant values for geometric calculations.
//

/// <summary>
/// A static class holding constant tolerance values for floating-point geometric comparisons.
/// Using shared constants ensures consistency across different calculation utilities.
/// </summary>
public static class GeometryConstants
{
    /// <summary>
    /// A small tolerance value for general-purpose floating-point comparisons.
    /// Used to check if values are "close enough" to be considered equal.
    /// </summary>
    public const float GeometricEpsilon = 1e-5f;

    /// <summary>
    /// The square of GeometricEpsilon. Useful for comparing squared distances to avoid
    /// costly square root operations.
    /// </summary>
    public const float GeometricEpsilonSqr = GeometricEpsilon * GeometricEpsilon;

    /// <summary>
    /// A very small tolerance value for high-precision checks where floating point
    /// inaccuracies are more critical (e.g., testing for perfect collinearity).
    /// </summary>
    public const double HighPrecisionEpsilon = 1e-9;
}