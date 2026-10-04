// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.StaticAssets;
using NSail.Security;

namespace NSail.BaseServices.WebApi;

/// <summary>The wall between a signed ticket and the rows it names. A credential outlives
/// them: merge a duplicate party away and the sessions issued before it keep naming the id
/// that is gone, so the first write on their behalf is refused as a foreign key and the person
/// reads a 500 on every page after it — with nothing telling them to sign in again. So the
/// question is asked once per request, in the one place the credential is read, rather than
/// guarded per handler.
/// <para>The answer is a kit's (<c>CredentialProvider</c>) because the records are: a host
/// that composed no identity keeps the permissive floor and this wall stands open, the same
/// opt-in shape <c>EndpointGateMiddleware</c> has. The refusal takes the ticket with it and
/// then answers as the install answers anybody with none — and it says why, so the sign-in
/// page the browser lands on can tell the person their session is no longer valid instead of
/// looking like it forgot them.</para>
/// <para>Which arm a person actually meets depends on what the product renders, and the one
/// every NSail product ships is the 401: a WebAssembly app does no document navigation after its
/// boot, so somebody already inside meets this on a send. The ticket goes out there too, and the
/// client that was holding a session turns that 401 into a bounce to the door — carrying the
/// reason on the anonymous state it rebuilds (<c>SignInRoutes.StaleClaim</c>), because the cookie
/// the document arm would have been asked about is already gone.</para>
/// <para>Bound worth knowing: an interactive circuit that is already open built its session
/// from the document request that started it and does not pass through here again. It picks
/// the refusal up on its next document load, which is also the first thing a 500 makes
/// somebody do.</para></summary>
public sealed class CredentialMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (await Holds(context))
        {
            await next(context);

            return;
        }

        await Refuse(context);
    }

    static async Task<bool> Holds(HttpContext context)
    {
        // Read off the matched endpoint before anything is resolved, the way the two gates
        // beside this one read theirs: a file served off the manifest cannot write a row, cannot
        // render the notice and cannot be acted on by whoever reads it, and a cold load of a
        // WebAssembly app is dozens of them — the framework's own wasm, the css, the fonts. A
        // query each would be dozens of pool connections per person for an answer that changes
        // nothing. Everything else still asks, including a path that matched no endpoint at all:
        // this skips what is known to be harmless rather than admitting only what is known not
        // to be, so a host's own new endpoint is walled by default.
        if (context.GetEndpoint()?.Metadata.GetMetadata<StaticAssetDescriptor>() is not null)
        {
            return true;
        }

        // Resolved per request rather than injected, as the gates beside it are: this
        // middleware is registered by every host, and a constructor dependency on a service
        // only some compositions register fails the container's own startup validation.
        var services = context.RequestServices;

        // Nothing to check: a request carrying no credential names nobody, so there is no row
        // it could have outlived. This is also what keeps the query off the anonymous flood a
        // public door takes.
        if (services.GetService<SessionProvider>()?.Session is not { IsAuthenticated: true })
        {
            return true;
        }

        // Optional, like the gate's own marker: AddBaseWebApi does not call AddSecurity, so a
        // host that registered a SessionProvider by hand has no provider here — and that reads
        // the same as the permissive floor would, which is what such a host composes nothing to
        // replace.
        if (services.GetService<CredentialProvider>() is not { } credentials)
        {
            return true;
        }

        return await credentials.Holds(context.RequestAborted);
    }

    static async Task Refuse(HttpContext context)
    {
        // The ticket goes out with the refusal, and it cannot go out through an exception: the
        // error handler answers by clearing the response, which would wipe the Set-Cookie with
        // it (TenantClaimMiddleware's own reason). Left standing, it would be refused again on
        // the very next request, including the sign-in that would replace it.
        await context.SignOutAsync();

        // Then the install's own answer to a caller with no credential, which after the
        // sign-out is exactly what the holder is: 401 to an API, the sign-in page to a browser.
        // The mark rides the challenge so that redirect can carry the one thing this refusal
        // knows and the generic one does not — that the session died rather than expired.
        var properties = new AuthenticationProperties();

        properties.Items[SignInRoutes.StaleProperty] = "1";

        await context.ChallengeAsync(properties);
    }
}
