using Reqnroll;

namespace ApexPost.Specs.Support;

/// <summary>
/// Hooks run around every scenario. The factory is created BEFORE and disposed AFTER each scenario,
/// even when the scenario fails, so no test database is left behind and no scenario can see another one's data.
/// </summary>
[Binding]
public sealed class Hooks(ApiContext api)
{
    [BeforeScenario]
    public void StartApi() => api.Start();

    [AfterScenario]
    public void StopApi() => api.Stop();
}
