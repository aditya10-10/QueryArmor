
namespace QueryArmor.Application.Configuration
{
    public interface IGuardConfigurationProvider
    {
        GuardConfiguration Load(string? path = null);
    }
}
