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
	private const string POST_URL = "https://httpbin.org/post";
	private int currentImageIndex = 1;
	private const int MaxImages = 2;
	private string _capturedPhotoPath;

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

	#region Camera Functionality

	private async void OnGalleryClicked(object sender, EventArgs e)
	{
		GalleryButton.IsEnabled = false;
		try
		{
			await PickImageFromGallery();
		}
		finally
		{
			GalleryButton.IsEnabled = true;
		}
	}

	private async void OnCameraClicked(object sender, EventArgs e)
	{
		try
		{
			if (!MediaPicker.Default.IsCaptureSupported)
			{
				await DisplayAlert("Not Supported", "Camera is not supported on this device", "OK");
				
				// Fallback to file picker if camera not supported
				await PickImageFromGallery();
				return;
			}

			var status = await CheckAndRequestCameraPermission();
			if (status != PermissionStatus.Granted)
			{
				await DisplayAlert("Permission Denied", "Camera permission is required to take photos", "OK");
				return;
			}

			CameraButton.IsEnabled = false;
			StatusLabel.Text = "Opening camera...";

			try
			{
				var photo = await MediaPicker.Default.CapturePhotoAsync();
				if (photo != null)
				{
					await ProcessCapturedPhoto(photo);
				}
				else
				{
					StatusLabel.Text = "Photo capture cancelled";
					
					// Ask if user wants to use file picker instead
					bool useFilePicker = await DisplayAlert("Camera Issue", 
						"Camera capture failed or was cancelled. Would you like to choose an image from your gallery instead?", 
						"Yes", "No");
					
					if (useFilePicker)
					{
						await PickImageFromGallery();
					}
				}
			}
			catch (Exception cameraEx)
			{
				// Camera capture failed - log and offer alternative
				System.Diagnostics.Debug.WriteLine($"Camera capture exception: {cameraEx}");
				ErrorLabel.Text = $"Camera error: {cameraEx.Message}";
				
				bool useFilePicker = await DisplayAlert("Camera Error", 
					"An error occurred while trying to use the camera. Would you like to choose an image from your gallery instead?", 
					"Yes", "No");
				
				if (useFilePicker)
				{
					await PickImageFromGallery();
				}
			}
		}
		catch (Exception ex)
		{
			ErrorLabel.Text = $"Error: {ex.Message}";
			StatusLabel.Text = "Failed to access camera";
			System.Diagnostics.Debug.WriteLine($"General camera error: {ex}");
		}
		finally
		{
			CameraButton.IsEnabled = true;
		}
	}

	private async Task PickImageFromGallery()
	{
		try
		{
			StatusLabel.Text = "Opening gallery...";
			
			// Request storage permission if needed
			var status = await CheckAndRequestStoragePermission();
			if (status != PermissionStatus.Granted)
			{
				await DisplayAlert("Permission Denied", "Storage permission is required to pick photos", "OK");
				return;
			}
			
			var result = await MediaPicker.Default.PickPhotoAsync();
			if (result != null)
			{
				await ProcessCapturedPhoto(result);
			}
			else
			{
				StatusLabel.Text = "Photo selection cancelled";
			}
		}
		catch (Exception ex)
		{
			ErrorLabel.Text = $"Gallery error: {ex.Message}";
			StatusLabel.Text = "Failed to pick image";
			System.Diagnostics.Debug.WriteLine($"Gallery pick error: {ex}");
		}
	}

	private async Task ProcessCapturedPhoto(FileResult photo)
	{
		// Save the captured photo to a local file
		_capturedPhotoPath = await SavePhotoToLocalStorage(photo);
		
		// Display the captured photo
		MainImage.Source = ImageSource.FromFile(_capturedPhotoPath);
		StatusLabel.Text = "Photo loaded!";
		PathLabel.Text = $"Path: {_capturedPhotoPath}";
		
		// Enable POST button for camera image
		PostCameraButton.IsEnabled = true;
	}

	private async Task<string> SavePhotoToLocalStorage(FileResult photo)
	{
		// Create a unique filename with proper extension
		string extension = Path.GetExtension(photo.FileName);
		if (string.IsNullOrEmpty(extension))
		{
			extension = ".jpg"; // Default to jpg if no extension
		}
		
		string localFilePath = Path.Combine(FileSystem.CacheDirectory, $"photo_{DateTime.Now:yyyyMMddHHmmss}{extension}");

		try
		{
			using Stream sourceStream = await photo.OpenReadAsync();
			using FileStream localFileStream = File.OpenWrite(localFilePath);
			await sourceStream.CopyToAsync(localFileStream);
			
			System.Diagnostics.Debug.WriteLine($"Photo saved to: {localFilePath}");
			return localFilePath;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error saving photo: {ex}");
			throw;
		}
	}

	private async Task<PermissionStatus> CheckAndRequestCameraPermission()
	{
		PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Camera>();

		if (status != PermissionStatus.Granted)
		{
			status = await Permissions.RequestAsync<Permissions.Camera>();
		}

		return status;
	}
	
	private async Task<PermissionStatus> CheckAndRequestStoragePermission()
	{
		PermissionStatus status;
		
		// On Android API 33+ we need to use the Photos permission
		if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.Version.Major >= 13)
		{
			status = await Permissions.CheckStatusAsync<Permissions.Photos>();
			if (status != PermissionStatus.Granted)
			{
				status = await Permissions.RequestAsync<Permissions.Photos>();
			}
		}
		else
		{
			status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
			if (status != PermissionStatus.Granted)
			{
				status = await Permissions.RequestAsync<Permissions.StorageRead>();
			}
		}
		
		return status;
	}

	private async void OnPostCameraClicked(object sender, EventArgs e)
	{
		if (string.IsNullOrEmpty(_capturedPhotoPath) || !File.Exists(_capturedPhotoPath))
		{
			await DisplayAlert("Error", "No camera image available. Please capture a photo first.", "OK");
			return;
		}

		try
		{
			ResultLabel.Text = "Starting POST request with camera image...";
			GetButton.IsEnabled = false;
			PostButton.IsEnabled = false;
			PostCameraButton.IsEnabled = false;

			// Create multipart form data content
			var content = new MultipartFormDataContent();

			try
			{
				ResultLabel.Text = "Reading camera image...";
				byte[] imageBytes = File.ReadAllBytes(_capturedPhotoPath);

				ResultLabel.Text = $"Camera image loaded, size: {imageBytes.Length} bytes. Creating content...";

				// Create image content
				var imageContent = new ByteArrayContent(imageBytes);
				imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

				// Add the image to the form data
				var filename = Path.GetFileName(_capturedPhotoPath);
				content.Add(imageContent, "file", filename);

				// Add a custom form field
				content.Add(new StringContent("camera_captured"), "source");

				ResultLabel.Text = "Sending POST request with camera image...";

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
							ResultLabel.Text = $"CAMERA IMAGE POST SUCCESSFUL\n\n{uploadDetails}\n\nFull response:\n{FormatJson(responseString)}";
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
			catch (Exception ex)
			{
				ResultLabel.Text = $"Error processing camera image: {ex.GetType().Name} - {ex.Message}";
			}
		}
		catch (Exception ex)
		{
			ResultLabel.Text = $"Error in camera POST request: {ex.GetType().Name} - {ex.Message}";
		}
		finally
		{
			GetButton.IsEnabled = true;
			PostButton.IsEnabled = true;
			PostCameraButton.IsEnabled = true;
		}
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

