using CloudinaryDotNet;
using SkiShopBot.Services;

string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN") ?? "YOUR_TOKEN";
string adminId = Environment.GetEnvironmentVariable("ADMIN_ID") ?? "YOUR_ADMIN_ID";

string mongoUri = Environment.GetEnvironmentVariable("MONGO_URI") ?? "YOUR_CONNECTION_STRING";

var cloudinaryAcc = new Account(
    "cloud_name",
    "api_key",
    "api_secret"
);
var imageService = new ImageHostService(cloudinaryAcc);
var mongoService = new MongoDbService(mongoUri, "SkiShop");
var telegramService = new TelegramService(long.Parse(adminId) ,botToken , imageService, mongoService); 

await telegramService.Start();