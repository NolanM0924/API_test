using System.Text;
using System.Text.Json;

namespace MauiApiTest;

public partial class MainPage : ContentPage
{
	private readonly HttpClient _httpClient;
	private const string GET_URL = "https://httpbin.org/get";
	private const string POST_URL = "https://httpbin.org/post";

	public MainPage()
	{
		InitializeComponent();
		_httpClient = new HttpClient();
	}

	private async void OnGetClicked(object sender, EventArgs e)
	{
		try
		{
			ResultLabel.Text = "Loading GET request...";
			GetButton.IsEnabled = false;
			PostButton.IsEnabled = false;

			var response = await _httpClient.GetStringAsync(GET_URL);
			ResultLabel.Text = FormatJson(response);
		}
		catch (Exception ex)
		{
			ResultLabel.Text = $"Error: {ex.Message}";
		}
		finally
		{
			GetButton.IsEnabled = true;
			PostButton.IsEnabled = true;
		}
	}

	private async void OnPostClicked(object sender, EventArgs e)
	{
		try
		{
			ResultLabel.Text = "Loading POST request...";
			GetButton.IsEnabled = false;
			PostButton.IsEnabled = false;

			var content = new StringContent(
				JsonSerializer.Serialize(new { message = "Hello from MAUI!" }),
				Encoding.UTF8,
				"application/json");

			var response = await _httpClient.PostAsync(POST_URL, content);
			var responseString = await response.Content.ReadAsStringAsync();
			ResultLabel.Text = FormatJson(responseString);
		}
		catch (Exception ex)
		{
			ResultLabel.Text = $"Error: {ex.Message}";
		}
		finally
		{
			GetButton.IsEnabled = true;
			PostButton.IsEnabled = true;
		}
	}

	private string FormatJson(string json)
	{
		try
		{
			var jsonDoc = JsonDocument.Parse(json);
			return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
			{
				WriteIndented = true
			});
		}
		catch
		{
			return json;
		}
	}
}

