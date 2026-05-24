namespace AniNest.Host.Modules;

internal interface IMetadataRuntimeBootstrapService
{
    void EnsureInitialized();
    void NormalizeTransientStates();
}
