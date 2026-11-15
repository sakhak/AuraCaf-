
using AuraCafé.Web.Models;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace AuraCafé.Web.Services;

public class AuthSessionStore
{
    private readonly IMemoryCache _cache;

    public AuthSessionStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Create(LoginResult login)
    {
        var id = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        _cache.Set(
            id,
            login,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = new DateTimeOffset(
                    DateTime.SpecifyKind(
                        login.ExpiresAtUtc,
                        DateTimeKind.Utc))
            });

        return id;
    }

    public LoginResult? Get(string id)
    {
        return _cache.TryGetValue(
            id, out LoginResult? result)
            ? result
            : null;
    }

    public void Remove(string id)
    {
        _cache.Remove(id);
    }
}
