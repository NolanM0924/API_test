# MAUI API Test

A simple .NET MAUI application demonstrating image loading, camera capture, and API testing.

## Requirements

- .NET 9.0 SDK or later
- For MacCatalyst (macOS):
  - macOS 12.0 or later
  - Xcode 14.0 or later
- For Android:
  - Android SDK Platform API 21 or higher
  - Android device or emulator with camera

## Installation

1. Clone this repository
2. Ensure you have the .NET MAUI workload installed:
```
dotnet workload install maui
```

## Running the Application

### MacCatalyst
To run the application on MacCatalyst:
```
dotnet build -t:Run -f net9.0-maccatalyst
```

### Android
To run the application on Android:

1. Connect an Android device via USB (with USB debugging enabled) or start an Android emulator

2. Run the following command:
```
dotnet build -t:Run -f net9.0-android
```

Alternatively, you can specify a target device:
```
dotnet build -t:Run -f net9.0-android -p:AndroidTarget=<target-id>
```

The target ID can be obtained by running:
```
adb devices
```

## Features

- Image loading from embedded resources
- Camera integration to capture photos
- GET requests to httpbin.org
- POST requests with image upload to httpbin.org/post
- POST requests with camera-captured photos

## Camera Functionality

The app allows you to:
1. Capture photos using your device's camera
2. View the captured photos
3. Send the captured photos via POST request to httpbin.org/post
4. View the complete server response