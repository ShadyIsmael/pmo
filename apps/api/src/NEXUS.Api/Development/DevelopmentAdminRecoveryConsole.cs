using System.Security.Cryptography;
using System.Text;

namespace NEXUS.Api.Development;

internal static class DevelopmentAdminRecoveryConsole
{
    internal const string InputReadyMarker = "NEXUS_DEVELOPMENT_ADMIN_RECOVERY_INPUT_READY";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal sealed record Passwords(string Temporary, string Permanent);

    public static Passwords PromptForRecoveryFlow(int minimumLength, int maximumLength)
    {
        return PromptForRecoveryFlow(
            Console.In,
            Console.Out,
            Console.IsInputRedirected,
            minimumLength,
            maximumLength);
    }

    internal static Passwords PromptForRecoveryFlow(
        TextReader input,
        TextWriter output,
        bool inputIsRedirected,
        int minimumLength,
        int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!inputIsRedirected)
            throw new InvalidOperationException("Run Development Admin recovery with the secure PowerShell procedure; direct keyboard password input is unsupported.");
        if (minimumLength < 1 || maximumLength < minimumLength)
            throw new InvalidOperationException("The configured local password length policy is invalid.");

        // The PowerShell wrapper waits for this marker after the API has verified
        // the Development SQL target and migration state, before it asks for secrets.
        output.WriteLine(InputReadyMarker);

        var temporary = ReadConfirmedPassword(input, "temporary", minimumLength, maximumLength);
        var permanent = ReadConfirmedPassword(input, "permanent", minimumLength, maximumLength);

        var temporaryBytes = Encoding.UTF8.GetBytes(temporary);
        var permanentBytes = Encoding.UTF8.GetBytes(permanent);
        try
        {
            if (CryptographicOperations.FixedTimeEquals(temporaryBytes, permanentBytes))
                throw new InvalidOperationException("The permanent Admin password must differ from the temporary password.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(temporaryBytes);
            CryptographicOperations.ZeroMemory(permanentBytes);
        }

        if (input.Read() != -1)
            throw new InvalidOperationException("The secure password input contained unexpected extra data; run the recovery procedure again.");

        return new Passwords(temporary, permanent);
    }

    private static string ReadConfirmedPassword(TextReader input, string kind, int minimumLength, int maximumLength)
    {
        var password = ReadPassword(input, maximumLength);
        var confirmation = ReadPassword(input, maximumLength);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var confirmationBytes = Encoding.UTF8.GetBytes(confirmation);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(passwordBytes, confirmationBytes))
                throw new InvalidOperationException($"The {kind} Admin password entries did not match; run recovery again.");
            if (password.Length < minimumLength || password.Length > maximumLength)
                throw new InvalidOperationException($"The {kind} Admin password must be between {minimumLength} and {maximumLength} characters.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(confirmationBytes);
        }

        return password;
    }

    private static string ReadPassword(TextReader input, int maximumLength)
    {
        var encoded = ReadEncodedLine(input, GetMaximumEncodedLength(maximumLength));
        byte[]? bytes = null;
        try
        {
            bytes = Convert.FromBase64String(encoded);
            return StrictUtf8.GetString(bytes);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The secure password input was malformed; run the PowerShell recovery procedure again.");
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidOperationException("The secure password input was malformed; run the PowerShell recovery procedure again.");
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static long GetMaximumEncodedLength(int maximumLength)
    {
        // Password policy counts UTF-16 code units, while the pipe carries UTF-8
        // bytes. Reserve the UTF-8 worst case (3 bytes per UTF-16 unit) before
        // Base64 expansion so valid non-ASCII passwords are not rejected early.
        return (long)maximumLength * 4L;
    }

    private static string ReadEncodedLine(TextReader input, long maximumEncodedLength)
    {
        var builder = new StringBuilder();
        long characterCount = 0;
        var exceededMaximum = false;
        var receivedCharacter = false;

        while (true)
        {
            var value = input.Read();
            if (value < 0)
            {
                if (!receivedCharacter)
                    throw new InvalidOperationException("Secure password input ended before all password confirmations were received.");
                break;
            }

            if (value == '\n')
                break;
            if (value == '\r')
                continue;

            receivedCharacter = true;
            characterCount++;
            if (characterCount > maximumEncodedLength)
            {
                exceededMaximum = true;
                continue;
            }

            builder.Append((char)value);
        }

        if (exceededMaximum)
            throw new InvalidOperationException("A password exceeds the configured maximum length; run the recovery procedure again.");

        return builder.ToString();
    }
}
