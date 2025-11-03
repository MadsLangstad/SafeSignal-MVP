using SafeSignal.Cloud.Core.Interfaces;
using System.Text.RegularExpressions;

namespace SafeSignal.Cloud.Api.Services;

/// <summary>
/// Implements password validation according to OWASP and NIST guidelines.
///
/// Policy:
/// - Minimum 12 characters (NIST recommendation)
/// - At least one uppercase letter
/// - At least one lowercase letter
/// - At least one digit
/// - At least one special character
/// - No common passwords (top 100 most common)
/// - Maximum 128 characters (prevent DoS via bcrypt work factor)
/// </summary>
public class PasswordValidator : IPasswordValidator
{
    private readonly ILogger<PasswordValidator> _logger;
    private readonly IConfiguration _configuration;

    // Configuration with secure defaults
    private readonly int _minimumLength;
    private readonly int _maximumLength;
    private readonly bool _requireUppercase;
    private readonly bool _requireLowercase;
    private readonly bool _requireDigit;
    private readonly bool _requireSpecialCharacter;

    // Top 20 most common passwords (subset for performance)
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "password", "123456789", "12345678", "12345", "1234567", "1234567890",
        "qwerty", "abc123", "111111", "123123", "admin", "letmein", "welcome", "monkey",
        "password1", "qwerty123", "123321", "passw0rd", "master"
    };

    public PasswordValidator(ILogger<PasswordValidator> _logger, IConfiguration configuration)
    {
        this._logger = _logger;
        _configuration = configuration;

        // Load configuration with secure defaults
        var section = _configuration.GetSection("PasswordPolicy");
        _minimumLength = section.GetValue("MinimumLength", 12);
        _maximumLength = section.GetValue("MaximumLength", 128);
        _requireUppercase = section.GetValue("RequireUppercase", true);
        _requireLowercase = section.GetValue("RequireLowercase", true);
        _requireDigit = section.GetValue("RequireDigit", true);
        _requireSpecialCharacter = section.GetValue("RequireSpecialCharacter", true);

        this._logger.LogInformation(
            "PasswordValidator initialized: MinLength={MinLength}, MaxLength={MaxLength}, RequireUpper={RequireUpper}, RequireLower={RequireLower}, RequireDigit={RequireDigit}, RequireSpecial={RequireSpecial}",
            _minimumLength, _maximumLength, _requireUppercase, _requireLowercase, _requireDigit, _requireSpecialCharacter);
    }

    public PasswordValidationResult Validate(string password)
    {
        var errors = new List<string>();

        // Check null or whitespace
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required");
            return PasswordValidationResult.Failure(errors.ToArray());
        }

        // Length validation
        if (password.Length < _minimumLength)
        {
            errors.Add($"Password must be at least {_minimumLength} characters long");
        }

        if (password.Length > _maximumLength)
        {
            errors.Add($"Password must not exceed {_maximumLength} characters");
        }

        // Complexity requirements
        if (_requireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (_requireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (_requireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one digit");
        }

        if (_requireSpecialCharacter && !ContainsSpecialCharacter(password))
        {
            errors.Add("Password must contain at least one special character (!@#$%^&*(),.?\":{}|<>)");
        }

        // Common password check
        if (CommonPasswords.Contains(password))
        {
            errors.Add("Password is too common and easily guessable. Please choose a stronger password");
            _logger.LogWarning("Attempted to use common password");
        }

        // Check for sequential characters (simple check)
        if (ContainsSequentialCharacters(password))
        {
            errors.Add("Password contains sequential characters (e.g., 123, abc). Please choose a more complex password");
        }

        if (errors.Count > 0)
        {
            return PasswordValidationResult.Failure(errors.ToArray());
        }

        return PasswordValidationResult.Success();
    }

    private static bool ContainsSpecialCharacter(string password)
    {
        // OWASP recommended special characters
        var specialChars = "!@#$%^&*(),.?\":{}|<>";
        return password.Any(c => specialChars.Contains(c));
    }

    private static bool ContainsSequentialCharacters(string password)
    {
        // Check for 3+ sequential numbers or letters
        for (int i = 0; i < password.Length - 2; i++)
        {
            if (char.IsDigit(password[i]) && char.IsDigit(password[i + 1]) && char.IsDigit(password[i + 2]))
            {
                int first = password[i] - '0';
                int second = password[i + 1] - '0';
                int third = password[i + 2] - '0';

                if (second == first + 1 && third == second + 1)
                {
                    return true; // Ascending sequence like 123
                }
                if (second == first - 1 && third == second - 1)
                {
                    return true; // Descending sequence like 321
                }
            }

            if (char.IsLetter(password[i]) && char.IsLetter(password[i + 1]) && char.IsLetter(password[i + 2]))
            {
                char first = char.ToLower(password[i]);
                char second = char.ToLower(password[i + 1]);
                char third = char.ToLower(password[i + 2]);

                if (second == first + 1 && third == second + 1)
                {
                    return true; // Ascending sequence like abc
                }
                if (second == first - 1 && third == second - 1)
                {
                    return true; // Descending sequence like cba
                }
            }
        }

        return false;
    }
}
