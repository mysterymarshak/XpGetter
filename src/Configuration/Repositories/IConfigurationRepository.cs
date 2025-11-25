using XpGetter.Configuration.Entities;

namespace XpGetter.Configuration.Repositories;

public interface IConfigurationRepository
{
    AppConfiguration Get();
    void Export(AppConfiguration configuration);
}