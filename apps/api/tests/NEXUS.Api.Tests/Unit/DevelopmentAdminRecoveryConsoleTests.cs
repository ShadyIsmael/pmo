using System.Text;
using NEXUS.Api.Development;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class DevelopmentAdminRecoveryConsoleTests
{
    private const int MinimumLength = 12;
    private const int MaximumLength = 64;

    [Fact]
    public void Secure_stdin_decodes_quotes_and_special_characters_without_echoing_passwords()
    {
        var temporaryPassword = new string(['A', 'a', '1', '!', '"', '#', 'z', 'B', '2', '?', 'c', 'D']);
        const string permanentPassword = "Qw7$*mR2?zP9";
        var output = new StringWriter();

        var passwords = Read(
            output,
            inputIsRedirected: true,
            temporaryPassword,
            temporaryPassword,
            permanentPassword,
            permanentPassword);

        Assert.Equal(temporaryPassword, passwords.Temporary);
        Assert.Equal(permanentPassword, passwords.Permanent);
        Assert.Equal(DevelopmentAdminRecoveryConsole.InputReadyMarker + Environment.NewLine, output.ToString());
        Assert.DoesNotContain(temporaryPassword, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(permanentPassword, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Secure_stdin_accepts_valid_non_ascii_passwords_within_the_length_policy()
    {
        const string temporaryPassword = "Aa1!مفتاح_آمن12";
        const string permanentPassword = "Qw7$*mR2?zP9";
        var output = new StringWriter();

        var passwords = Read(
            output,
            inputIsRedirected: true,
            temporaryPassword,
            temporaryPassword,
            permanentPassword,
            permanentPassword);

        Assert.Equal(temporaryPassword, passwords.Temporary);
        Assert.Equal(permanentPassword, passwords.Permanent);
        Assert.DoesNotContain(temporaryPassword, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Direct_keyboard_input_is_rejected_before_any_password_is_read()
    {
        var output = new StringWriter();
        var input = new StringReader(string.Empty);

        var error = Assert.Throws<InvalidOperationException>(() =>
            DevelopmentAdminRecoveryConsole.PromptForRecoveryFlow(
                input,
                output,
                inputIsRedirected: false,
                MinimumLength,
                MaximumLength));

        Assert.Contains("secure PowerShell procedure", error.Message, StringComparison.Ordinal);
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void Temporary_password_confirmation_must_match_and_error_does_not_reveal_input()
    {
        const string temporaryPassword = "Aa1!Temporary12";
        const string differentConfirmation = "Bb2@Temporary34";
        var output = new StringWriter();

        var error = Assert.Throws<InvalidOperationException>(() =>
            Read(output, true, temporaryPassword, differentConfirmation, "Qw7$*mR2?zP9", "Qw7$*mR2?zP9"));

        Assert.Contains("temporary Admin password entries did not match", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(temporaryPassword, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(differentConfirmation, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(temporaryPassword, output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Permanent_password_confirmation_must_match_without_revealing_input()
    {
        const string temporaryPassword = "Aa1!Temporary12";
        const string permanentPassword = "Qw7$*mR2?zP9";
        const string differentConfirmation = "Rx8%+nS3?yQ0";

        var error = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, temporaryPassword, temporaryPassword, permanentPassword, differentConfirmation));

        Assert.Contains("permanent Admin password entries did not match", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(permanentPassword, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(differentConfirmation, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Temporary_password_must_meet_the_configured_minimum_length()
    {
        const string tooShort = "Aa1!short";

        var error = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, tooShort, tooShort, "Qw7$*mR2?zP9", "Qw7$*mR2?zP9"));

        Assert.Contains("temporary Admin password must be between 12 and 64 characters", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(tooShort, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Permanent_password_must_meet_the_configured_minimum_length()
    {
        const string tooShort = "Qw7$short";
        const string temporaryPassword = "Aa1!Temporary12";

        var error = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, temporaryPassword, temporaryPassword, tooShort, tooShort));

        Assert.Contains("permanent Admin password must be between 12 and 64 characters", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(tooShort, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Password_longer_than_configured_maximum_is_rejected()
    {
        var tooLong = new string('a', MaximumLength + 1);

        var error = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, tooLong, tooLong, "Qw7$*mR2?zP9", "Qw7$*mR2?zP9"));

        Assert.Contains("must be between 12 and 64 characters", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(tooLong, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Permanent_password_must_differ_from_temporary_password()
    {
        const string password = "Aa1!Temporary12";

        var error = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, password, password, password, password));

        Assert.Contains("must differ from the temporary password", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(password, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_input_and_cancelled_input_fail_without_echoing_any_value()
    {
        var invalidOutput = new StringWriter();
        var invalidError = Assert.Throws<InvalidOperationException>(() =>
            DevelopmentAdminRecoveryConsole.PromptForRecoveryFlow(
                new StringReader("not-base64" + Environment.NewLine),
                invalidOutput,
                inputIsRedirected: true,
                MinimumLength,
                MaximumLength));
        Assert.Contains("secure password input was malformed", invalidError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not-base64", invalidError.Message, StringComparison.Ordinal);

        var cancelledError = Assert.Throws<InvalidOperationException>(() =>
            DevelopmentAdminRecoveryConsole.PromptForRecoveryFlow(
                new StringReader(string.Empty),
                new StringWriter(),
                inputIsRedirected: true,
                MinimumLength,
                MaximumLength));
        Assert.Contains("ended before all password confirmations", cancelledError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncated_or_extra_secure_input_is_rejected()
    {
        var temporaryPassword = "Aa1!Temporary12";
        var permanentPassword = "Qw7$*mR2?zP9";
        var truncated = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, temporaryPassword, temporaryPassword, permanentPassword));
        Assert.Contains("ended before all password confirmations", truncated.Message, StringComparison.Ordinal);

        var extra = Assert.Throws<InvalidOperationException>(() =>
            Read(new StringWriter(), true, temporaryPassword, temporaryPassword, permanentPassword, permanentPassword, "extra"));
        Assert.Contains("unexpected extra data", extra.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(temporaryPassword, extra.Message, StringComparison.Ordinal);
    }

    private static DevelopmentAdminRecoveryConsole.Passwords Read(
        TextWriter output,
        bool inputIsRedirected,
        params string[] values)
    {
        var lines = values.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
        var input = new StringReader(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        return DevelopmentAdminRecoveryConsole.PromptForRecoveryFlow(
            input,
            output,
            inputIsRedirected,
            MinimumLength,
            MaximumLength);
    }
}
