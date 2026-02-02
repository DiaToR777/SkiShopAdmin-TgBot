using SkiShopBot.Models;

namespace SkiShopBot.Enums;
public class Session
{
    public Step CurrentStep { get; set; } = Step.Idle;
    public Product Product { get; set; }
    public List<string> TempFileIds { get; set;  } = new();
}
