namespace SkiShopBot.Models.Convertors
{
    public static class ProductCategoryExtensions
    {
        public static string ToFriendlyName(this ProductCategory category)
        {
            return category switch
            {
                ProductCategory.Skis => "⛷ Лижі",
                ProductCategory.Boots => "🥾 Черевики",
                _ => "❓ Невідомо"
            };
        }
    }
}
