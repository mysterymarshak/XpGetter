using XpGetter.Application.Features.Configuration.Entities;

namespace XpGetter.Application.Features.Configuration.Repositories;

public interface IConfigurationRepository
{
    AppConfiguration Get();
    void Export(AppConfiguration configuration);
}