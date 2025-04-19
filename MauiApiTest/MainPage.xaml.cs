using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Maui.Controls;

namespace MauiApiTest;

public partial class MainPage : ContentPage
{
	private readonly HttpClient _httpClient;
	private const string GET_URL = "https://httpbin.org/get";
	private const string POST_URL = "https://postman-echo.com/post";
	private int currentImageIndex = 1;
	private const int MaxImages = 2;

	public MainPage()
	{
		InitializeComponent();
		LoadImageButton.Text = $"Load Image {currentImageIndex}";
		_httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(30)
		};
		// Load the initial image
		MainThread.BeginInvokeOnMainThread(async () => await LoadCurrentImage());
	}

	#region Image Loading

	private async void OnLoadImageClicked(object sender, EventArgs e)
	{
		// Cycle to next image first
		currentImageIndex = (currentImageIndex % MaxImages) + 1;
		LoadImageButton.Text = $"Load Image {currentImageIndex}";
		
		await LoadCurrentImage();
	}

	private async Task LoadCurrentImage()
	{
		try
		{
			StatusLabel.Text = "Loading image...";
			ErrorLabel.Text = "";
			LoadImageButton.IsEnabled = false;

			string imageName = $"image{currentImageIndex}.png";
			System.Diagnostics.Debug.WriteLine($"Attempting to load image: {imageName}");
			
			await TryLoadImageAsync(imageName);
		}
		catch (Exception ex)
		{
			ErrorLabel.Text = $"Error: {ex.Message}";
			StatusLabel.Text = "Failed to load image";
			System.Diagnostics.Debug.WriteLine($"Error in LoadCurrentImage: {ex}");
		}
		finally
		{
			LoadImageButton.IsEnabled = true;
		}
	}

	private async Task TryLoadImageAsync(string imageName)
	{
		// Method 1: Try loading from embedded resource
		var assembly = Assembly.GetExecutingAssembly();
		
		// List all embedded resources for debugging
		System.Diagnostics.Debug.WriteLine("Available embedded resources:");
		foreach (var resource in assembly.GetManifestResourceNames())
		{
			System.Diagnostics.Debug.WriteLine($"  - {resource}");
		}

		// Try different resource path formats
		var resourcePaths = new[]
		{
			$"MauiApiTest.Resources.Raw.{imageName}",
			imageName,
			$"Resources.Raw.{imageName}",
			$"Raw.{imageName}"
		};

		foreach (var resourcePath in resourcePaths)
		{
			try
			{
				System.Diagnostics.Debug.WriteLine($"Trying embedded resource path: {resourcePath}");
				using var stream = assembly.GetManifestResourceStream(resourcePath);
				if (stream != null)
				{
					var imageData = ReadStream(stream);
					await MainThread.InvokeOnMainThreadAsync(() =>
					{
						MainImage.Source = ImageSource.FromStream(() => new MemoryStream(imageData));
						StatusLabel.Text = "Loaded from embedded resource";
						PathLabel.Text = $"Path: {resourcePath}";
					});
					return;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to load as embedded resource '{resourcePath}': {ex.Message}");
			}
		}

		// Method 2: Try loading from file system
		var fileSystemPaths = new[]
		{
			Path.Combine(FileSystem.AppDataDirectory, "Resources", "Raw", imageName),
			Path.Combine(FileSystem.AppDataDirectory, imageName),
			Path.Combine(FileSystem.Current.AppDataDirectory, "Resources", "Raw", imageName),
			Path.Combine("Resources", "Raw", imageName)
		};

		foreach (var path in fileSystemPaths)
		{
			System.Diagnostics.Debug.WriteLine($"Trying file system path: {path}");
			if (File.Exists(path))
			{
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					MainImage.Source = ImageSource.FromFile(path);
					StatusLabel.Text = "Loaded from file system";
					PathLabel.Text = $"Path: {path}";
				});
				return;
			}
		}

		// Method 3: Try loading as MAUI asset
		try
		{
			System.Diagnostics.Debug.WriteLine("Trying MAUI asset");
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				MainImage.Source = ImageSource.FromFile(imageName);
				StatusLabel.Text = "Loaded as MAUI asset";
				PathLabel.Text = $"Path: {imageName}";
			});
			return;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Failed to load as MAUI asset: {ex.Message}");
		}

		throw new FileNotFoundException($"Could not load image {imageName} using any available method");
	}

	private byte[] ReadStream(Stream stream)
	{
		using var memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	#endregion

	#region API Testing

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
			ResultLabel.Text = "Starting POST request...";
			GetButton.IsEnabled = false;
			PostButton.IsEnabled = false;

			// Create multipart form data content
			var content = new MultipartFormDataContent();

			try
			{
				ResultLabel.Text = "Loading image file...";
				var imageStream = await FileSystem.OpenAppPackageFileAsync($"Resources/Raw/image{currentImageIndex}.png");
				
				using (var memoryStream = new MemoryStream())
				{
					await imageStream.CopyToAsync(memoryStream);
					var imageBytes = memoryStream.ToArray();

					ResultLabel.Text = $"Image loaded, size: {imageBytes.Length} bytes. Creating content...";

					// Create image content
					var imageContent = new ByteArrayContent(imageBytes);
					imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

					// Add the image to the form data
					content.Add(imageContent, "file", $"image{currentImageIndex}.png");

					ResultLabel.Text = "Sending POST request...";

					try
					{
						// Send the request with timeout handling
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
						var response = await _httpClient.PostAsync(POST_URL, content, cts.Token);
						
						ResultLabel.Text = $"Response received. Status: {response.StatusCode}. Reading content...";
						
						if (response.IsSuccessStatusCode)
						{
							var responseString = await response.Content.ReadAsStringAsync();
							
							// Parse the response JSON to extract useful information
							try
							{
								var jsonResponse = JsonDocument.Parse(responseString);
								var root = jsonResponse.RootElement;
								
								// Extract file information if available
								string uploadDetails = "";
								if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
								{
									// Get the first file (should be our image)
									foreach (var file in files.EnumerateObject())
									{
										uploadDetails += $"✅ File uploaded: {file.Name}\n";
										
										// Get the first few characters of the base64 data
										string base64Preview = file.Value.GetString()?.Substring(0, 50) + "...";
										uploadDetails += $"Base64 preview: {base64Preview}\n\n";
										break; // Just show the first file
									}
								}
								
								// Extract form data if present
								if (root.TryGetProperty("form", out var form) && form.ValueKind == JsonValueKind.Object)
								{
									uploadDetails += "Form data included:\n";
									foreach (var field in form.EnumerateObject())
									{
										uploadDetails += $"- {field.Name}: {field.Value}\n";
									}
								}
								
								// Show the full JSON response after the summary
								ResultLabel.Text = $"POST REQUEST SUCCESSFUL\n\n{uploadDetails}\n\nFull response:\n{FormatJson(responseString)}";
							}
							catch
							{
								// If parsing fails, just show the raw response
								ResultLabel.Text = FormatJson(responseString);
							}
						}
						else
						{
							ResultLabel.Text = $"Server returned error: {response.StatusCode} - {response.ReasonPhrase}";
						}
					}
					catch (TaskCanceledException)
					{
						ResultLabel.Text = "Request timed out after 30 seconds";
					}
					catch (HttpRequestException hex)
					{
						ResultLabel.Text = $"HTTP Request failed: {hex.Message}";
					}
				}
			}
			catch (Exception ex)
			{
				ResultLabel.Text = $"Error during image processing: {ex.GetType().Name} - {ex.Message}";
			}
		}
		catch (Exception ex)
		{
			ResultLabel.Text = $"Error in POST request: {ex.GetType().Name} - {ex.Message}";
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

	#endregion
}

