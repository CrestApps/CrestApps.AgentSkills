# Menu Examples

## Nested Menu Recipe

```json
{
  "steps": [
    {
      "name": "content",
      "data": [
        {
          "ContentItemId": "4j42fxryjcpqkkacmg6cwz3pr5",
          "ContentType": "Menu",
          "DisplayText": "Main Menu",
          "Latest": true,
          "Published": true,
          "MenuPart": {},
          "TitlePart": {
            "Title": "Main Menu"
          },
          "MenuItemsListPart": {
            "MenuItems": [
              {
                "ContentItemId": "4j42fxryjcpqkkacmg6cwz3pr6",
                "ContentType": "LinkMenuItem",
                "DisplayText": "Products",
                "LinkMenuItemPart": {
                  "Url": "~/products"
                },
                "MenuItemsListPart": {
                  "MenuItems": [
                    {
                      "ContentItemId": "4j42fxryjcpqkkacmg6cwz3pr7",
                      "ContentType": "LinkMenuItem",
                      "DisplayText": "Software",
                      "LinkMenuItemPart": {
                        "Url": "~/products/software"
                      }
                    }
                  ]
                }
              }
            ]
          }
        }
      ]
    }
  ]
}
```

## Liquid Alternate

`Views/Menu-MainMenu.liquid`:

```liquid
<nav class="navbar" aria-label="{{ Model.MenuName }}">
  {% for item in Model.Items %}
    {{ item | shape_render }}
  {% endfor %}
</nav>
```
