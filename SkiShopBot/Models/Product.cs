using SkiShopBot.Models.Convertors;

namespace SkiShopBot.Models
{
    public class Product
    {
        public MongoDB.Bson.ObjectId Id { get; set; }
        public string Name { get; set; }
        public ProductCategory Category { get; set; }
        public decimal Size { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public List<Uri> PhotosUrls { get; set; } = new List<Uri>();

        public override string ToString()
        {

            return $"📦 *Категорія:* {ProductCategoryExtensions.ToFriendlyName(Category)}\n" +
                   $"🏷 *Назва:* {Name}\n" +
                   $"📏 *Розмір:* {Size}\n" +
                   $"📄 *Опис:* {Description}\n" +
                   $"💰 *Ціна:* {Price} грн\n";
        }
    }
}
