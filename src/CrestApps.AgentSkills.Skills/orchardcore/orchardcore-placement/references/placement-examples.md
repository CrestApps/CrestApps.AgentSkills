# Orchard Core Placement Examples

## Example 1: Blog Post Part Placement

Complete placement.json for a blog module:

```json
{
  "TitlePart": [
    {
      "displayType": "Detail",
      "place": "Header:5"
    },
    {
      "displayType": "Summary",
      "place": "Header:5"
    }
  ],
  "HtmlBodyPart": [
    {
      "displayType": "Detail",
      "place": "Content:5"
    },
    {
      "displayType": "Summary",
      "place": "-"
    }
  ],
  "SubtitlePart": [
    {
      "contentType": ["BlogPost"],
      "displayType": "Detail",
      "place": "Header:10"
    },
    {
      "contentType": ["BlogPost"],
      "displayType": "Summary",
      "place": "Header:10"
    }
  ],
  "SubtitlePart_Edit": [
    {
      "place": "Content:2"
    }
  ],
  "CommonPart_Edit": [
    {
      "place": "Parts:5"
    }
  ],
  "AutoroutePart_Edit": [
    {
      "place": "Parts:2"
    }
  ]
}
```

## Example 2: Hiding Parts for Specific Content Types

```json
{
  "HtmlBodyPart": [
    {
      "contentType": ["FAQ"],
      "place": "-"
    },
    {
      "place": "Content:5"
    }
  ],
  "AutoroutePart": [
    {
      "contentType": ["Widget"],
      "place": "-"
    }
  ]
}
```

## Example 3: Using Alternates for Content-Type-Specific Views

```json
{
  "TitlePart": [
    {
      "contentType": ["BlogPost"],
      "displayType": "Detail",
      "place": "Header:5",
      "alternates": ["TitlePart__BlogPost"]
    },
    {
      "contentType": ["Product"],
      "displayType": "Detail",
      "place": "Header:5",
      "alternates": ["TitlePart__Product"]
    }
  ]
}
```
