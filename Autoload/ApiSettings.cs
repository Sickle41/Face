using Godot;
using WarmachineClient.Api;

namespace WarmachineClient.Autoload;

public partial class ApiSettings : Node
{
    private const string ConfigPath = "user://settings.cfg";
    private const string SectionApi = "api";

    public string BaseUrl { get; private set; } = "http://localhost:5000";
    public string ApiKey { get; private set; } = string.Empty;

    public WarmachineApiClient Client { get; private set; } = null!;

    public override void _Ready()
    {
        LoadFromDisk();
        RebuildClient();
    }

    public void LoadFromDisk()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            BaseUrl = config.GetValue(SectionApi, "base_url", BaseUrl).AsString();
            ApiKey = config.GetValue(SectionApi, "api_key", ApiKey).AsString();
        }
    }

    public void Save(string baseUrl, string apiKey)
    {
        BaseUrl = baseUrl;
        ApiKey = apiKey;

        var config = new ConfigFile();
        config.SetValue(SectionApi, "base_url", BaseUrl);
        config.SetValue(SectionApi, "api_key", ApiKey);
        config.Save(ConfigPath);

        RebuildClient();
    }

    private void RebuildClient()
    {
        Client = new WarmachineApiClient(BaseUrl, ApiKey);
    }
}
