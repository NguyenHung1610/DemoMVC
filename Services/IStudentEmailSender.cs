namespace DemoMVC.Services;

public interface IStudentEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody);
}
