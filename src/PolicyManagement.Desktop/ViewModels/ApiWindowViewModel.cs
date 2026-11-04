using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Media;
using PolicyManagement.Contracts;

namespace PolicyManagement.Desktop.ViewModels;

public sealed class ApiWindowViewModel : ObservableObject, IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly AsyncCommand _loadDetailsCommand;
    private string _apiBaseUrl = "http://localhost:5101/";
    private string _webSocketUrl = "ws://localhost:5101/ws/policies";
    private Guid _tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private PolicyDto? _selectedPolicy;
    private bool _enabledOnly;
    private string _statusText = "Ready.";
    private Brush _statusBrush = Brushes.SlateGray;
    private string _detailsText = "Select a policy, then load its details.";
    private int _tenantVersion;

    public ApiWindowViewModel()
    {
        Tenants =
        [
            new TenantOption(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Bank A"),
            new TenantOption(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Bank B")
        ];
        RefreshCommand = new AsyncCommand(RefreshAsync);
        _loadDetailsCommand = new AsyncCommand(LoadDetailsAsync, () => SelectedPolicy is not null);
    }

    public ObservableCollection<PolicyDto> Policies { get; } = [];
    public IReadOnlyList<TenantOption> Tenants { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand LoadDetailsCommand => _loadDetailsCommand;

    public string ApiBaseUrl
    {
        get => _apiBaseUrl;
        set => SetProperty(ref _apiBaseUrl, value);
    }

    public string WebSocketUrl
    {
        get => _webSocketUrl;
        set => SetProperty(ref _webSocketUrl, value);
    }

    public Guid TenantId
    {
        get => _tenantId;
        set
        {
            if (SetProperty(ref _tenantId, value))
            {
                _tenantVersion++;
                Policies.Clear();
                SelectedPolicy = null;
                DetailsText = "Tenant changed. Refresh the list.";
                OnPropertyChanged(nameof(CurrentTenantName));
            }
        }
    }

    public string CurrentTenantName => Tenants.FirstOrDefault(x => x.Id == TenantId)?.Name ?? "Custom tenant";

    public PolicyDto? SelectedPolicy
    {
        get => _selectedPolicy;
        set
        {
            if (SetProperty(ref _selectedPolicy, value))
            {
                _loadDetailsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool EnabledOnly
    {
        get => _enabledOnly;
        set => SetProperty(ref _enabledOnly, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public string DetailsText
    {
        get => _detailsText;
        private set => SetProperty(ref _detailsText, value);
    }

    private async Task RefreshAsync()
    {
        var tenantVersion = _tenantVersion;
        SetStatus("Loading policies…", false);
        try
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                $"api/policies?enabledOnly={EnabledOnly.ToString().ToLowerInvariant()}");
            using var response = await _httpClient.SendAsync(request);
            if (tenantVersion != _tenantVersion) return;
            if (!response.IsSuccessStatusCode)
            {
                SetStatus(await ReadErrorAsync(response), true);
                return;
            }

            var policies = await response.Content.ReadFromJsonAsync<List<PolicyDto>>() ?? [];
            if (tenantVersion != _tenantVersion) return;
            Policies.Clear();
            foreach (var policy in policies)
            {
                Policies.Add(policy);
            }

            SetStatus($"Loaded {Policies.Count} policies for {CurrentTenantName}.", false);
        }
        catch (Exception ex)
        {
            if (tenantVersion != _tenantVersion) return;
            SetStatus($"API unavailable: {ex.Message}", true);
        }
    }

    private async Task LoadDetailsAsync()
    {
        var tenantVersion = _tenantVersion;
        if (SelectedPolicy is null)
        {
            return;
        }

        SetStatus("Loading policy details…", false);
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/policies/{SelectedPolicy.Id}");
            using var response = await _httpClient.SendAsync(request);
            if (tenantVersion != _tenantVersion) return;
            if (!response.IsSuccessStatusCode)
            {
                SetStatus(await ReadErrorAsync(response), true);
                return;
            }

            var policy = await response.Content.ReadFromJsonAsync<PolicyDto>();
            if (tenantVersion != _tenantVersion) return;
            DetailsText = policy is null
                ? "The API returned an empty response."
                : $"{policy.Name}\n\n{policy.Description ?? "No description"}\n\n" +
                  $"Severity: {policy.Severity}\nEnabled: {policy.IsEnabled}\n" +
                  $"Created: {policy.CreatedAtUtc:u}\nUpdated: {policy.UpdatedAtUtc:u}";
            SetStatus("Policy details loaded.", false);
        }
        catch (Exception ex)
        {
            if (tenantVersion != _tenantVersion) return;
            SetStatus($"Could not load details: {ex.Message}", true);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var baseUri = new Uri(ApiBaseUrl.EndsWith('/') ? ApiBaseUrl : ApiBaseUrl + "/");
        var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath));
        request.Headers.Add("X-Tenant-Id", TenantId.ToString());
        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return $"API returned {(int)response.StatusCode} ({response.ReasonPhrase}). {body}";
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        StatusBrush = isError ? Brushes.Firebrick : Brushes.DarkGreen;
    }

    public void Dispose() => _httpClient.Dispose();
}
