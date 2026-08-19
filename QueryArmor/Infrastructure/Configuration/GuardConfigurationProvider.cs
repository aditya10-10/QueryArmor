using QueryArmor.Application.Configuration;

namespace QueryArmor.Infrastructure.Configuration
{
    public sealed class GuardConfigurationProvider : IGuardConfigurationProvider
    {
        public GuardConfiguration Load(string? path = null)
            => GuardConfiguration.Load(path);
    }
}
