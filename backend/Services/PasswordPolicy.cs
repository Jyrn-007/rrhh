using System.Security.Cryptography;

namespace KitsuneHrm.Api.Services;

public static class PasswordPolicy
{
    public const int MinLength = 10;

    public static bool EsValida(string? pwd, out string error)
    {
        error = "";
        if (string.IsNullOrEmpty(pwd) || pwd.Length < MinLength) error = $"La contraseña debe tener al menos {MinLength} caracteres.";
        else if (pwd.Length > 128) error = "La contraseña es demasiado larga.";
        else if (!pwd.Any(char.IsUpper)) error = "La contraseña debe incluir una mayúscula.";
        else if (!pwd.Any(char.IsLower)) error = "La contraseña debe incluir una minúscula.";
        else if (!pwd.Any(char.IsDigit)) error = "La contraseña debe incluir un número.";
        else if (pwd.All(char.IsLetterOrDigit)) error = "La contraseña debe incluir un símbolo.";
        return error == "";
    }

    public static string Hash(string pwd) => BCrypt.Net.BCrypt.HashPassword(pwd, workFactor: 12);

    /// <summary>Contraseña temporal aleatoria que cumple la política.</summary>
    public static string Generar()
    {
        const string up = "ABCDEFGHJKLMNPQRSTUVWXYZ", lo = "abcdefghijkmnpqrstuvwxyz", di = "23456789", sy = "!@#$%*?";
        var all = up + lo + di + sy;
        var chars = new List<char>
        {
            up[RandomNumberGenerator.GetInt32(up.Length)],
            lo[RandomNumberGenerator.GetInt32(lo.Length)],
            di[RandomNumberGenerator.GetInt32(di.Length)],
            sy[RandomNumberGenerator.GetInt32(sy.Length)],
        };
        while (chars.Count < 14) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }
}
