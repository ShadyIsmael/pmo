using System.Security.Cryptography;

namespace NEXUS.Modules.Identity.Infrastructure;

internal static class TemporaryPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!#$%&*+-=?@";
    private const string All = Upper + Lower + Digits + Symbols;

    public static string Generate(int length = 32)
    {
        if (length < 4)
            throw new ArgumentOutOfRangeException(nameof(length));

        var characters = new char[length];
        characters[0] = Pick(Upper);
        characters[1] = Pick(Lower);
        characters[2] = Pick(Digits);
        characters[3] = Pick(Symbols);
        for (var index = 4; index < characters.Length; index++)
            characters[index] = Pick(All);

        for (var index = characters.Length - 1; index > 0; index--)
        {
            var other = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[other]) = (characters[other], characters[index]);
        }

        return new string(characters);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
