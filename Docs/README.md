# EWFDS.BlazorInfrastructure

A shared Razor Class Library containing common infrastructure services for EWFDS Blazor applications.

## Overview

This library provides reusable infrastructure components that can be shared across multiple EWFDS Blazor applications, ensuring consistency and reducing code duplication.

## Installation

Add a project reference to your Blazor application:

```xml
<ProjectReference Include="..\EWFDS.BlazorInfrastructure\EWFDS.BlazorInfrastructure\EWFDS.BlazorInfrastructure.csproj" />
```

## Usage

### Register Services

In your `Program.cs`:

```csharp
using EWFDS.BlazorInfrastructure.Extensions;

// Add all infrastructure services
builder.Services.AddEwfdsBlazorInfrastructure();

// Or add individual services
builder.Services.AddThemeService();
builder.Services.AddUserStateService();
```

### Theme Service

The theme service provides Telerik Kendo theme switching with localStorage persistence.

```csharp
@using EWFDS.BlazorInfrastructure.Services.Theming
@inject IThemeService ThemeService

// Initialize on app startup (e.g., in MainLayout)
await ThemeService.InitializeAsync();

// Change theme
await ThemeService.SetThemeAsync("fluent-main");

// Get available themes
var themes = ThemeService.AvailableThemes;
```

### Base Components with Authentication

Inherit from `ComponentBaseWithAuth` or `LayoutComponentBaseWithAuth` for automatic user state management:

```csharp
@using EWFDS.BlazorInfrastructure.Services.Authorization
@inherits ComponentBaseWithAuth

// CurrentUser is automatically populated
<p>Welcome, @UserFullName!</p>

// Access user properties
@if (IsWFDSStaff)
{
    <p>Staff-only content</p>
}
```

### Implementing IApplicationUserIdentity

Your application must provide an implementation of `IApplicationUserIdentity`:

```csharp
using EWFDS.BlazorInfrastructure.Services.Identity;

public class ApplicationUserIdentity : IApplicationUserIdentity
{
    // Implement all interface members
}
```

Register it in your DI container:
```csharp
builder.Services.AddScoped<IApplicationUserIdentity, ApplicationUserIdentity>();
```

## Phased Implementation

This library is being built incrementally:

### ✅ Phase 1: Core Foundation (Complete)
- Theme Service (`IThemeService`, `ThemeService`)
- Service registration extensions
- JavaScript interop for theme switching

### ✅ Phase 2: User Identity & Authorization (Complete)
- `IApplicationUserIdentity` interface
- `IUserStateService`, `UserStateService`
- `ComponentBaseWithAuth` base class
- `LayoutComponentBaseWithAuth` base class

### 🔜 Phase 3: Additional Services
- Navigation services
- Login services
- Error handling middleware

### 🔜 Phase 4: Shared UI Components
- `StatusMessage.razor`
- `GlobalErrorBoundary.razor`
- `SelectComponentBase.cs`

## Static Web Assets

JavaScript files from this library are served from:
```
_content/EWFDS.BlazorInfrastructure/js/themeService.js
```

## Dependencies

- .NET 10.0
- Microsoft.AspNetCore.App (Framework Reference)
- EWFDSBL8 (for business library types)

## Namespaces

| Namespace | Purpose |
|-----------|---------|
| `EWFDS.BlazorInfrastructure.Services.Theming` | Theme switching services |
| `EWFDS.BlazorInfrastructure.Services.Identity` | User identity interface |
| `EWFDS.BlazorInfrastructure.Services.State` | User state management |
| `EWFDS.BlazorInfrastructure.Services.Authorization` | Base component classes |
| `EWFDS.BlazorInfrastructure.Extensions` | DI registration extensions |
