using Api.Data.Entities;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private static IEnumerable<ProductSeed> GetFurnitureProducts()
    {
        yield return Product("Wood Planks", "wood-planks", Industry.Furniture, 18m, 1, "Cut and dried timber planks for furniture and interior fittings.", "Plank", "planks", 40m, 0.4m, ResourceIngredient("wood", 1m));
        yield return Product("Wooden Chair", "wooden-chair", Industry.Furniture, 45m, 2, "A basic wooden chair suited for starter furniture production.", "Chair", "chairs", 20m, 1m, ResourceIngredient("wood", 1m));
        yield return Product("Wooden Table", "wooden-table", Industry.Furniture, 120m, 3, "A classic dining table and one of the first scalable furniture products.", "Table", "tables", 10m, 1m, ResourceIngredient("wood", 1m));
        yield return Product("Wooden Bed", "wooden-bed", Industry.Furniture, 200m, 4, "A comfortable wooden bed frame for mass-market housing.", "Bed", "beds", 5m, 1m, ResourceIngredient("wood", 1m));

        foreach (var (name, slug, price, ticks, output, energy, planks, fasteners, description) in new[]
        {
            ("Wooden Stool", "wooden-stool", 32m, 2, 12m, 0.8m, 8m, 1m, "Simple low-cost wooden seating with fast throughput."),
            ("Bookshelf", "bookshelf", 95m, 3, 4m, 0.9m, 14m, 2m, "A mid-market bookshelf assembled from planks and brackets."),
            ("Nightstand", "nightstand", 70m, 3, 6m, 0.8m, 10m, 1m, "Compact bedside storage made from finished timber sections."),
            ("Office Desk", "office-desk", 160m, 4, 4m, 1.2m, 16m, 3m, "Commercial desk with cable management and sturdy frame."),
            ("Dining Bench", "dining-bench", 85m, 3, 4m, 0.9m, 12m, 2m, "Bench seating for residential dining sets."),
            ("Wardrobe", "wardrobe", 250m, 5, 2m, 1.5m, 22m, 4m, "Tall clothing cabinet for the consumer home market."),
            ("Dresser", "dresser", 210m, 5, 2m, 1.5m, 20m, 4m, "Bedroom dresser with multiple drawers and reinforced slides."),
            ("Coffee Table", "coffee-table", 90m, 3, 4m, 0.9m, 11m, 2m, "Low-profile living-room table produced in compact batches."),
            ("Bunk Bed", "bunk-bed", 340m, 6, 1m, 1.8m, 30m, 6m, "Space-saving stacked bed frame for residential and dormitory use."),
            ("Crib", "crib", 140m, 4, 2m, 1m, 14m, 2m, "Starter family furniture item with compact dimensions."),
            ("Patio Chair", "patio-chair", 60m, 3, 6m, 1m, 9m, 2m, "Outdoor chair with reinforced joints for weather exposure."),
            ("Patio Table", "patio-table", 150m, 4, 3m, 1.2m, 15m, 3m, "Outdoor table assembled from treated planks and metal hardware."),
            ("Door Frame", "door-frame", 110m, 3, 3m, 1m, 13m, 2m, "Interior frame for commercial and residential buildings."),
            ("Sofa Frame", "sofa-frame", 175m, 4, 2m, 1.3m, 18m, 3m, "Wooden support frame for upholstered living-room furniture."),
            ("TV Stand", "tv-stand", 135m, 4, 3m, 1.1m, 14m, 2m, "Cabinet-style stand for home media equipment.")
        })
        {
            yield return Product(name, slug, Industry.Furniture, price, ticks, description, "Piece", "pcs", output, energy, ProductIngredient("wood-planks", planks), ProductIngredient("iron-fasteners", fasteners));
        }

        yield return Product("Filing Cabinet", "filing-cabinet", Industry.Furniture, 180m, 4, "Office storage furniture reinforced with metal rails.", "Piece", "pcs", 3m, 1.2m, ProductIngredient("wood-planks", 10m), ProductIngredient("steel-panel", 2m), ProductIngredient("iron-fasteners", 2m));
        yield return Product("Window Frame", "window-frame", Industry.Furniture, 105m, 3, "Finished wood frame designed for glazing insertion.", "Piece", "pcs", 4m, 1m, ProductIngredient("wood-planks", 10m), ProductIngredient("glass-pane", 1m), ProductIngredient("iron-fasteners", 2m));
        yield return Product("Dining Set", "dining-set", Industry.Furniture, 480m, 6, "Bundled premium set combining table and chair production.", "Set", "sets", 1m, 2.2m, ProductIngredient("wooden-table", 1m), ProductIngredient("wooden-chair", 4m));
        yield return Product("Electronic Table", "electronic-table", Industry.Furniture, 520m, 6, "Smart office table with built-in electronics and connected features.", "Piece", "pcs", 1m, 2m, ResourceIngredient("wood", 1m), ProductIngredient("iron-fasteners", 10m), ProductIngredient("electronic-components", 10m));
    }

    private static IEnumerable<ProductSeed> GetFoodProducts()
    {
        yield return Product("Flour", "flour", Industry.FoodProcessing, 8m, 1, "Milled grain flour used in baking, noodle making, and packaged food.", "Bag", "bags", 10m, 0.4m, ResourceIngredient("grain", 1m));
        yield return Product("Bread", "bread", Industry.FoodProcessing, 3m, 1, "Basic bread loaf and one of the simplest onboarding products.", "Loaf", "loaves", 12m, 0.5m, ResourceIngredient("grain", 1m));

        foreach (var (name, slug, price, ticks, output, energy, flourQuantity, description) in new[]
        {
            ("Pasta", "pasta", 9m, 2, 16m, 0.7m, 2m, "Dry pasta manufactured from grain flour in large volumes."),
            ("Noodles", "noodles", 8m, 2, 18m, 0.6m, 2m, "Shelf-stable noodle packs for retail and wholesale markets."),
            ("Crackers", "crackers", 6m, 2, 20m, 0.6m, 1m, "Baked snack crackers produced from seasoned flour dough."),
            ("Pancake Mix", "pancake-mix", 11m, 2, 12m, 0.6m, 2m, "Convenience baking mix for pancakes and waffles."),
            ("Cake Mix", "cake-mix", 12m, 2, 12m, 0.7m, 2m, "Packaged baking mix for retail bakery goods."),
            ("Bakery Premix", "bakery-premix", 13m, 2, 8m, 0.7m, 3m, "Bulk flour blend for industrial bakery clients."),
            ("Biscuit Pack", "biscuit-pack", 9m, 2, 16m, 0.6m, 1m, "Retail biscuit pack with high throughput and stable demand."),
            ("Sandwich Bread", "sandwich-bread", 4m, 1, 14m, 0.5m, 1m, "Soft loaf product optimized for grocery chains."),
            ("Toast Bread", "toast-bread", 4m, 1, 14m, 0.5m, 1m, "Consistent sliced bread for breakfast and hospitality channels.")
        })
        {
            yield return Product(name, slug, Industry.FoodProcessing, price, ticks, description, "Pack", "packs", output, energy, ProductIngredient("flour", flourQuantity));
        }

        foreach (var (name, slug, price, ticks, output, energy, grainQuantity, description) in new[]
        {
            ("Cereal Flakes", "cereal-flakes", 7m, 2, 14m, 0.7m, 1m, "Packaged breakfast cereal made from processed grain."),
            ("Semolina", "semolina", 9m, 1, 10m, 0.4m, 1m, "Durum-style coarse flour used in premium food production."),
            ("Porridge Mix", "porridge-mix", 6m, 1, 14m, 0.3m, 1m, "Simple grain breakfast mix for mass-market distribution."),
            ("Grain Bars", "grain-bars", 10m, 2, 18m, 0.6m, 1m, "Packaged grain snack bars sold in multipacks."),
            ("Animal Feed", "animal-feed", 7m, 1, 10m, 0.3m, 1m, "Low-margin but reliable feed product for agribusiness buyers."),
            ("Bran Bags", "bran-bags", 4m, 1, 10m, 0.2m, 1m, "By-product bagged for livestock and industrial buyers.")
        })
        {
            yield return Product(name, slug, Industry.FoodProcessing, price, ticks, description, "Bag", "bags", output, energy, ResourceIngredient("grain", grainQuantity));
        }

        yield return Product("Breadcrumbs", "breadcrumbs", Industry.FoodProcessing, 5m, 1, "Crumb ingredient made from dried bread loaves.", "Bag", "bags", 10m, 0.3m, ProductIngredient("bread", 2m));
        yield return Product("Pasta Kit", "pasta-kit", Industry.FoodProcessing, 14m, 2, "Retail kit bundling pasta portions for home cooking.", "Kit", "kits", 12m, 0.7m, ProductIngredient("pasta", 2m));
        yield return Product("Snack Crackers", "snack-crackers", Industry.FoodProcessing, 7m, 2, "Premium cracker line with retail-ready packaging.", "Box", "boxes", 18m, 0.6m, ProductIngredient("crackers", 1m));
    }

    private static IEnumerable<ProductSeed> GetHealthcareProducts()
    {
        yield return Product("Bandages", "bandages", Industry.Healthcare, 15m, 1, "Basic wound-care bandages and a simple onboarding healthcare product.", "Pack", "packs", 20m, 0.3m, ResourceIngredient("cotton", 1m));
        yield return Product("Basic Medicine", "basic-medicine", Industry.Healthcare, 50m, 3, "Essential pharmaceutical product for starter healthcare chains.", "Bottle", "bottles", 8m, 1m, ResourceIngredient("chemical-minerals", 1m));

        foreach (var (name, slug, price, ticks, output, energy, description) in new[]
        {
            ("Antiseptic", "antiseptic", 24m, 2, 12m, 0.8m, "Disinfecting liquid used in wound treatment and surgical care."),
            ("Pain Relief Tablets", "pain-relief-tablets", 34m, 2, 14m, 0.9m, "Mass-market analgesic tablets packed for pharmacies."),
            ("Cough Syrup", "cough-syrup", 28m, 2, 10m, 0.8m, "Liquid cough relief product for pharmacies and clinics."),
            ("Vitamin Pack", "vitamin-pack", 26m, 2, 12m, 0.8m, "Supplement packs blended from mineral compounds."),
            ("Cold Pack", "cold-pack", 12m, 1, 16m, 0.4m, "Instant cold-compress product for sports and emergency use."),
            ("Saline Kit", "saline-kit", 18m, 2, 10m, 0.7m, "Sterile saline pack for hospitals and laboratories."),
            ("Healing Ointment", "healing-ointment", 30m, 2, 14m, 0.8m, "Topical wound treatment cream sold in tubes."),
            ("Allergy Tablets", "allergy-tablets", 27m, 2, 14m, 0.8m, "Seasonal allergy medication for pharmacy shelves."),
            ("Mineral Supplement", "mineral-supplement", 25m, 2, 10m, 0.8m, "Supplement tablets blended from purified mineral inputs.")
        })
        {
            yield return Product(name, slug, Industry.Healthcare, price, ticks, description, "Pack", "packs", output, energy, ResourceIngredient("chemical-minerals", 1m));
        }

        foreach (var (name, slug, price, ticks, output, energy, cottonQuantity, description) in new[]
        {
            ("Sterile Gauze", "sterile-gauze", 18m, 1, 16m, 0.4m, 1m, "Medical gauze made from refined cotton fibres."),
            ("Surgical Masks", "surgical-masks", 21m, 2, 24m, 0.6m, 1m, "Disposable masks sold to clinics and pharmacies."),
            ("Cotton Swabs", "cotton-swabs", 8m, 1, 30m, 0.3m, 0.5m, "Disposable cotton swabs for healthcare and cosmetic use."),
            ("Surgical Tape", "surgical-tape", 11m, 1, 20m, 0.3m, 0.5m, "Adhesive medical tape for wound dressing and equipment fastening."),
            ("Compression Wrap", "compression-wrap", 16m, 1, 14m, 0.4m, 0.8m, "Elastic cotton wrap used in sports and clinic recovery treatment.")
        })
        {
            yield return Product(name, slug, Industry.Healthcare, price, ticks, description, "Pack", "packs", output, energy, ResourceIngredient("cotton", cottonQuantity));
        }

        yield return Product("Medical Gloves", "medical-gloves", Industry.Healthcare, 22m, 2, "Protective gloves for healthcare and laboratory use.", "Box", "boxes", 20m, 0.6m, ResourceIngredient("chemical-minerals", 0.5m), ResourceIngredient("cotton", 0.2m));
        yield return Product("Disinfectant Wipes", "disinfectant-wipes", Industry.Healthcare, 19m, 2, "Pre-soaked wipes for cleaning and sanitation.", "Pack", "packs", 18m, 0.7m, ResourceIngredient("cotton", 0.5m), ProductIngredient("antiseptic", 1m));
        yield return Product("First Aid Kit", "first-aid-kit", Industry.Healthcare, 42m, 2, "Retail first-aid kit assembled from basic medical supplies.", "Kit", "kits", 8m, 0.6m, ProductIngredient("bandages", 2m), ProductIngredient("antiseptic", 1m), ProductIngredient("cotton-swabs", 1m));
        yield return Product("Wound Dressing Kit", "wound-dressing-kit", Industry.Healthcare, 38m, 2, "Advanced dressing bundle for hospitals and clinics.", "Kit", "kits", 8m, 0.7m, ProductIngredient("bandages", 2m), ProductIngredient("sterile-gauze", 2m), ProductIngredient("surgical-tape", 1m));
    }

    private static IEnumerable<ProductSeed> GetElectronicsProducts()
    {
        // Starter electronics products — all use Silicon directly so they work as
        // onboarding factory configurations (purchase → manufacture → sell).
        yield return Product("Basic Electronics", "basic-electronics", Industry.Electronics, 45m, 3, "A starter pack of electronic components assembled from raw silicon. The entry point for any electronics manufacturer.", "Pack", "packs", 12m, 1.0m, ResourceIngredient("silicon", 1m));
        yield return Product("LED Screen", "led-screen", Industry.Electronics, 85m, 4, "A flat-panel LED display made from silicon. High-margin starter product for premium retail channels.", "Display", "displays", 6m, 1.3m, ResourceIngredient("silicon", 1m));
        yield return Product("Circuit Board", "circuit-board", Industry.Electronics, 55m, 3, "A populated circuit board assembled from silicon. Core platform for advanced electronics assemblies.", "Board", "boards", 10m, 1.1m, ResourceIngredient("silicon", 2m));

        yield return Product("Silicon Wafer", "silicon-wafer", Industry.Electronics, 22m, 2, "Processed silicon wafer used as the basis for chips and sensors.", "Wafer", "wafers", 12m, 0.9m, ResourceIngredient("silicon", 1m));
        yield return Product("Glass Pane", "glass-pane", Industry.Electronics, 18m, 2, "Industrial glass pane made from processed silicon.", "Pane", "panes", 12m, 0.8m, ResourceIngredient("silicon", 1m));
        yield return Product("Gold Contact", "gold-contact", Industry.Electronics, 65m, 2, "Precision gold contact used in high-end electronic assemblies.", "Set", "sets", 10m, 0.8m, ResourceIngredient("gold", 0.2m));
        yield return Product("Electronic Components", "electronic-components", Industry.Electronics, 48m, 3, "Mixed component pack for smart devices, controls, and electronics furniture.", "Pack", "packs", 16m, 1.2m, ProductIngredient("silicon-wafer", 1m), ProductIngredient("gold-contact", 1m));
        yield return Product("Sensor Module", "sensor-module", Industry.Electronics, 72m, 4, "Compact sensor package for automation and smart-home products.", "Module", "modules", 8m, 1.4m, ProductIngredient("electronic-components", 3m), ProductIngredient("circuit-board", 1m));
        yield return Product("Battery Pack", "battery-pack", Industry.Electronics, 46m, 3, "Rechargeable battery pack using treated carbon and chemical inputs.", "Pack", "packs", 10m, 1.3m, ResourceIngredient("coal", 1m), ResourceIngredient("chemical-minerals", 1m));
        yield return Product("LED Lamp", "led-lamp", Industry.Electronics, 28m, 3, "Energy-efficient lighting unit for retail and construction buyers.", "Piece", "pcs", 12m, 1m, ProductIngredient("electronic-components", 2m), ProductIngredient("glass-pane", 1m));
        yield return Product("Power Adapter", "power-adapter", Industry.Electronics, 32m, 3, "External adapter unit for electronics and office equipment.", "Piece", "pcs", 10m, 1.1m, ProductIngredient("electronic-components", 2m), ProductIngredient("iron-fasteners", 1m));
        yield return Product("Radio Set", "radio-set", Industry.Electronics, 85m, 4, "Compact radio receiver for consumer retail distribution.", "Piece", "pcs", 6m, 1.5m, ProductIngredient("circuit-board", 1m), ProductIngredient("electronic-components", 3m), ProductIngredient("gold-contact", 1m));
        yield return Product("Desk Speaker", "desk-speaker", Industry.Electronics, 78m, 4, "Desktop audio speaker with compact amplifier internals.", "Piece", "pcs", 6m, 1.5m, ProductIngredient("circuit-board", 1m), ProductIngredient("electronic-components", 2m));
        yield return Product("Calculator", "calculator", Industry.Electronics, 26m, 3, "Simple consumer calculator with low input complexity.", "Piece", "pcs", 12m, 1m, ProductIngredient("electronic-components", 2m), ProductIngredient("glass-pane", 1m));
        yield return Product("Smart Home Hub", "smart-home-hub", Industry.Electronics, 140m, 5, "Connected automation hub with premium electronic internals.", "Piece", "pcs", 4m, 1.8m, ProductIngredient("circuit-board", 2m), ProductIngredient("electronic-components", 4m), ProductIngredient("signal-amplifier", 1m));
        yield return Product("Solar Cell", "solar-cell", Industry.Electronics, 58m, 4, "Photovoltaic cell used in energy and construction assemblies.", "Cell", "cells", 8m, 1.6m, ProductIngredient("silicon-wafer", 2m), ProductIngredient("gold-contact", 1m));
        yield return Product("Control Panel", "control-panel", Industry.Electronics, 95m, 4, "Industrial control panel for buildings and machinery.", "Panel", "panels", 5m, 1.7m, ProductIngredient("circuit-board", 2m), ProductIngredient("electronic-components", 3m), ProductIngredient("iron-fasteners", 2m));
        yield return Product("Industrial Relay", "industrial-relay", Industry.Electronics, 38m, 3, "Switching relay for control systems and automation.", "Piece", "pcs", 12m, 1m, ProductIngredient("gold-contact", 1m), ProductIngredient("electronic-components", 2m));
        yield return Product("Touch Display", "touch-display", Industry.Electronics, 115m, 5, "Glass-fronted display assembly for smart devices and kiosks.", "Display", "displays", 4m, 1.9m, ProductIngredient("glass-pane", 1m), ProductIngredient("circuit-board", 1m), ProductIngredient("gold-contact", 1m));
        yield return Product("Signal Amplifier", "signal-amplifier", Industry.Electronics, 68m, 4, "Amplifier module used in hubs, radios, and broadcast equipment.", "Module", "modules", 6m, 1.4m, ProductIngredient("circuit-board", 1m), ProductIngredient("electronic-components", 2m));
        yield return Product("Network Router", "network-router", Industry.Electronics, 88m, 4, "Consumer and small-office router with multi-component internals.", "Piece", "pcs", 6m, 1.6m, ProductIngredient("circuit-board", 1m), ProductIngredient("electronic-components", 3m), ProductIngredient("signal-amplifier", 1m));
        yield return Product("LED Bulb Pack", "led-bulb-pack", Industry.Electronics, 24m, 3, "Retail multi-pack of LED bulbs for home improvement channels.", "Pack", "packs", 10m, 1m, ProductIngredient("led-lamp", 2m));
        yield return Product("Meter Module", "meter-module", Industry.Electronics, 64m, 4, "Measurement module used in utilities and smart industrial systems.", "Module", "modules", 6m, 1.5m, ProductIngredient("sensor-module", 1m), ProductIngredient("circuit-board", 1m));
    }

    private static IEnumerable<ProductSeed> GetConstructionProducts()
    {
        yield return Product("Steel Ingot", "steel-ingot", Industry.Construction, 30m, 2, "Processed iron-and-coal metal stock used for structural production.", "Ingot", "ingots", 20m, 1.2m, ResourceIngredient("iron-ore", 1m), ResourceIngredient("coal", 1m));
        yield return Product("Steel Beam", "steel-beam", Industry.Construction, 70m, 3, "Structural steel beam for industrial and commercial buildings.", "Beam", "beams", 8m, 1.4m, ProductIngredient("steel-ingot", 2m));
        yield return Product("Iron Nails", "iron-nails", Industry.Construction, 12m, 1, "Standard nail box used in furniture and building assembly.", "Box", "boxes", 25m, 0.5m, ResourceIngredient("iron-ore", 0.5m));
        yield return Product("Iron Fasteners", "iron-fasteners", Industry.Construction, 18m, 2, "Precision fastener batch measured in kilograms for assembly lines.", "Kilogram", "kg", 20m, 0.7m, ResourceIngredient("iron-ore", 1m));
        yield return Product("Screws Box", "screws-box", Industry.Construction, 16m, 2, "Box of threaded screws for modular assembly and construction.", "Box", "boxes", 20m, 0.6m, ProductIngredient("iron-fasteners", 5m));
        yield return Product("Wood Panel", "wood-panel", Industry.Construction, 26m, 2, "Finished wooden panel for modular interiors and building shells.", "Panel", "panels", 12m, 0.7m, ProductIngredient("wood-planks", 3m));
        yield return Product("Insulation Roll", "insulation-roll", Industry.Construction, 22m, 2, "Insulation material made from processed cotton fibres.", "Roll", "rolls", 10m, 0.6m, ResourceIngredient("cotton", 1m));
        yield return Product("Glass Window", "glass-window", Industry.Construction, 42m, 3, "Finished glazed window for residential and commercial projects.", "Window", "windows", 6m, 1m, ProductIngredient("glass-pane", 2m), ProductIngredient("window-frame", 1m));
        yield return Product("Roofing Sheet", "roofing-sheet", Industry.Construction, 34m, 3, "Metal roofing section for warehouse and industrial builds.", "Sheet", "sheets", 10m, 1m, ProductIngredient("steel-ingot", 1m));
        yield return Product("Cable Duct", "cable-duct", Industry.Construction, 20m, 2, "Rigid duct section for power and data routing in buildings.", "Section", "sections", 12m, 0.7m, ProductIngredient("steel-ingot", 1m));
        yield return Product("Wall Panel", "wall-panel", Industry.Construction, 36m, 3, "Finished wall panel assembled from timber and insulation layers.", "Panel", "panels", 8m, 1m, ProductIngredient("wood-panel", 2m), ProductIngredient("insulation-roll", 1m));
        yield return Product("Support Column", "support-column", Industry.Construction, 65m, 4, "Load-bearing column for industrial and commercial structures.", "Column", "columns", 4m, 1.5m, ProductIngredient("steel-beam", 2m));
        yield return Product("Scaffold Kit", "scaffold-kit", Industry.Construction, 88m, 4, "Reusable scaffolding kit for construction contractors.", "Kit", "kits", 4m, 1.5m, ProductIngredient("steel-beam", 1m), ProductIngredient("iron-fasteners", 5m));
        yield return Product("Gate Frame", "gate-frame", Industry.Construction, 54m, 3, "Metal gate frame for warehouse and commercial property use.", "Frame", "frames", 6m, 1.1m, ProductIngredient("steel-ingot", 1m), ProductIngredient("iron-fasteners", 3m));
        yield return Product("Warehouse Rack", "warehouse-rack", Industry.Construction, 95m, 4, "Heavy-duty rack system for storage and logistics buildings.", "Rack", "racks", 4m, 1.4m, ProductIngredient("steel-beam", 1m), ProductIngredient("iron-fasteners", 4m));
        yield return Product("Solar Roof Tile", "solar-roof-tile", Industry.Construction, 145m, 5, "Integrated roofing tile with embedded photovoltaic cell.", "Tile", "tiles", 4m, 1.9m, ProductIngredient("roofing-sheet", 1m), ProductIngredient("solar-cell", 1m));
        yield return Product("Safety Railing", "safety-railing", Industry.Construction, 32m, 2, "Protective railing for industrial and commercial facilities.", "Section", "sections", 8m, 0.8m, ProductIngredient("steel-ingot", 1m), ProductIngredient("iron-fasteners", 2m));
        yield return Product("Steel Panel", "steel-panel", Industry.Construction, 40m, 3, "Flat steel panel used in cabinets, junction boxes, and doors.", "Panel", "panels", 8m, 1m, ProductIngredient("steel-ingot", 1m));
        yield return Product("Junction Box", "junction-box", Industry.Construction, 28m, 2, "Electrical junction housing used in construction projects.", "Box", "boxes", 10m, 0.8m, ProductIngredient("steel-panel", 1m), ProductIngredient("electronic-components", 1m));
        yield return Product("Ventilation Grille", "ventilation-grille", Industry.Construction, 24m, 2, "Steel ventilation grille for building airflow systems.", "Piece", "pcs", 12m, 0.7m, ProductIngredient("steel-panel", 1m));
        yield return Product("Steel Door", "steel-door", Industry.Construction, 80m, 4, "Durable steel security door for commercial and industrial use.", "Door", "doors", 4m, 1.5m, ProductIngredient("steel-panel", 2m), ProductIngredient("iron-fasteners", 4m));
        yield return Product("Pipe Section", "pipe-section", Industry.Construction, 30m, 2, "Standardized pipe section for ventilation and utility networks.", "Section", "sections", 10m, 0.9m, ProductIngredient("steel-ingot", 1m));
        yield return Product("Assembly Pallet", "assembly-pallet", Industry.Construction, 18m, 1, "Transport pallet for factories, warehouses, and supply chains.", "Pallet", "pallets", 12m, 0.4m, ProductIngredient("wood-planks", 3m));
    }

    private static IEnumerable<ProductSeed> GetPharmaceuticalsProducts()
    {
        // Starter pharma products — all use Gold directly so they work as
        // onboarding factory configurations (purchase → manufacture → sell).
        yield return Product("Aspirin", "aspirin", Industry.Pharmaceuticals, 55m, 3, "A starter pharmaceutical tablet synthesised from refined gold compounds. The entry point for any pharmaceutical manufacturer.", "Bottle", "bottles", 10m, 1.0m, ResourceIngredient("gold", 1m));
        yield return Product("Vitamin Capsule", "vitamin-capsule", Industry.Pharmaceuticals, 80m, 4, "Premium vitamin supplement produced from pure gold compounds. High-margin product for health-conscious markets.", "Pack", "packs", 6m, 1.2m, ResourceIngredient("gold", 1m));
        yield return Product("Antibiotic", "antibiotic", Industry.Pharmaceuticals, 120m, 5, "A broad-spectrum antibiotic formulated from concentrated gold catalyst compounds. Maximum margin in any pharmacy product line.", "Box", "boxes", 4m, 1.5m, ResourceIngredient("gold", 2m));

        yield return Product("Analgesic Syrup", "analgesic-syrup", Industry.Pharmaceuticals, 48m, 3, "Liquid pain-relief formulation for pediatric and elderly markets.", "Bottle", "bottles", 10m, 1.0m, ResourceIngredient("gold", 1m));
        yield return Product("Antiseptic Gel", "antiseptic-gel", Industry.Pharmaceuticals, 40m, 2, "Topical antiseptic gel for wound care and infection prevention.", "Tube", "tubes", 14m, 0.8m, ResourceIngredient("chemical-minerals", 1m));
        yield return Product("Cough Suppressant", "cough-suppressant", Industry.Pharmaceuticals, 35m, 2, "Over-the-counter cough suppressant with broad retail appeal.", "Bottle", "bottles", 15m, 0.7m, ResourceIngredient("chemical-minerals", 1m));
        yield return Product("Eye Drops", "eye-drops", Industry.Pharmaceuticals, 45m, 3, "Sterile ophthalmic solution for ocular relief.", "Bottle", "bottles", 12m, 0.9m, ResourceIngredient("chemical-minerals", 1m));
        yield return Product("Pharmaceutical Capsule", "pharmaceutical-capsule", Industry.Pharmaceuticals, 65m, 4, "Encapsulated active pharmaceutical ingredient for controlled delivery.", "Pack", "packs", 8m, 1.1m, ProductIngredient("aspirin", 2m));
        yield return Product("Medical Cream", "medical-cream", Industry.Pharmaceuticals, 58m, 3, "Topical therapeutic cream combining antiseptic and analgesic properties.", "Tube", "tubes", 8m, 1.0m, ProductIngredient("antiseptic-gel", 2m), ProductIngredient("analgesic-syrup", 1m));
        yield return Product("Vaccine Vial", "vaccine-vial", Industry.Pharmaceuticals, 200m, 5, "Refrigerated biological vaccine in single-dose vial format.", "Vial", "vials", 4m, 1.8m, ProductIngredient("antibiotic", 1m), ResourceIngredient("gold", 1m));
        yield return Product("Insulin Pen", "insulin-pen", Industry.Pharmaceuticals, 150m, 5, "Pre-filled insulin delivery pen for diabetes management.", "Pen", "pens", 4m, 1.7m, ProductIngredient("vitamin-capsule", 2m), ResourceIngredient("gold", 1m));
        yield return Product("Paracetamol Pack", "paracetamol-pack", Industry.Pharmaceuticals, 30m, 2, "Standard paracetamol blister pack for everyday pain management.", "Pack", "packs", 15m, 0.6m, ResourceIngredient("chemical-minerals", 1m));
        yield return Product("Diagnostic Kit", "diagnostic-kit", Industry.Pharmaceuticals, 95m, 4, "At-home diagnostic test kit for rapid medical screening.", "Kit", "kits", 6m, 1.3m, ProductIngredient("pharmaceutical-capsule", 1m), ProductIngredient("antiseptic-gel", 2m));
        yield return Product("Nasal Spray", "nasal-spray", Industry.Pharmaceuticals, 38m, 2, "Saline nasal irrigation spray for allergy and cold relief.", "Bottle", "bottles", 14m, 0.7m, ResourceIngredient("chemical-minerals", 1m));
    }

    private static IEnumerable<ProductSeed> GetEnergyProducts()
    {
        // Starter energy products — all use Coal directly so they work as
        // onboarding factory configurations (purchase → manufacture → sell).
        yield return Product("Coal Briquette", "coal-briquette", Industry.Energy, 28m, 2, "A compressed coal briquette providing consistent heat output for domestic and industrial furnaces. The entry point for any energy producer.", "Bag", "bags", 15m, 0.8m, ResourceIngredient("coal", 2m));
        yield return Product("Heating Oil", "heating-oil", Industry.Energy, 50m, 3, "Refined heating oil distilled from coal for residential and commercial heating systems. Steady demand across all seasons.", "Barrel", "barrels", 8m, 1.1m, ResourceIngredient("coal", 3m));
        yield return Product("Industrial Fuel", "industrial-fuel", Industry.Energy, 75m, 4, "High-density industrial fuel refined from premium coal stocks. Powers factories, generators, and heavy machinery.", "Drum", "drums", 5m, 1.4m, ResourceIngredient("coal", 4m));

        yield return Product("Coke Block", "coke-block", Industry.Energy, 22m, 2, "Processed coke block used in metallurgy and heat-intensive manufacturing.", "Block", "blocks", 18m, 0.7m, ResourceIngredient("coal", 2m));
        yield return Product("Charcoal Pack", "charcoal-pack", Industry.Energy, 18m, 1, "Retail charcoal pack for consumer barbecue and heating use.", "Pack", "packs", 20m, 0.5m, ResourceIngredient("coal", 1m));
        yield return Product("Gas Canister", "gas-canister", Industry.Energy, 40m, 3, "Pressurised gas canister for portable heating and cooking appliances.", "Canister", "canisters", 10m, 1.2m, ResourceIngredient("coal", 2m), ResourceIngredient("iron-ore", 1m));
        yield return Product("Battery Cell", "battery-cell", Industry.Energy, 35m, 3, "Rechargeable battery cell derived from carbon compounds.", "Cell", "cells", 12m, 1.1m, ResourceIngredient("coal", 1m), ResourceIngredient("chemical-minerals", 1m));
        yield return Product("Power Pellet", "power-pellet", Industry.Energy, 32m, 2, "High-energy biomass pellet for power stations and district heating.", "Bag", "bags", 14m, 0.9m, ProductIngredient("coal-briquette", 2m));
        yield return Product("Refined Kerosene", "refined-kerosene", Industry.Energy, 60m, 4, "Aviation-grade kerosene distillate for transport and turbine applications.", "Barrel", "barrels", 6m, 1.3m, ProductIngredient("heating-oil", 1m), ResourceIngredient("coal", 1m));
        yield return Product("Turbine Oil", "turbine-oil", Industry.Energy, 90m, 5, "Precision-grade turbine lubricant for power generation equipment.", "Drum", "drums", 4m, 1.6m, ProductIngredient("industrial-fuel", 1m), ResourceIngredient("chemical-minerals", 1m));
        yield return Product("Fuel Rod", "fuel-rod", Industry.Energy, 130m, 5, "High-density fuel rod for industrial boilers and waste-heat recovery.", "Rod", "rods", 3m, 1.9m, ProductIngredient("industrial-fuel", 1m), ProductIngredient("coke-block", 2m));
        yield return Product("Compressed Gas Bottle", "compressed-gas-bottle", Industry.Energy, 55m, 3, "Compressed industrial gas bottle for welding and cutting operations.", "Bottle", "bottles", 8m, 1.2m, ProductIngredient("gas-canister", 1m), ResourceIngredient("iron-ore", 1m));
        yield return Product("Energy Tablet", "energy-tablet", Industry.Energy, 24m, 2, "High-caffeine energy supplement tablet produced using coal-derived carbon.", "Pack", "packs", 16m, 0.6m, ProductIngredient("charcoal-pack", 1m));
    }

    private static IEnumerable<ProductSeed> GetLogisticsProducts()
    {
        // Starter logistics products — all use Cotton directly so they work as
        // onboarding factory configurations (purchase → manufacture → sell).
        yield return Product("Shipping Bag", "shipping-bag", Industry.Logistics, 20m, 2, "A durable cotton shipping bag for consumer goods distribution. The entry point for any logistics manufacturer.", "Bag", "bags", 18m, 0.6m, ResourceIngredient("cotton", 1m));
        yield return Product("Storage Sack", "storage-sack", Industry.Logistics, 35m, 3, "Reinforced cotton storage sack for bulk commodity warehousing. High-volume demand from agricultural and industrial buyers.", "Sack", "sacks", 10m, 0.9m, ResourceIngredient("cotton", 2m));
        yield return Product("Cargo Pack", "cargo-pack", Industry.Logistics, 55m, 4, "Heavy-duty cotton cargo pack built for international shipping and warehouse handling. Premium packaging for high-value goods.", "Pack", "packs", 6m, 1.2m, ResourceIngredient("cotton", 3m));

        yield return Product("Cotton Wrap", "cotton-wrap", Industry.Logistics, 14m, 1, "Protective cotton wrap material for fragile product packaging.", "Roll", "rolls", 24m, 0.4m, ResourceIngredient("cotton", 1m));
        yield return Product("Padded Envelope", "padded-envelope", Industry.Logistics, 18m, 2, "Cushioned mailing envelope for e-commerce parcels and document dispatch.", "Pack", "packs", 20m, 0.5m, ResourceIngredient("cotton", 1m));
        yield return Product("Tote Bag", "tote-bag", Industry.Logistics, 22m, 2, "Reusable retail tote bag for consumer goods and branded packaging.", "Bag", "bags", 16m, 0.6m, ResourceIngredient("cotton", 1m));
        yield return Product("Fabric Label", "fabric-label", Industry.Logistics, 8m, 1, "Woven cotton label for product identification and branding.", "Pack", "packs", 30m, 0.2m, ResourceIngredient("cotton", 0.5m));
        yield return Product("Insulated Liner", "insulated-liner", Industry.Logistics, 45m, 3, "Thermal insulated cotton liner for cold-chain logistics packaging.", "Piece", "pcs", 8m, 1.0m, ProductIngredient("storage-sack", 1m), ProductIngredient("cotton-wrap", 2m));
        yield return Product("Pallet Cover", "pallet-cover", Industry.Logistics, 38m, 3, "Stretch cotton cover for pallet wrapping and load protection.", "Piece", "pcs", 10m, 0.8m, ProductIngredient("cotton-wrap", 3m), ProductIngredient("shipping-bag", 1m));
        yield return Product("Courier Bag", "courier-bag", Industry.Logistics, 42m, 3, "Tamper-evident courier bag for last-mile delivery services.", "Bag", "bags", 10m, 0.9m, ProductIngredient("shipping-bag", 2m));
        yield return Product("Heavy Duty Sack", "heavy-duty-sack", Industry.Logistics, 65m, 4, "Ultra-reinforced sack for mining, construction, and industrial bulk transport.", "Sack", "sacks", 5m, 1.3m, ProductIngredient("cargo-pack", 1m), ResourceIngredient("cotton", 1m));
        yield return Product("Flat Pack Box", "flat-pack-box", Industry.Logistics, 28m, 2, "Collapsible flat-pack corrugated box for e-commerce and warehouse shipping.", "Box", "boxes", 14m, 0.7m, ResourceIngredient("cotton", 1m), ResourceIngredient("wood", 1m));
    }

    private static ResourceSeed Resource(string name, string slug, string category, decimal basePrice, decimal weightPerUnit, string unitName, string unitSymbol, string description, string icon, string backgroundColor, string accentColor)
        => new(name, slug, category, basePrice, weightPerUnit, unitName, unitSymbol, description, icon, backgroundColor, accentColor);

    private static ProductSeed Product(string name, string slug, string industry, decimal basePrice, int baseCraftTicks, string description, string unitName, string unitSymbol, decimal outputQuantity, decimal energyConsumptionMwh, params RecipeSeed[] ingredients)
        => new(
            name,
            slug,
            industry,
            basePrice,
            baseCraftTicks,
            description,
            unitName,
            unitSymbol,
            outputQuantity,
            energyConsumptionMwh,
            ComputeBasicLaborHours(baseCraftTicks, energyConsumptionMwh, ingredients.Length),
            DeterminePriceElasticity(industry),
            ingredients);

    private static decimal DeterminePriceElasticity(string industry) => industry switch
    {
        Industry.FoodProcessing => 0.75m,
        Industry.Healthcare => 0.20m,
        Industry.Furniture => 0.35m,
        Industry.Electronics => 0.55m,
        Industry.Construction => 0.30m,
        Industry.Pharmaceuticals => 0.22m,
        Industry.Energy => 0.40m,
        Industry.Logistics => 0.50m,
        _ => 0.35m,
    };

    private static decimal ComputeBasicLaborHours(int baseCraftTicks, decimal energyConsumptionMwh, int ingredientCount)
    {
        var labor = (baseCraftTicks * 0.55m) + (energyConsumptionMwh * 0.35m) + (ingredientCount * 0.15m);
        return decimal.Round(Math.Max(0.25m, labor), 4, MidpointRounding.AwayFromZero);
    }

    private static RecipeSeed ResourceIngredient(string resourceSlug, decimal quantity)
        => new(resourceSlug, null, quantity);

    private static RecipeSeed ProductIngredient(string productSlug, decimal quantity)
        => new(null, productSlug, quantity);

    private static string CreateEmojiImageDataUrl(string icon, string backgroundColor, string accentColor)
    {
        var safeBackgroundColor = NormalizeHexColor(backgroundColor);
        var safeAccentColor = NormalizeHexColor(accentColor);
        var svg = $$"""
        <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 160 160'>
          <defs>
            <linearGradient id='g' x1='0' y1='0' x2='1' y2='1'>
              <stop offset='0%' stop-color='{{safeBackgroundColor}}'/>
              <stop offset='100%' stop-color='{{safeAccentColor}}'/>
            </linearGradient>
          </defs>
          <rect width='160' height='160' rx='28' fill='url(#g)'/>
          <circle cx='80' cy='80' r='48' fill='rgba(255,255,255,0.18)'/>
          <text x='80' y='96' text-anchor='middle' font-size='56'>{{icon}}</text>
        </svg>
        """;

        return $"data:image/svg+xml;utf8,{Uri.EscapeDataString(svg)}";
    }

    private static string NormalizeHexColor(string value)
    {
        if (value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit))
        {
            return value;
        }

        return "#4B5563";
    }
}
