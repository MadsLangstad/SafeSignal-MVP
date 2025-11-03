namespace SafeSignal.Cloud.Core.Interfaces;

/// <summary>
/// Service for validating passwords against security policy requirements.
/// Enforces minimum length, complexity, and common password checks.
/// </summary>
public interface IPasswordValidator
{
    /// <summary>
    /// Validates a password against security policy.
    /// </summary>
    /// <param name="password">Password to validate</param>
    /// <returns>Validation result with success status and error messages</returns>
    PasswordValidationResult Validate(string password);
}

/// <summary>
/// Result of password validation.
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static PasswordValidationResult Success() => new() { IsValid = true };

    public static PasswordValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
