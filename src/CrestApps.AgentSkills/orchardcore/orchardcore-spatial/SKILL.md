---
name: orchardcore-spatial
description: Skill for working with geographic and spatial data in Orchard Core. Covers GeoPointField for location storage, geo-bounding-box queries, geo-distance queries with the Haversine formula, combined spatial queries, and geographic content filtering patterns.
---

# Orchard Core Spatial - Prompt Templates

## Work with Geographic and Spatial Data

You are an Orchard Core expert. Generate code and queries for spatial data including GeoPointField usage, geo-bounding-box queries, geo-distance queries, and combined spatial filtering with Lucene.

### Guidelines

- Enable `OrchardCore.Spatial` to add the `GeoPointField` content field.
- `GeoPointField` stores a geographic position (latitude and longitude) on content items.
- Use `geo_bounding_box` queries for fast area-based filtering without distance calculation.
- Use `geo_distance` queries for precise radius-based filtering using the Haversine formula.
- Combine `geo_bounding_box` with `geo_distance` for optimal performance on large datasets — the bounding box pre-filters records before the more expensive distance calculation runs.
- Distance values are "as the crow flies" (straight-line) because the earth is round.
- Lucene field names follow the pattern `{ContentType}.{FieldName}` (e.g., `BlogPost.Location`).
- All recipe JSON must be wrapped in `{ "steps": [...] }`.
- All C# classes must use the `sealed` modifier.

### Enabling the Spatial Feature

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "OrchardCore.Spatial"
      ],
      "disable": []
    }
  ]
}
```

### Adding a GeoPointField to a Content Type

Add a `GeoPointField` named `Location` to a content type via recipe:

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentTypes": [
        {
          "Name": "Store",
          "DisplayName": "Store",
          "Settings": {
            "ContentTypeSettings": {
              "Creatable": true,
              "Listable": true,
              "Draftable": true
            }
          },
          "ContentTypePartDefinitionRecords": [
            {
              "PartName": "Store",
              "Name": "Store",
              "Settings": {}
            }
          ]
        }
      ],
      "ContentParts": [
        {
          "Name": "Store",
          "Settings": {},
          "ContentPartFieldDefinitionRecords": [
            {
              "FieldName": "TextField",
              "Name": "Name",
              "Settings": {
                "ContentPartFieldSettings": {
                  "DisplayName": "Name"
                }
              }
            },
            {
              "FieldName": "GeoPointField",
              "Name": "Location",
              "Settings": {
                "ContentPartFieldSettings": {
                  "DisplayName": "Location"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

### Geo Bounding Box Query

A `geo_bounding_box` query finds documents within a rectangular area defined by top-left and bottom-right coordinates. This is fast and does not calculate distance from a center point.

Example: Find all `Store` content items with a `Location` field within a bounding box:

```json
{
    "query": {
        "bool": {
            "must": {
                "match_all": {}
            },
            "filter": {
                "geo_bounding_box": {
                    "Store.Location": {
                        "top_left": {
                            "lat": -33,
                            "lon": 137
                        },
                        "bottom_right": {
                            "lat": -35,
                            "lon": 139
                        }
                    }
                }
            }
        }
    }
}
```

This returns content items whose `Location` falls within the rectangle bounded by:
- **Top-left**: latitude -33, longitude 137
- **Bottom-right**: latitude -35, longitude 139

### Geo Distance Query

A `geo_distance` query finds documents within a specified radius of a central point. It uses the **Haversine formula** to calculate the distance between each document and the center point.

Example: Find all `Store` content items within 200km of a point:

```json
{
    "query": {
        "bool": {
            "must": {
                "match_all": {}
            },
            "filter": {
                "geo_distance": {
                    "distance": "200km",
                    "Store.Location": {
                        "lat": -34,
                        "lon": 138
                    }
                }
            }
        }
    }
}
```

A 200km radius is approximately 1.7986 degrees of arc from the center point.

### Distance Units

| Unit | Example | Description |
|------|---------|-------------|
| `km` | `"200km"` | Kilometers |
| `mi` | `"100mi"` | Miles |
| `m` | `"5000m"` | Meters |

### Combined Bounding Box and Distance Query

For large datasets, combine `geo_bounding_box` with `geo_distance` for optimal performance. The bounding box pre-filters the dataset to a smaller region, then the distance calculation runs only on the filtered results:

```json
{
    "query": {
        "bool": {
            "must": [
                {
                    "term": {
                        "Content.ContentItem.ContentType.keyword": "Store"
                    }
                },
                {
                    "bool": {
                        "filter": {
                            "geo_distance": {
                                "distance": "200km",
                                "Store.Location": {
                                    "lat": -33,
                                    "lon": 137
                                }
                            }
                        }
                    }
                }
            ],
            "filter": {
                "geo_bounding_box": {
                    "Store.Location": {
                        "top_left": {
                            "lat": -31,
                            "lon": 135
                        },
                        "bottom_right": {
                            "lat": -35,
                            "lon": 139
                        }
                    }
                }
            }
        }
    }
}
```

### Query Strategy Comparison

| Query Type | Speed | Precision | Use Case |
|------------|-------|-----------|----------|
| `geo_bounding_box` | Fast | Rectangular area only | Quick area filtering, map viewport queries |
| `geo_distance` | Slower (Haversine) | Exact radius | Precise "within X km" searches |
| Combined | Optimal | Exact radius with pre-filter | Large datasets requiring radius search |

### Haversine Formula

The `geo_distance` query uses the Haversine formula to calculate the great-circle distance between two points on a sphere. Key characteristics:

- Calculates distance "as the crow flies" (straight line over the earth's surface).
- More computationally expensive than bounding box checks.
- Results are accurate for all distances but assume a spherical earth.
- For optimal performance on large datasets, always pre-filter with a `geo_bounding_box` before applying `geo_distance`.

### Content Type with Multiple Spatial Fields

A content type can have multiple `GeoPointField` instances for different geographic attributes:

```json
{
  "steps": [
    {
      "name": "ContentDefinition",
      "ContentParts": [
        {
          "Name": "DeliveryRoute",
          "Settings": {},
          "ContentPartFieldDefinitionRecords": [
            {
              "FieldName": "GeoPointField",
              "Name": "Origin",
              "Settings": {
                "ContentPartFieldSettings": {
                  "DisplayName": "Origin Location"
                }
              }
            },
            {
              "FieldName": "GeoPointField",
              "Name": "Destination",
              "Settings": {
                "ContentPartFieldSettings": {
                  "DisplayName": "Destination Location"
                }
              }
            }
          ]
        }
      ]
    }
  ]
}
```

Query by origin location:

```json
{
    "query": {
        "bool": {
            "must": {
                "match_all": {}
            },
            "filter": {
                "geo_distance": {
                    "distance": "50km",
                    "DeliveryRoute.Origin": {
                        "lat": 40.7128,
                        "lon": -74.0060
                    }
                }
            }
        }
    }
}
```
