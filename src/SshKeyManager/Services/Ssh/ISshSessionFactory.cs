namespace SshKeyManager.Services.Ssh;

public interface ISshSessionFactory
{
    ISshSessionScope CreateScope();
}
