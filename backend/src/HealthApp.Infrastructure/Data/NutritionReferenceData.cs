using HealthApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthApp.Infrastructure.Data;

/// <summary>
/// Reference nutrition values per 100 g for the built-in ingredient catalog.
/// Values are generic food references, not a substitute for supplier labels or
/// recipe-specific cooked/raw states. The database stores the values and source
/// metadata so an authorized catalog workflow can replace them with verified data.
/// </summary>
internal static class NutritionReferenceData
{
    private sealed record Reference(decimal Calories, decimal Protein, decimal Carbs, decimal Fat,
        decimal Fiber, decimal Sugar, string Source, string ReferenceId);

    private static Reference R(double kcal, double protein, double carbs, double fat,
        double fiber = 0, double sugar = 0) =>
        new((decimal)kcal, (decimal)protein, (decimal)carbs, (decimal)fat, (decimal)fiber, (decimal)sugar,
            "USDA FoodData Central generic food reference; verify product, brand and cooked/raw state",
            "Generic reference; verify before production use");

    private static readonly IReadOnlyDictionary<string, Reference> Values =
        new Dictionary<string, Reference>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chicken Breast"] = new(151m,30.5m,0m,3.2m,0m,0m,"USDA FoodData Central · cooked grilled, skinless boneless","FDC 171534"),
            ["Chicken Breast (Grilled)"] = new(151m,30.5m,0m,3.2m,0m,0m,"USDA FoodData Central · cooked grilled, skinless boneless","FDC 171534"),
            ["Basmati Rice"] = new(130m,2.7m,28.2m,.28m,.4m,.05m,"USDA FoodData Central · cooked white rice reference","FDC 168878"),
            ["Basmati Rice (Boiled)"] = new(130m,2.7m,28.2m,.28m,.4m,.05m,"USDA FoodData Central · cooked white rice reference","FDC 168878"),
            ["Brown Rice"] = new(123m,2.74m,25.58m,.97m,1.6m,0m,"USDA FoodData Central · cooked brown rice","FDC 169704"),
            ["Broccoli"] = new(35m,2.38m,7.18m,.41m,3.3m,1.39m,"USDA FoodData Central · cooked broccoli","FDC 169967"),
            ["Chickpeas"] = new(164m,8.86m,27.42m,2.59m,7.6m,4.8m,"USDA FoodData Central · cooked chickpeas","FDC 173757"),
            ["Lentils"] = new(116m,9.02m,20.13m,.38m,7.9m,1.8m,"USDA FoodData Central · cooked lentils","FDC 172421"),
            ["Olive Oil"] = new(884m,0m,0m,100m,0m,0m,"USDA FoodData Central · olive oil","FDC 171413"),
            ["Tahini"] = new(595m,17m,21.19m,53.76m,9.3m,.5m,"USDA FoodData Central · tahini","FDC 170189"),
            ["Prawns"] = new(99m,23.98m,.2m,.28m,0m,0m,"USDA FoodData Central · cooked shrimp","FDC 175180"),
            ["Paneer"] = R(265,18.3,1.2,20.8,0,1.2),
            ["Wheat Noodles"] = R(138,4.5,25,2.1,1.2,0.6),
            ["Mutton"] = R(294,25,0,21,0,0),
            ["White Rice"] = R(130,2.7,28.2,.3,.4,.1),
            ["Sona Masoori Rice"] = R(130,2.7,28.2,.3,.4,.1),
            ["Chicken Thigh"] = R(209,26,0,10.9,0,0),
            ["Whole Wheat Flour"] = R(340,13.2,72,2.5,10.7,.4),
            ["Toor Dal"] = R(343,22.3,62.8,1.5,15,2),
            ["Black Lentils"] = R(341,25,59,1.6,18,2),
            ["Kidney Beans"] = R(127,8.7,22.8,.5,6.4,.3),
            ["Idli Batter"] = R(125,4,25,1,2,.5),
            ["Dosa Batter"] = R(120,3.5,23,1.5,1.5,.5),
            ["Potato"] = R(87,1.9,20.1,.1,1.8,.9),
            ["Tomato"] = R(18,.9,3.9,.2,1.2,2.6),
            ["Onion"] = R(40,1.1,9.3,.1,1.7,4.2),
            ["Carrot"] = R(41,.9,9.6,.2,2.8,4.7),
            ["Mixed Vegetables"] = R(65,3,12,.5,4,4),
            ["Bell Pepper"] = R(31,1,6,.3,2.1,4.2),
            ["Ginger Garlic Paste"] = R(80,3,17,.5,2,3),
            ["Green Chilies"] = R(40,2,9,.4,1.5,5),
            ["Curry Leaves"] = R(108,6.1,18.7,1,6.4,0),
            ["Coriander"] = R(23,2.1,3.7,.5,2.8,.9),
            ["Tamarind"] = R(239,2.8,62.5,.6,5.1,38.8),
            ["Coconut"] = R(354,3.3,15.2,33.5,9,6.2),
            ["Mustard Seeds"] = R(508,26.1,28.1,36.2,12.2,6.8),
            ["Red Chili Powder"] = R(282,13.5,50,14,34,7.2),
            ["Garam Masala"] = R(350,12,55,13,25,3),
            ["Sesame Oil"] = R(884,0,0,100,0,0),
            ["Yogurt"] = R(61,3.5,4.7,3.3,0,4.7),
            ["Ghee"] = R(900,0,0,100,0,0),
            ["Butter"] = R(717,.9,.1,81.1,0,.1),
            ["Cream"] = R(340,2.1,2.8,36,0,2.9),
            ["Cashews"] = R(553,18.2,30.2,43.9,3.3,5.9),
            ["Peanuts"] = R(567,25.8,16.1,49.2,8.5,4.7),
            ["Fish Fillet"] = R(120,22,0,3,0,0),
            ["Saffron"] = R(310,11.4,65.4,5.9,3.9,0),
            ["Biryani Masala"] = R(300,10,50,10,20,5),
            ["Mint"] = R(44,3.3,8.4,.7,6.8,0),
            ["Wheat"] = R(339,13.7,71.1,2.5,12.2,.4),
            ["Corn Flour"] = R(381,.3,91.3,.1,.9,.3),
            ["Sesame Seeds"] = R(573,17.7,23.4,49.7,11.8,.3),
            ["Bread"] = R(265,9,49,3.2,2.7,5),
            ["Milk"] = R(61,3.2,4.8,3.3,0,5.1),
            ["Sugar"] = R(387,0,100,0,0,100),
            ["Cabbage"] = R(25,1.3,5.8,.1,2.5,3.2),
            ["Spring Onion"] = R(32,1.8,7.3,.2,2.6,2.3),
            ["Soy Sauce"] = R(53,8.1,4.9,.6,.8,.4),
            ["Vinegar"] = R(18,0,.04,0,0,0),
            ["Schezwan Sauce"] = R(150,2,18,8,2,8),
            ["Chili Sauce"] = R(100,1,23,.5,1,15),
            ["Eggs"] = R(143,12.6,.7,9.5,0,.4),
            ["Egg Whites"] = R(52,10.9,.7,.2,0,.7),
            ["Tofu"] = R(76,8.1,1.9,4.8,.3,.6),
            ["Sweet Potato"] = R(86,1.6,20.1,.1,3,4.2),
            ["Spinach"] = R(23,2.9,3.6,.4,2.2,.4),
            ["Cauliflower"] = R(25,1.9,5,.3,2,1.9),
            ["Zucchini"] = R(17,1.2,3.1,.3,1,.3),
            ["Mushrooms"] = R(22,3.1,3.3,.3,1,2),
            ["Green Peas"] = R(81,5.4,14.5,.4,5.7,5.7),
            ["Corn"] = R(86,3.3,18.7,1.4,2,6.3),
            ["Black Beans"] = R(132,8.9,23.7,.5,8.7,.3),
            ["Quinoa"] = R(120,4.4,21.3,1.9,2.8,.9),
            ["Oats"] = R(389,16.9,66.3,6.9,10.6,.9),
            ["Banana"] = R(89,1.1,22.8,.3,2.6,12.2),
            ["Apple"] = R(52,.3,13.8,.2,2.4,10.4),
            ["Orange"] = R(47,.9,11.8,.1,2.4,9.4),
            ["Lemon"] = R(29,1.1,9.3,.3,2.8,2.5),
            ["Avocado"] = R(160,2,8.5,14.7,6.7,.7),
            ["Almonds"] = R(579,21.2,21.6,49.9,12.5,4.4),
            ["Walnuts"] = R(654,15.2,13.7,65.2,6.7,2.6),
            ["Greek Yogurt"] = R(97,9,3.9,5,0,3.2),
            ["Cheddar Cheese"] = R(403,24.9,1.3,33.1,0,.5),
            ["Mozzarella"] = R(280,27.5,3.1,17.1,0,1),
            ["Chickpea Flour"] = R(387,22.4,57.8,6.7,10.8,10.9),
            ["Rice Flour"] = R(366,6,80.1,1.4,2.4,.1),
            ["Ragi Flour"] = R(336,7.3,72,1.3,11.5,0),
            ["Jowar Flour"] = R(349,10.4,72.6,3.5,6.7,2.5),
            ["Bajra Flour"] = R(361,11,67,5,11,2),
            ["Coconut Milk"] = R(230,2.3,5.5,23.8,2.2,3.3),
            ["Tomato Puree"] = R(38,1.6,8.2,.2,1.5,5.3),
            ["Garlic"] = R(149,6.4,33.1,.5,2.1,1),
            ["Turmeric"] = R(312,9.7,67.1,3.3,22.7,3.2),
            ["Cumin Seeds"] = R(375,17.8,44.2,22.3,10.5,2.3),
            ["Coriander Powder"] = R(298,12.4,54.99,17.8,41.9,.9),
            ["Black Pepper"] = R(251,10.4,64,3.3,25.3,0.6),
            ["Salt"] = R(0,0,0,0,0,0),
            ["Honey"] = R(304,.3,82.4,0,0,82.1),
            ["Dates"] = R(282,2.5,75,0.4,8,63),
            ["Raisins"] = R(299,3.1,79.2,.5,3.7,59.2),
            ["Flax Seeds"] = R(534,18.3,28.9,42.2,27.3,1.6),
            ["Chia Seeds"] = R(486,16.5,42.1,30.7,34.4,0),
            ["Pumpkin Seeds"] = R(559,30.2,10.7,49.1,6,1.4),
            ["Sunflower Seeds"] = R(584,20.8,20,51.5,8.6,2.6),
            ["Peas"] = R(81,5.4,14.5,.4,5.7,5.7),
            ["Beetroot"] = R(43,1.6,9.6,.2,2.8,6.8),
            ["Cucumber"] = R(15,.7,3.6,.1,.5,1.7),
            ["Lettuce"] = R(15,1.4,2.9,.2,1.3,.8),
            ["Green Beans"] = R(31,1.8,7,.2,2.7,3.3),
            ["Asparagus"] = R(20,2.2,3.9,.1,2.1,1.9),
        };

    public static async Task ApplyAsync(HealthAppDbContext db, CancellationToken cancellationToken)
    {
        var ingredients = await db.Ingredients.ToListAsync(cancellationToken);
        foreach (var ingredient in ingredients)
        {
            if (!Values.TryGetValue(ingredient.Name, out var reference))
            {
                // Unknown/custom ingredients must be reviewed rather than silently assigned
                // misleading zero nutrition values.
                continue;
            }

            ingredient.CaloriesPer100g = reference.Calories;
            ingredient.ProteinGramsPer100g = reference.Protein;
            ingredient.CarbsGramsPer100g = reference.Carbs;
            ingredient.FatGramsPer100g = reference.Fat;
            ingredient.FiberGramsPer100g = reference.Fiber;
            ingredient.SugarGramsPer100g = reference.Sugar;
            ingredient.NutritionSource = reference.Source;
            ingredient.NutritionReferenceId = reference.ReferenceId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
