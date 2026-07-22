// Data models for the Manufacturing Encyclopedia and Resource Detail
// screens, mirroring `projects/frontend/src/views/ManufacturingEncyclopediaView.vue`
// / `ResourceDetailView.vue`. GraphQL field names verified against
// `Api/Types/Query.Encyclopedia.cs` (`encyclopediaResources` /
// `encyclopediaResourceDetail`).

class EncyclopediaEntry {
  const EncyclopediaEntry({
    required this.id,
    required this.kind,
    required this.name,
    required this.slug,
    required this.category,
    required this.industry,
    required this.description,
    required this.imageUrl,
    required this.isPerishable,
    required this.isProOnly,
    required this.isUnlockedForCurrentPlayer,
    required this.basePrice,
    required this.weightPerUnit,
    required this.baseCraftTicks,
    required this.outputQuantity,
    required this.unitName,
    required this.unitSymbol,
  });

  final String id;

  /// `RESOURCE` or `PRODUCT`.
  final String kind;
  final String name;
  final String slug;
  final String? category;
  final String? industry;
  final String? description;
  final String? imageUrl;
  final bool isPerishable;
  final bool isProOnly;
  final bool isUnlockedForCurrentPlayer;
  final double basePrice;
  final double? weightPerUnit;
  final int? baseCraftTicks;
  final double? outputQuantity;
  final String? unitName;
  final String? unitSymbol;

  factory EncyclopediaEntry.fromJson(Map<String, dynamic> json) => EncyclopediaEntry(
    id: json['id'] as String,
    kind: (json['kind'] as String?) ?? 'RESOURCE',
    name: (json['name'] as String?) ?? '',
    slug: (json['slug'] as String?) ?? '',
    category: json['category'] as String?,
    industry: json['industry'] as String?,
    description: json['description'] as String?,
    imageUrl: json['imageUrl'] as String?,
    isPerishable: json['isPerishable'] as bool? ?? false,
    isProOnly: json['isProOnly'] as bool? ?? false,
    isUnlockedForCurrentPlayer: json['isUnlockedForCurrentPlayer'] as bool? ?? true,
    basePrice: (json['basePrice'] as num?)?.toDouble() ?? 0,
    weightPerUnit: (json['weightPerUnit'] as num?)?.toDouble(),
    baseCraftTicks: (json['baseCraftTicks'] as num?)?.toInt(),
    outputQuantity: (json['outputQuantity'] as num?)?.toDouble(),
    unitName: json['unitName'] as String?,
    unitSymbol: json['unitSymbol'] as String?,
  );
}

class EncyclopediaResourcesPage {
  const EncyclopediaResourcesPage({required this.page, required this.totalPages, required this.items});

  final int page;
  final int totalPages;
  final List<EncyclopediaEntry> items;

  factory EncyclopediaResourcesPage.fromJson(Map<String, dynamic> json) => EncyclopediaResourcesPage(
    page: (json['page'] as num?)?.toInt() ?? 1,
    totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
    items: ((json['items'] as List<dynamic>?) ?? const [])
        .map((e) => EncyclopediaEntry.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class RecipeInput {
  const RecipeInput({
    required this.kind,
    required this.name,
    required this.slug,
    required this.quantity,
    required this.unitSymbol,
    required this.isProOnly,
  });

  final String kind;
  final String name;
  final String slug;
  final double quantity;
  final String? unitSymbol;
  final bool isProOnly;

  factory RecipeInput.fromJson(Map<String, dynamic> json) => RecipeInput(
    kind: (json['kind'] as String?) ?? 'RESOURCE',
    name: (json['name'] as String?) ?? '',
    slug: (json['slug'] as String?) ?? '',
    quantity: (json['quantity'] as num?)?.toDouble() ?? 0,
    unitSymbol: json['unitSymbol'] as String?,
    isProOnly: json['isProOnly'] as bool? ?? false,
  );
}

class EncyclopediaRecipe {
  const EncyclopediaRecipe({
    required this.id,
    required this.recipeName,
    required this.buildingType,
    required this.output,
    required this.inputs,
  });

  final String id;
  final String recipeName;
  final String buildingType;
  final EncyclopediaEntry output;
  final List<RecipeInput> inputs;

  factory EncyclopediaRecipe.fromJson(Map<String, dynamic> json) => EncyclopediaRecipe(
    id: json['id'] as String,
    recipeName: (json['recipeName'] as String?) ?? '',
    buildingType: (json['buildingType'] as String?) ?? '',
    output: EncyclopediaEntry.fromJson((json['output'] as Map<String, dynamic>?) ?? const {}),
    inputs: ((json['inputs'] as List<dynamic>?) ?? const [])
        .map((e) => RecipeInput.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class EncyclopediaResourceDetail {
  const EncyclopediaResourceDetail({required this.entry, required this.producedByRecipes, required this.usedInRecipes});

  final EncyclopediaEntry entry;
  final List<EncyclopediaRecipe> producedByRecipes;
  final List<EncyclopediaRecipe> usedInRecipes;

  factory EncyclopediaResourceDetail.fromJson(Map<String, dynamic> json) => EncyclopediaResourceDetail(
    entry: EncyclopediaEntry.fromJson(json['entry'] as Map<String, dynamic>),
    producedByRecipes: ((json['producedByRecipes'] as List<dynamic>?) ?? const [])
        .map((e) => EncyclopediaRecipe.fromJson(e as Map<String, dynamic>))
        .toList(),
    usedInRecipes: ((json['usedInRecipes'] as List<dynamic>?) ?? const [])
        .map((e) => EncyclopediaRecipe.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
