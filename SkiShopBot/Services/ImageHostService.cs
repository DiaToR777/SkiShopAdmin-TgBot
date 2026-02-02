using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SkiShopBot.Services
{
    public class ImageHostService
    {
        private Account account;
        private Cloudinary cloudinary;
        public ImageHostService(Account account)
        {
             cloudinary = new Cloudinary(account);
        }

        public async Task<Uri> UploadImageAsync(Uri imgUrl)
        {

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(imgUrl.ToString()), 
                Folder = "SkiShop/ManualUpload"
            };

            var uploadResult = await cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine("✅ Фото успішно завантажено!");
                Console.WriteLine("🔗 Посилання: " + uploadResult.SecureUrl);

            }
            else
            {
                Console.WriteLine("❌ Помилка: " + uploadResult.Error.Message);
            }


            return uploadResult.SecureUrl;
        }
    }
}
