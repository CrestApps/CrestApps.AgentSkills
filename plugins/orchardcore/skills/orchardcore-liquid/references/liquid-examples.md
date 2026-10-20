# Liquid Examples

## Theme Layout

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}" dir="{{ Culture.Dir }}">
<head>
  {% resources type: "HeadMeta" %}
  {% resources type: "HeadLink" %}
</head>
<body>
  {% shape "Menu", alias: "alias:main-menu" %}
  {% render_section "Content", required: true %}
  {% resources type: "FootScript" %}
</body>
</html>
```

## Content Template

```liquid
<article>
  <h1>{{ Model.ContentItem.DisplayText }}</h1>
  <p>{{ Model.ContentItem.PublishedUtc | local | date: "%B %d, %Y" }}</p>
  {{ Model.Content.HtmlBodyPart | shape_render }}
  <p>{{ "Thank you for reading." | t }}</p>
</article>
```

## Rendering a Field Shape by Differentiator

Field shapes use the `{PartName}-{FieldName}` differentiator. Rendering the
shape applies shortcodes and sanitization (HTML and Markdown fields) or
executes the Liquid source (`LiquidField`):

```liquid
{{ Model.Content["LandingPage-Intro"] | shape_render }}
{{ Model.Content["Article-Summary"] | shape_render }}
```

## Adding to a Zone

```liquid
{% zone "BeforeContent", position: "10" %}
  <aside class="notice">{{ "A message for visitors" | t }}</aside>
{% endzone %}
```
