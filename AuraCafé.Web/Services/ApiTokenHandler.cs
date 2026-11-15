
using System.Net.Http.Headers;

namespace AuraCafé.Web.Services;

public class ApiTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly AuthSessionStore _sessions;

    public ApiTokenHandler(
        IHttpContextAccessor contextAccessor,
        AuthSessionStore sessions)
    {
        _contextAccessor = contextAccessor;
        _sessions = sessions;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sessionId = _contextAccessor.HttpContext?
            .User.FindFirst("session_id")?.Value;

        if (sessionId is not null)
        {
            var login = _sessions.Get(sessionId);

            if (login is not null)
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", login.AccessToken);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
