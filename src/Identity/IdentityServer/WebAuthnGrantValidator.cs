﻿using System.Security.Claims;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Fido2NetLib;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Identity.IdentityServer;

public class WebAuthnGrantValidator : BaseRequestValidator<ExtensionGrantValidationContext>, IExtensionGrantValidator
{
    public const string GrantType = "webauthn";

    private readonly IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> _assertionOptionsDataProtector;

    public WebAuthnGrantValidator(
        UserManager<User> userManager,
        IDeviceRepository deviceRepository,
        IDeviceService deviceService,
        IUserService userService,
        IEventService eventService,
        IOrganizationDuoWebTokenProvider organizationDuoWebTokenProvider,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IApplicationCacheService applicationCacheService,
        IMailService mailService,
        ILogger<CustomTokenRequestValidator> logger,
        ICurrentContext currentContext,
        GlobalSettings globalSettings,
        ISsoConfigRepository ssoConfigRepository,
        IUserRepository userRepository,
        IPolicyService policyService,
        IDataProtectorTokenFactory<SsoEmail2faSessionTokenable> tokenDataFactory,
        IDataProtectorTokenFactory<WebAuthnLoginAssertionOptionsTokenable> assertionOptionsDataProtector,
        IFeatureService featureService,
        IDistributedCache distributedCache
        )
        : base(userManager, deviceRepository, deviceService, userService, eventService,
            organizationDuoWebTokenProvider, organizationRepository, organizationUserRepository,
            applicationCacheService, mailService, logger, currentContext, globalSettings,
            userRepository, policyService, tokenDataFactory, featureService, ssoConfigRepository, distributedCache)
    {
        _assertionOptionsDataProtector = assertionOptionsDataProtector;
    }

    string IExtensionGrantValidator.GrantType => "webauthn";

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        var rawToken = context.Request.Raw.Get("token");
        var rawDeviceResponse = context.Request.Raw.Get("deviceResponse");
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(rawDeviceResponse))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant);
            return;
        }

        var verified = _assertionOptionsDataProtector.TryUnprotect(rawToken, out var token) &&
            token.TokenIsValid(WebAuthnLoginAssertionOptionsScope.Authentication);
        var deviceResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(rawDeviceResponse);

        if (!verified)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest);
            return;
        }

        var user = await _userService.CompleteWebAuthLoginAssertionAsync(token.Options, deviceResponse);
        var validatorContext = new CustomValidatorRequestContext
        {
            User = user,
            KnownDevice = await KnownDeviceAsync(user, context.Request)
        };

        await ValidateAsync(context, context.Request, validatorContext);
    }

    protected override Task<bool> ValidateContextAsync(ExtensionGrantValidationContext context,
        CustomValidatorRequestContext validatorContext)
    {
        if (validatorContext.User == null)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    protected override Task SetSuccessResult(ExtensionGrantValidationContext context, User user,
        List<Claim> claims, Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(user.Id.ToString(), "Application",
            identityProvider: "bitwarden",
            claims: claims.Count > 0 ? claims : null,
            customResponse: customResponse);
        return Task.CompletedTask;
    }

    protected override ClaimsPrincipal GetSubject(ExtensionGrantValidationContext context)
    {
        return context.Result.Subject;
    }

    protected override void SetTwoFactorResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Two factor required.",
            customResponse);
    }

    protected override void SetSsoResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Sso authentication required.",
            customResponse);
    }

    protected override void SetErrorResult(ExtensionGrantValidationContext context,
        Dictionary<string, object> customResponse)
    {
        context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, customResponse: customResponse);
    }
}
