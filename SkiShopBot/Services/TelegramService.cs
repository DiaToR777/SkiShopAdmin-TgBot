using SkiShopBot.Enums;
using SkiShopBot.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SkiShopBot.Services
{
    public class TelegramService
    {
        private readonly Dictionary<long, Session> _sessions = new();
        private readonly long _adminId;
        private readonly string _botToken;

        private readonly ITelegramBotClient _bot;

        private readonly ImageHostService _imageService;
        private readonly MongoDbService _dbService;


        public TelegramService(long adminId, string botToken, ImageHostService imageService, MongoDbService dbService)
        {
            _botToken = botToken;
            _adminId = adminId;

            _imageService = imageService;
            _dbService = dbService;

            _bot = new TelegramBotClient(botToken);
        }

        public async Task Start()
        {
            var me = await _bot.GetMe();
            Console.WriteLine($"✓ Бот @{me.Username} запущено");

            using var cts = new CancellationTokenSource();

            _bot.StartReceiving(
                updateHandler: HandleUpdate,
                HandleError,
                receiverOptions: new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
                cancellationToken: cts.Token
            );

            await Task.Delay(-1);
        }

        private async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message) return;
            var chatId = message.Chat.Id;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message.Chat.FirstName}: {message.Text ?? "[Media]"}");

            if (chatId != _adminId)
            {
                await bot.SendMessage(chatId, "Вибачте, цей бот тільки для адміністратора.");
                return;
            }

            if (!_sessions.ContainsKey(chatId)) _sessions[chatId] = new Session();
            var session = _sessions[chatId];

            if (message.Text == "/start" || message.Text == "/help")
            {
                session.CurrentStep = Step.Idle; 
                await bot.SendMessage(chatId, "⛷ Вас вітає SkiShopAdmin!\n\n" +
                                              "Команди:\n" +
                                              "/add - Додати новий товар\n" +
                                              "/all - Показати всі товари\n" +
                                              "/cancel - Скасувати поточну дію",
                                              replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            if (message.Text == "/cancel")
            {
                _sessions.Remove(chatId);
                await bot.SendMessage(chatId, "❌ Дію скасовано.", replyMarkup: new ReplyKeyboardRemove());
                return;
            }
            if (message.Text == "/all")
            {
                var products = await _dbService.GetAllProductsAsync();
                foreach (var p in products)
                {
                    var source = p.PhotosUrls.Select(uri => InputFile.FromUri(uri.ToString()));
                    await SendProductPreviewWithMedia(chatId, bot, p, source);
                }
            }
            switch (session.CurrentStep)
            {

                case Step.Idle:
                    if (message.Text == "/add")
                    {
                        await HandleAddCommand(chatId, bot, session);
                    }
                    break;
                case Step.WaitingForCategory:
                    await HandleGetCategoryStep(chatId, message, session, bot);
                    break;

                case Step.WaitingForPhoto:
                    await HandleGetPhotoStep(chatId, message, session, bot);
                    break;

                case Step.WaitingForName:
                    await HandleNameStep(chatId, message, session, bot);
                    break;

                case Step.WaitingForSize:
                    await HandleSizeStep(chatId, message, session, bot);
                    break;

                case Step.WaitingForDescription:
                    await HandleDescriptionStep(chatId, message, session, bot);
                    break;

                case Step.WaitingForPrice:
                    await HandlePriceStep(chatId, message, session, bot);
                    break;
                case Step.Confirm:
                    await HandleConfirmStep(chatId, message, session, bot);
                    break;
            }

        }
        private void ResetForNewProduct(Session session)
        {
            session.Product = new Product(); 
            session.TempFileIds.Clear();

            session.CurrentStep = Step.WaitingForCategory;
        }
        private async Task HandleAddCommand(long chatId, ITelegramBotClient bot, Session session)
        {
            ResetForNewProduct(session);

            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new[] { new KeyboardButton("⛷ Лижі"), new KeyboardButton("🥾 Черевики") }
                               })
            { ResizeKeyboard = true, OneTimeKeyboard = true };
            await bot.SendMessage(chatId, "Вибери категорію товару:", replyMarkup: keyboard);
        }
        private async Task HandleGetCategoryStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if (message.Text == "⛷ Лижі") session.Product.Category = ProductCategory.Skis;
            else if (message.Text == "🥾 Черевики") session.Product.Category = ProductCategory.Boots;

            var stopKeyboard = new ReplyKeyboardMarkup(new[]
            {
            new[] { new KeyboardButton("🛑 СТОП (фото завантажені)") }
                                   })
            { ResizeKeyboard = true };

            await bot.SendMessage(chatId,
                $"Обрано: {message.Text}. Тепер скидай фото.\nКоли закінчиш — натисни кнопку нижче 👇",
                replyMarkup: stopKeyboard);

            session.CurrentStep = Step.WaitingForPhoto;
        }

        private async Task HandleGetPhotoStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if (message.Type == MessageType.Photo)
            {
                var fileId = message.Photo!.Last().FileId;
                session.TempFileIds.Add(fileId);

                await bot.SendMessage(chatId, $"📸 Фото №{session.TempFileIds.Count} додано! Скидай ще або напиши 'стоп'.");
            }
            else if (message.Text != null && (message.Text.Contains("СТОП") || message.Text.ToLower() == "стоп"))
            {
                if (session.TempFileIds.Count > 0)
                {
                    await bot.SendMessage(chatId,
                                    "✅ Фото прийняті. Тепер напиши назву (бренд та модель):",
                                    replyMarkup: new ReplyKeyboardRemove());
                    session.CurrentStep = Step.WaitingForName;
                }
                else
                {
                    await bot.SendMessage(chatId, "Потрібно хоча б одне фото!");
                }
            }

        }
        private async Task HandleNameStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if (!IsValidDescription(message, out string? error))
            {
                await bot.SendMessage(chatId, error!);
                return;
            }
            session.Product.Name = message.Text!;

            string sizePrompt = session.Product.Category == ProductCategory.Skis
                ? "📏 Яка довжина лиж у см?"
                : "📏 Який розмір черевиків (EU)?";

            await bot.SendMessage(chatId, sizePrompt);
            session.CurrentStep = Step.WaitingForSize;
        }
        private async Task HandleSizeStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if (decimal.TryParse(message.Text, out decimal size))
            {
                session.Product.Size = size;
                await bot.SendMessage(chatId, "📝 Додай короткий опис (стан, дефекти, кріплення):");
                session.CurrentStep = Step.WaitingForDescription;
            }
            else
            {
                await bot.SendMessage(chatId, "Будь ласка, введи коректну ціну цифрами.");
            }
        }
        private async Task HandleConfirmStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if (message.Text == "✅ Так" )
            {
                var uploadTasks = session.TempFileIds.Select(async fileId =>
                {
                    try
                    {
                        var file = await bot.GetFile(fileId);
                        var downloadUrl = $"https://api.telegram.org/file/bot{_botToken}/{file.FilePath}";
                        return await _imageService.UploadImageAsync(new Uri(downloadUrl));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Помилка завантаження фото {fileId}: {ex.Message}");
                        return null; 
                    }
                });
                var results = await Task.WhenAll(uploadTasks);

                var successfulUrls = results.Where(url => url != null).Cast<Uri>().ToList();

                if (successfulUrls.Any()) 
                {
                    
                    session.Product.PhotosUrls.AddRange(successfulUrls);
                    await _dbService.AddProductAsync(session.Product);

                    string statusMessage = successfulUrls.Count == session.TempFileIds.Count
                        ? "🎉 Товар успішно додано до каталогу!"
                        : $"⚠️ Товар додано, але завантажено лише {successfulUrls.Count} з {session.TempFileIds.Count} фото.";

                    await bot.SendMessage(chatId, statusMessage, replyMarkup: new ReplyKeyboardRemove());
                }
                else
                {
                    await bot.SendMessage(chatId, "❌ Не вдалося завантажити жодного фото. Спробуйте ще раз.", replyMarkup: new ReplyKeyboardRemove());
                }

                session.TempFileIds.Clear();
                session.CurrentStep = Step.Idle;
            }
            else
            {
                await bot.SendMessage(chatId, "Скасовано. Спробуй ще раз через /add", replyMarkup: new ReplyKeyboardRemove());
                session.CurrentStep = Step.Idle;
            }
        }

        private async Task HandleDescriptionStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if(!IsValidDescription(message, out string? error))
            {
                await bot.SendMessage(chatId, error!);
                return;
            }
            session.Product.Description = message.Text!;
            await bot.SendMessage(chatId, "💰 Вкажи ціну (тільки цифри, в грн):");
            session.CurrentStep = Step.WaitingForPrice;

        }
        private async Task HandlePriceStep(long chatId, Message message, Session session, ITelegramBotClient bot)
        {
            if (decimal.TryParse(message.Text, out decimal price))
            {
                session.Product.Price = price;
                var sources = session.TempFileIds.Select(id => InputFile.FromFileId(id));
                await SendProductPreviewWithMedia(chatId, bot, session.Product, sources);

                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                new[] { new KeyboardButton("✅ Так"), new KeyboardButton("❌ Ні, заново") }
            })
                { ResizeKeyboard = true, OneTimeKeyboard = true };

                await bot.SendMessage(chatId, "Все правильно?", replyMarkup: keyboard);
                session.CurrentStep = Step.Confirm;
            }
            else
            {
                await bot.SendMessage(chatId, "Будь ласка, введи коректну ціну цифрами.");
            }
        }

        public async Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            Console.WriteLine($"❌ Помилка бота: {ex.Message}");
            try
            {
                await bot.SendMessage(_adminId, $"⛔️ Помилка полінгу: {ex.Message}");
            }
            catch { /* ignored */ }
        }
        private async Task SendProductPreviewWithMedia(long chatId, ITelegramBotClient bot, Product product, IEnumerable<InputFile> mediaSources)
        {
            var sources = mediaSources.ToList();
            string summary = product.ToString();

            if (!sources.Any())
            {
                await bot.SendMessage(chatId, summary, parseMode: ParseMode.Markdown);
                return;
            }

            if (sources.Count == 1)
            {
                await bot.SendPhoto(chatId, sources.First(), caption: summary, parseMode: ParseMode.Markdown);
            }
            else
            {
                var album = sources.Take(10).Select((file, index) =>
                    index == 0
                        ? (IAlbumInputMedia)new InputMediaPhoto(file) { Caption = summary, ParseMode = ParseMode.Markdown }
                        : new InputMediaPhoto(file)
                ).ToArray();

                await bot.SendMediaGroup(chatId, album);
            }
        }
        private bool IsValidDescription(Message message, out string? error)
        {
            error = null;

            if (message?.Text == null || string.IsNullOrWhiteSpace(message.Text))
            {
                error = "Текст повинен бути в текстовому форматі.";
                return false;
            }

            var text = message.Text.Trim();

            if (text.Length < 10)
            {
                error = "Опис занадто короткий. Напиши хоча б 10 символів.";
                return false;
            }

            if (text.Length > 2000)
            {
                error = $"Опис занадто довгий ({text.Length} символів). Скороти до 2000.";
                return false;
            }

            // 3. Найнебезпечніші невидимі символи (RTL тролінг, zero-width тощо)
            var dangerousChars = new[] { '\u202E', '\u200B', '\u200C', '\u200D', '\uFEFF' };
            if (text.Any(c => dangerousChars.Contains(c)))
            {
                error = "Виявлено заборонені невидимі символи. Напиши текст без фокусів.";
                return false;
            }           
            return true;
        }

    }
}