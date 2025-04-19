# MAUI API Test

A simple .NET MAUI application demonstrating image loading and API testing.

## Requirements

- .NET 9.0 SDK or later
- For MacCatalyst (macOS):
  - macOS 12.0 or later
  - Xcode 14.0 or later

## Installation

1. Clone this repository
2. Ensure you have the .NET MAUI workload installed:
```
dotnet workload install maui
```

## Running the Application

To run the application on MacCatalyst:
```
dotnet build -t:Run -f net9.0-maccatalyst
```

## Features

- Image loading from embedded resources
- GET requests to httpbin.org
- POST requests with image upload to httpbin.org/post