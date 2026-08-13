---
name: orchardcore-recaptcha
description: Skill for protecting Orchard Core endpoints and forms with Google reCAPTCHA. Covers ReCaptchaSettings, ReCaptchaShape, the ReCaptcha shape, the captcha tag helper, form protection, controller validation, recipe configuration, and Users login registration and password reset protection. Use this skill when requests mention Orchard Core ReCaptcha, Google reCAPTCHA, ValidateReCaptchaAttribute, ReCaptchaSettings, Forms reCAPTCHA field, login CAPTCHA, or closely related Orchard Core implementation setup extension or troubleshooting work. Strong matches include OrchardCore.ReCaptcha, OrchardCore.ReCaptcha.Users, OrchardCore.ReCaptcha.Configuration, OrchardCore.ReCaptcha.ActionFilters, ReCaptchaService, ReCaptchaPart, IRegistrationFormEvents, ILoginFormEvent, and IPasswordRecoveryFormEvents. It also helps with forms, workflows, settings recipes, and the source-backed patterns captured in this skill.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Orchard Core ReCaptcha

The `OrchardCore.ReCaptcha` feature supplies Google reCAPTCHA rendering and
verification. Add `OrchardCore.ReCaptcha.Users` when built-in user login,
registration, and password recovery pages also need protection. It is not a
replacement for authorization, rate limiting, or server-side input validation.

## Guidelines

- Enable `OrchardCore.ReCaptcha` for the base service, settings, and shape.
- Enable `OrchardCore.ReCaptcha.Users` to protect user forms; it depends on the base feature and Users.
- Enable the Users registration or reset-password features separately when those pages must be protected.
- Store the site key and secret in secure configuration for production, not source control.
- Use the `ReCaptcha` shape or the `<captcha />` tag helper to render a challenge.
- Use `ValidateReCaptchaAttribute` only for form posts that actually render and submit the challenge token.
- The Forms integration requires both `OrchardCore.Forms` and `OrchardCore.ReCaptcha`.
- The workflow validation task requires both `OrchardCore.Workflows` and `OrchardCore.ReCaptcha`.
- `ReCaptchaService` verifies the server response and retries its HTTP calls through the configured resilient client.
- All recipe JSON uses the root `{ "steps": [...] }` format.

## Enable the Base and Users Features

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.ReCaptcha",
        "OrchardCore.ReCaptcha.Users"
      ],
      "disable": []
    }
  ]
}
```

The Users feature registers `IRegistrationFormEvents`, `ILoginFormEvent`, and
`IPasswordRecoveryFormEvents`. With the relevant Users subfeatures enabled it
also provides display drivers for registration, forgot-password, and
reset-password forms.

## Configure ReCaptcha Settings

Navigate to **Configuration → Settings → ReCaptcha** and set the site key and
secret key. `ReCaptchaSettings` exposes `SiteKey`, `SecretKey`,
`ReCaptchaScriptUri`, and `ReCaptchaApiUri`. A usable configuration requires
the keys and verification URI.

Configure a recipe with the `Settings` step:

```json
{
  "steps": [
    {
      "name": "Settings",
      "ReCaptchaSettings": {
        "SiteKey": "your-site-key",
        "SecretKey": "your-secret-key",
        "ReCaptchaScriptUri": "https://www.google.com/recaptcha/api.js",
        "ReCaptchaApiUri": "https://www.google.com/recaptcha/api/siteverify"
      }
    }
  ]
}
```

Override tenant settings through configuration by calling
`ConfigureReCaptchaSettings()` while building the host:

```csharp
builder.ConfigureReCaptchaSettings();
```

```json
{
  "OrchardCore_ReCaptcha": {
    "SiteKey": "your-site-key",
    "SecretKey": "your-secret-key"
  }
}
```

## Protect a Custom MVC Form

Render the `ReCaptcha` shape in the form and validate the posted token on the action.
The attribute adds a model-state error named `ReCaptcha` when verification
fails, so the action must return the model when `ModelState` is invalid.

```cshtml
<form asp-action="Submit" method="post">
    <input asp-for="Email" />
    <shape type="ReCaptcha" language="en-US" />
    <button type="submit">Submit</button>
</form>
```

```csharp
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ReCaptcha.ActionFilters;

namespace MyModule.Controllers;

public sealed class ContactController : Controller
{
    [HttpPost]
    [ValidateReCaptcha]
    public IActionResult Submit(ContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        return RedirectToAction(nameof(Complete));
    }

    public IActionResult Complete() => View();
}
```

For a client-side JSON post, send the generated token in the
`g-recaptcha-response` request header when a workflow task consumes it.

## Render the Shape

Use the shape when building Liquid or Razor output instead of hand-writing the
provider markup. The shape always renders the challenge when settings are
configured:

```liquid
{% shape "ReCaptcha", language: "en-US" %}
```

```cshtml
<shape type="ReCaptcha" language="en-US" />
```

## Forms and Workflows

In a Form content type, add the ReCaptcha form part after enabling Forms and
ReCaptcha. The module registers `ReCaptchaPart` and its display driver, which renders the
challenge:

```html
<captcha />
```

The **Validate ReCaptcha** workflow task is available only when Workflows is
also enabled. It validates the challenge submitted with the request and is
appropriate for a Forms workflow, not for bypassing server-side validation.
