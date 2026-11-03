using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KitsuneHrm.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace KitsuneHrm.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public (string Token, DateTime Expira) CrearAccessToken(Usuario u, IEnumerable<string> permisos)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var expira = DateTime.UtcNow.AddMinutes(cfg.GetValue("Jwt:AccessTokenMinutes", 15));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Username),
            new(ClaimTypes.Role, u.Rol.Nombre),
            new("must_change", u.DebeCambiarPassword ? "true" : "false"),
        };
        if (u.EmpleadoId is int eid) claims.Add(new Claim("empleado_id", eid.ToString()));
        claims.AddRange(permisos.Select(p => new Claim("permiso", p)));

        var jwt = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expira);
    }

    public static string NuevoRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
