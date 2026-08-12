using DigiPOSE.Models;

namespace DigiPOSE.Helpers
{
    public interface IMailLogic
    {
        Task SendEmailAsync(MailInfo mailInfo);
        Task SendOrderSuccessEmailAsync(Order order, MailInfo mailInfo, Retail? retail = null);
    }
}