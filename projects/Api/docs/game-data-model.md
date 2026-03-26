# Capitalism V — Game Data Model

## Entity Relationship Overview

```
Player (1) ──── (N) Company (1) ──── (N) Building (1) ──── (N) BuildingUnit
                       │                      │
                       │                      ├──── (N) Inventory
                       │                      │
                       └── (N) Brand          └──── City (1) ──── (N) CityResource
                                                                        │
                                                                   ResourceType
                                                                        │
ProductType (1) ──── (N) ProductRecipe ──── (1) ResourceType

GameState (singleton)
ExchangeOrder ──── Building (exchange), Company
```

## Entities

### Player
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Email | string(256) | Unique login email |
| DisplayName | string(100) | In-game name |
| PasswordHash | string | BCrypt hashed password |
| Role | string(20) | `PLAYER` or `ADMIN` |
| CreatedAtUtc | DateTime | Registration timestamp |
| LastLoginAtUtc | DateTime? | Last login timestamp |

### Company
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| PlayerId | Guid | FK → Player |
| Name | string(200) | Company name |
| Cash | decimal(18,2) | Available cash balance |
| FoundedAtUtc | DateTime | When the company was created |

### Building
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| CompanyId | Guid | FK → Company |
| CityId | Guid | FK → City |
| Type | string(30) | Building type (see BuildingType constants) |
| Name | string(200) | Display name |
| Latitude / Longitude | double | Map position |
| Level | int | Building level (default 1) |
| PowerConsumption | decimal(18,2) | Power usage in MW |
| IsForSale | bool | Whether listed for sale |
| AskingPrice | decimal? | Sale price |
| PricePerSqm | decimal? | Rent price (apartment/commercial) |
| OccupancyPercent | decimal? | Current occupancy (apartment/commercial) |
| TotalAreaSqm | decimal? | Total area (apartment/commercial) |
| PowerPlantType | string? | COAL, GAS, NUCLEAR, SOLAR, WIND |
| PowerOutput | decimal? | MW output (power plants) |
| MediaType | string? | NEWSPAPER, RADIO, TV (media houses) |
| InterestRate | decimal? | Bank interest rate |

#### Building Types
- `MINE` — Extracts raw materials
- `FACTORY` — Manufactures products from raw materials
- `SALES_SHOP` — Sells products to the public
- `RESEARCH_DEVELOPMENT` — Improves product/brand quality
- `APARTMENT` — Residential building (earns rent)
- `COMMERCIAL` — Commercial office building (earns rent)
- `MEDIA_HOUSE` — Newspaper, Radio, TV (improves brand)
- `BANK` — Lends money to players
- `EXCHANGE` — Commodity trading marketplace
- `POWER_PLANT` — Generates electricity

### BuildingUnit
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| BuildingId | Guid | FK → Building |
| UnitType | string(30) | Unit type (see below) |
| GridX / GridY | int | Position in 4×4 grid (0-3) |
| Level | int | Unit upgrade level |
| LinkRight | bool | Link to right neighbour |
| LinkDown | bool | Link to bottom neighbour |
| LinkDiagonalDown | bool | Diagonal ↘ link |
| LinkDiagonalUp | bool | Diagonal ↗ link |

#### Unit Types by Building
| Building | Allowed Units |
|----------|--------------|
| Mine | MINING, STORAGE, B2B_SALES |
| Factory | PURCHASE, MANUFACTURING, BRANDING, STORAGE, B2B_SALES |
| Sales Shop | PURCHASE, MARKETING, PUBLIC_SALES |
| R&D | PRODUCT_QUALITY, BRAND_QUALITY |

### City
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Name | string(200) | City name |
| CountryCode | string(2) | ISO 3166 alpha-2 |
| Latitude / Longitude | double | City centre coordinates |
| Population | int | Affects product demand |
| AverageRentPerSqm | decimal(18,2) | Average rent benchmark |

### ResourceType
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Name | string(100) | Resource name |
| Slug | string(100) | URL-friendly identifier (unique) |
| Category | string(30) | RAW_MATERIAL, MINERAL, ORGANIC |
| BasePrice | decimal(18,2) | Default exchange price |
| WeightPerUnit | decimal(18,4) | Transport weight |

### ProductType
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Name | string(200) | Product name |
| Slug | string(200) | URL-friendly identifier (unique) |
| Industry | string(50) | FURNITURE, FOOD_PROCESSING, HEALTHCARE, etc. |
| BasePrice | decimal(18,2) | Default market price |
| BaseCraftTicks | int | Ticks to manufacture |
| IsProOnly | bool | Requires Pro subscription |

### ProductRecipe
Links products to required raw materials.

| Field | Type | Description |
|-------|------|-------------|
| ProductTypeId | Guid | FK → ProductType |
| ResourceTypeId | Guid | FK → ResourceType |
| Quantity | decimal(18,4) | Amount needed per product unit |

### Brand
| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| CompanyId | Guid | FK → Company |
| Name | string(200) | Brand name |
| Scope | string(20) | PRODUCT, CATEGORY, COMPANY |
| Awareness | decimal(5,4) | 0.0–1.0 brand awareness level |
| Quality | decimal(5,4) | 0.0–1.0 brand quality level |

### GameState (Singleton)
| Field | Type | Description |
|-------|------|-------------|
| CurrentTick | long | Current game tick |
| TickIntervalSeconds | int | Seconds between ticks |
| TaxCycleTicks | int | Ticks between tax calculations |
| TaxRate | decimal(5,2) | Global tax rate (%) |

## Seed Data

### Resources (8 types)
Wood, Iron Ore, Coal, Gold, Chemical Minerals, Cotton, Grain, Silicon

### Cities (3 starter cities)
Bratislava (SK), Prague (CZ), Vienna (AT) — each with local resources

### Products (7 types across 3 industries)
- **Furniture**: Wooden Chair, Wooden Table, Wooden Bed
- **Food Processing**: Bread, Flour
- **Healthcare**: Basic Medicine, Bandages
