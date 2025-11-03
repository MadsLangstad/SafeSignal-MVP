namespace SafeSignal.Cloud.Core.Interfaces;

/// <summary>
/// Service for tracking login attempts and implementing brute force protection.
/// Prevents automated attacks by locking accounts after repeated failed login attempts.
/// </summary>
public interface ILoginAttemptService
{
    /// <summary>
    /// Records a failed login attempt for the specified email address.
    /// </summary>
    /// <param name="email">Email address that failed authentication</param>
    /// <param name="ipAddress">IP address of the request</param>
    /// <returns>Task</returns>
    Task RecordFailedAttemptAsync(string email, string ipAddress);

    /// <summary>
    /// Resets failed login attempt counter for the specified email address after successful login.
    /// </summary>
    /// <param name="email">Email address that successfully authenticated</param>
    /// <returns>Task</returns>
    Task ResetFailedAttemptsAsync(string email);

    /// <summary>
    /// Checks if an account is currently locked due to too many failed login attempts.
    /// </summary>
    /// <param name="email">Email address to check</param>
    /// <returns>True if account is locked, false otherwise</returns>
    Task<bool> IsAccountLockedAsync(string email);

    /// <summary>
    /// Gets the remaining lockout time for a locked account.
    /// </summary>
    /// <param name="email">Email address to check</param>
    /// <returns>Remaining lockout time in seconds, or 0 if not locked</returns>
    Task<int> GetLockoutRemainingSecondsAsync(string email);

    /// <summary>
    /// Gets the number of failed attempts for an email address.
    /// </summary>
    /// <param name="email">Email address to check</param>
    /// <returns>Number of failed attempts</returns>
    Task<int> GetFailedAttemptsCountAsync(string email);
}
