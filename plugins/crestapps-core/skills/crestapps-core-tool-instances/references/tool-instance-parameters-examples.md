# Tool Instance Parameter Examples

## A Parameter-Aware Function

The source in `SKILL.md` returns this function. It merges the instance's declared parameters into its schema, resolves them per call, and places each value by its binding target (`Argument` or `Header`, as advertised in the source registration).

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Parameters;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

public sealed class OrderStatusToolFunction : AIFunction
{
    private const string ProtectionPurpose = "OrderStatusTool.Secrets";

    private readonly string _name;
    private readonly string _description;
    private readonly OrderStatusToolSettings _settings;
    private readonly IReadOnlyList<AIToolInstanceParameter> _parameters;
    private readonly JsonElement _jsonSchema;

    public OrderStatusToolFunction(
        string name,
        string description,
        OrderStatusToolSettings settings,
        AIToolInstance instance)
    {
        _name = name;
        _description = description;
        _settings = settings;
        _parameters = AIToolParameterBinder.GetParameters(instance);

        // The tool has no arguments of its own, so only declared model-filled parameters appear.
        _jsonSchema = AIToolParameterSchemaBuilder.Merge(null, _parameters);
    }

    public override string Name => _name;

    public override string Description => _description;

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var protector = arguments.Services.GetService<IDataProtectionProvider>()?.CreateProtector(ProtectionPurpose);

        var resolution = AIToolParameterBinder.Resolve(
            _parameters,
            arguments,
            arguments.Services,
            unprotect: value => protector is null ? value : protector.Unprotect(value));

        if (!resolution.Succeeded)
        {
            // Returned to the model so it can correct the call.
            return JsonSerializer.Serialize(new { success = false, error = string.Join(" ", resolution.Errors) });
        }

        var body = new JsonObject();

        foreach (var resolved in resolution.ForTarget("Argument"))
        {
            body[resolved.Binding.Name] = JsonValue.Create(resolved.StringValue);
        }

        var httpClient = arguments.Services.GetRequiredService<IHttpClientFactory>().CreateClient("OrderStatus");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_settings.BaseUrl), "orders/status"))
        {
            Content = JsonContent.Create(body),
        };

        if (!string.IsNullOrEmpty(_settings.ProtectedApiKey) && protector is not null)
        {
            request.Headers.Add("X-Api-Key", protector.Unprotect(_settings.ProtectedApiKey));
        }

        foreach (var resolved in resolution.ForTarget("Header"))
        {
            var value = resolved.StringValue;

            // Never let a value with a line break split the request.
            if (value is not null && value.IndexOfAny(['\r', '\n']) < 0)
            {
                request.Headers.TryAddWithoutValidation(resolved.Binding.Name, value);
            }
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return JsonSerializer.Serialize(new { success = response.IsSuccessStatusCode, statusCode = (int)response.StatusCode, body = content });
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
```

## Declaring Parameters on an Instance

Parameters are stored on the instance as `AIToolInstanceParametersMetadata`; the sample-host editors write them for you. A binding is `Target` or `Target:Name` (for example `Header:X-Acting-User`).

| Name | Type | Fill | Binding | Notes |
|---|---|---|---|---|
| `orderId` | `String` | `Model` | `Argument` | Required |
| `region` | `String` | `Fixed` | `Argument:region` | Pinned to `eu` |
| `actingUser` | `String` | `Context` | `Header:X-Acting-User` | Context key `user.id` |

The model sees only `orderId`. `region` and `actingUser` are filled server-side, so a prompt-injected model cannot change them.

## A Custom Context Key

```csharp
using CrestApps.Core.AI.Tooling.Parameters;
using Microsoft.Extensions.DependencyInjection;

public sealed class TenantParameterContextResolver : IAIToolParameterContextResolver
{
    public IReadOnlyList<AIToolParameterContextKey> SupportedKeys { get; } =
    [
        new AIToolParameterContextKey("tenant.id", "Tenant id", "The id of the current tenant."),
    ];

    public bool TryResolve(string contextKey, IServiceProvider services, out object value)
    {
        if (string.Equals(contextKey, "tenant.id", StringComparison.OrdinalIgnoreCase))
        {
            value = services.GetRequiredService<ITenantAccessor>().Current.Id;

            return true;
        }

        value = null;

        return false;
    }
}
```

```csharp
builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIToolParameterContextResolver, TenantParameterContextResolver>());
```

`ITenantAccessor` stands for the host's own service. Resolvers are tried in registration order and the first that handles a key wins.
