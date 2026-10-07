using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HealthApp.Application.Abstractions;
using HealthApp.Shared.DTOs;
using HealthApp.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace HealthApp.Infrastructure.Authentication;

public sealed class JwtOptions {
    public string Issuer {
        get;
        set;
    }
    ="HealthApp.Api";
    public string Audience {
        get;
        set;
    }
    ="HealthApp.Web";
    public string Key {
        get;
        set;
    }
    ="CHANGE_THIS_DEVELOPMENT_KEY_TO_A_LONG_RANDOM_SECRET_1234567890";
    public int Minutes {
        get;
        set;
    }
    =60;
}

public sealed class PasswordService : IPasswordService {
    private readonly PasswordHasher<User> _hasher=new();
    public string Hash(string p)=>_hasher.HashPassword(new User(),p);
    public bool Verify(string p,string h)=>_hasher.VerifyHashedPassword(new User(),h,p) is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public AuthResponse CreateToken(User user) {
        var o=options.Value;
        var expires=DateTime.UtcNow.AddMinutes(o.Minutes);
        var claims=new[] {
            new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),
            new Claim(ClaimTypes.Email,user.Email),
            new Claim(ClaimTypes.Role,user.Role.ToString()),
            new Claim("outlet_id",user.OutletId?.ToString()??"")
        };
        var key=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key));
        var creds=new SigningCredentials(key,SecurityAlgorithms.HmacSha256);
        var token=new JwtSecurityToken(o.Issuer,o.Audience,claims,expires:expires,signingCredentials:creds);
        return new(new JwtSecurityTokenHandler().WriteToken(token),expires,new(user.Id,user.Email,user.FirstName,user.LastName,user.Role.ToString(),user.OutletId,false,null,user.MobileNumber));
    }
}
