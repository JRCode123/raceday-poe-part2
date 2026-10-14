namespace RaceDay.API.Helpers;

/// <summary>
/// Wraps BCrypt.Net to hash and verify passwords.
/// Passwords are NEVER stored in plain text — only the BCrypt hash is saved.
/// BCrypt automatically generates and embeds a random salt in the hash.
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// Hashes a plain-text password using BCrypt.
    /// Work factor of 12 is a good balance between security and speed.
    /// </summary>
    public static string Hash(string plainPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

    /// <summary>
    /// Verifies a plain-text password against a stored BCrypt hash.
    /// Returns true if the password matches, false otherwise.
    /// </summary>
    public static bool Verify(string plainPassword, string storedHash)
        => BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
}
