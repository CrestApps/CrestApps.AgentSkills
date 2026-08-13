# Liquid Examples

## Theme Layout

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}">
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

## Adding to a Zone

```liquid
{% zone "BeforeContent", position: "10" %}
  <aside class="notice">{{ "A message for visitors" | t }}</aside>
{% endzone %}
```
