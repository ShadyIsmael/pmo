using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;

namespace NEXUS.Api.Development;

internal static class DevelopmentAdminRecoveryConsole
{
    internal sealed record Passwords(string Temporary, string Permanent);

    public static Passwords PromptForRecoveryFlow(int minimumLength, int maximumLength)
    {
        Console.WriteLine("Enter the temporary and first-login passwords at this local hidden prompt. Characters are not echoed.");
        var temporary = ReadConfirmedPassword("New temporary Admin password", minimumLength, maximumLength);
        var permanent = ReadConfirmedPassword("New permanent Admin password", minimumLength, maximumLength);

        var temporaryBytes = Encoding.UTF8.GetBytes(temporary);
        var permanentBytes = Encoding.UTF8.GetBytes(permanent);
        try
        {
            if (CryptographicOperations.FixedTimeEquals(temporaryBytes, permanentBytes))
                throw new InvalidOperationException("The permanent password must differ from the temporary password.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(temporaryBytes);
            CryptographicOperations.ZeroMemory(permanentBytes);
        }

        return new Passwords(temporary, permanent);
    }

    private static string ReadConfirmedPassword(string label, int minimumLength, int maximumLength)
    {
        var password = ReadHidden(label, maximumLength);
        var confirmation = ReadHidden($"Confirm {label}", maximumLength);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var confirmationBytes = Encoding.UTF8.GetBytes(confirmation);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(passwordBytes, confirmationBytes))
                throw new InvalidOperationException($"The {label.ToLowerInvariant()} entries did not match.");
            if (password.Length < minimumLength || password.Length > maximumLength)
                throw new InvalidOperationException($"The {label.ToLowerInvariant()} must be between {minimumLength} and {maximumLength} characters.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(confirmationBytes);
        }

        return password;
    }

    private static string ReadHidden(string label, int maximumLength)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new InvalidOperationException("Admin recovery requires a local interactive console so passwords are not redirected or echoed.");

        Console.Write($"{label}: ");
        var characters = new char[maximumLength];
        var characterCount = 0;
        var exceededMaximum = false;
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                    break;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (characterCount > 0)
                        characters[--characterCount] = '\0';
                    exceededMaximum = false;
                    continue;
                }
                if (char.IsControl(key.KeyChar))
                    continue;
                if (characterCount >= maximumLength)
                {
                    exceededMaximum = true;
                    continue;
                }
                characters[characterCount++] = key.KeyChar;
            }

            Console.WriteLine();
            if (exceededMaximum)
                throw new InvalidOperationException($"The {label.ToLowerInvariant()} exceeds the configured maximum length of {maximumLength} characters.");

            return new string(characters, 0, characterCount);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(characters.AsSpan()));
        }
    }
}
