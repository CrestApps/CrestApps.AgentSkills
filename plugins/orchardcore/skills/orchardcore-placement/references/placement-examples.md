# Orchard Core placement examples

## Example 1: Card-based editor layout

Use cards when the editor should stay in a single `Content` zone but related fields need to be visually grouped:

```json
{
  "AIProfileGeneralFields_Edit": [
    {
      "place": "Content:1%General;1"
    }
  ],
  "AIProfileDeployment_Edit": [
    {
      "place": "Content:2%Deployments;1"
    }
  ],
  "AIProfileInteractionFields_Edit": [
    {
      "place": "Content:3%Interactions;2"
    }
  ],
  "AIProfileInstructionFields_Edit": [
    {
      "place": "Content:4%Instructions;3"
    }
  ]
}
```

## Example 2: Tabs plus cards

```json
{
  "MySummary_Edit": [
    {
      "place": "Content:1#General;1%Overview;1"
    }
  ],
  "MyToolSettings_Edit": [
    {
      "place": "Content:5#Capabilities;8%Tools;3"
    }
  ],
  "MyAgentSettings_Edit": [
    {
      "place": "Content:6#Capabilities;8%Agents;2"
    }
  ]
}
```

## Example 3: Cards with columns

```json
{
  "PlacementFieldOne_Edit": [
    {
      "place": "Content:1%Layout;1|Col_4;1"
    }
  ],
  "PlacementFieldTwo_Edit": [
    {
      "place": "Content:1%Layout;1|Col_4;2"
    }
  ],
  "PlacementFieldThree_Edit": [
    {
      "place": "Content:1%Layout;1|Col_4;3"
    }
  ],
  "PlacementFieldFour_Edit": [
    {
      "place": "Content:2%Layout;1"
    }
  ]
}
```

The first three shapes render in three separate `col-md-4` wrappers on one row. The fourth shape has no column modifier, so Orchard Core renders it full-width below the row in the same card.

## Example 4: Fluent placement in a display driver

```csharp
public sealed class MySettingsDisplayDriver : SiteDisplayDriver<MySettings>
{
    public override IDisplayResult Edit(ISite site, MySettings settings, BuildEditorContext context)
    {
        return Initialize<MySettingsViewModel>("MySettings_Edit", model =>
        {
            model.Enabled = settings.Enabled;
        })
        .Location(c => c
            .Zone("Content", "4")
            .Tab("Capabilities", "8")
            .Card("Tools", "3")
            .Column("Col", "2", "4"));
    }
}
```

## Example 5: Dynamic placement provider

```csharp
public sealed class MyPlacementProvider : IShapePlacementProvider
{
    public Task<IPlacementInfoResolver> BuildPlacementInfoResolverAsync(IBuildShapeContext context)
    {
        return Task.FromResult<IPlacementInfoResolver>(new Resolver());
    }

    private sealed class Resolver : IPlacementInfoResolver
    {
        public PlacementInfo ResolvePlacement(ShapePlacementContext context)
        {
            if (context.ShapeType == "MyShape")
            {
                return new PlacementInfo
                {
                    Location = "Content:2#General;1%Details;1",
                };
            }

            return null;
        }
    }
}
```

## Example 6: Hiding whole part editor wrappers

Use `ContentPart_Edit` when the whole editor row should disappear, including the label, description, and wrapper:

```json
{
  "ContentPart_Edit": [
    {
      "differentiator": "PlacementTest-BagPart",
      "place": "-"
    },
    {
      "differentiator": "PlacementTest-FlowPart",
      "place": "-"
    },
    {
      "differentiator": "PlacementTest-WidgetsListPart",
      "place": "-"
    },
    {
      "differentiator": "PlacementTest-TitlePart",
      "place": "-"
    }
  ]
}
```

## Example 7: Field differentiators

```json
{
  "TextField": [
    {
      "differentiator": "Article-Subtitle",
      "place": "Content:2"
    }
  ],
  "TextField_Display": [
    {
      "differentiator": "Blog-Subtitle-TextField_Display__Header",
      "place": "Content:1"
    }
  ]
}
```
