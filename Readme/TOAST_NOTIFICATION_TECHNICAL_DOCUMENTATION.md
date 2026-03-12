# 🔔 Real-Time Toast Notification System - Technical Documentation

**SmartClinic Hospital Management System**  
**Blazor Server - Real-Time Notifications via SignalR**

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Architecture Diagram](#architecture-diagram)
3. [Component Responsibilities](#component-responsibilities)
4. [Technical Flow](#technical-flow)
5. [Code Implementation](#code-implementation)
6. [Global Toast Display](#global-toast-display)
7. [Optional Manual Toasts](#optional-manual-toasts)
8. [Design Patterns & Best Practices](#design-patterns--best-practices)
9. [Troubleshooting Guide](#troubleshooting-guide)

---

## System Overview

The toast notification system is a **multi-layer, event-driven architecture** that bridges server-side SignalR events with client-side UI notifications in Blazor Server.

### Key Characteristics

- **Real-time**: Uses SignalR WebSocket for instant updates
- **Decoupled**: Services don't directly call UI; they raise events
- **Global**: Notifications appear on any page without per-page setup
- **Flexible**: Supports SignalR events (automatic) and manual toasts (optional)
- **Singleton**: NotificationService maintains a single connection for the entire app lifetime
- **Thread-safe**: Properly handles async/await and component lifecycle

### Design Goals

✅ Single SignalR connection (no duplicates)  
✅ Global notification system (works on all pages)  
✅ Separation of concerns (SignalR vs UI vs Events)  
✅ Minimal per-page code (no subscription boilerplate)  
✅ Graceful degradation (works without manual toast support)  

---

## Architecture Diagram

### High-Level View

```
┌─────────────────────────────────────────────────────────────┐
│                         Backend Server                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  PatientService / ExaminationService / etc.                │
│  └─ Performs business logic                                │
│  └─ Creates/updates entities                               │
│  └─ Triggers SignalR broadcast                             │
│     └─ patientHub.Clients.All.SendAsync("QueueTicketUpdated", ...)
│                                                             │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ WebSocket (SignalR)
                         │ "QueueTicketUpdated" event
                         ↓
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (Blazor Client)                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  NotificationService (Singleton)                           │
│  ├─ Maintains HubConnection to SignalR hub                 │
│  ├─ Listens to "QueueTicketUpdated" event                  │
│  └─ Raises C# event: OnPatientQueueUpdated(patientName)    │
│                                                             │
│  ↓                                                           │
│                                                             │
│  DoctorLayout (Global Layout - Persistent)                 │
│  ├─ <ToastHost /> (global component)                       │
│  │  ├─ Subscribes to OnPatientQueueUpdated event           │
│  │  ├─ Calls ToastService.ShowToastAsync()                │
│  │  └─ Calls JS.InvokeVoidAsync("appToasts.show", ...)    │
│  └─ @Body                                                  │
│     ├─ Index.razor (dashboard page)                        │
│     ├─ MyPatient.razor (queue page)                        │
│     └─ Any other doctor page                               │
│                                                             │
│  ↓                                                           │
│                                                             │
│  JavaScript (notifications.js)                             │
│  ├─ window.appToasts.show(message, type, url)             │
│  └─ Creates and displays toast UI on screen               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow Sequence

```
Time    Server                  Network              Client (Blazor)         JavaScript
════════════════════════════════════════════════════════════════════════════════════════
 0ms   PatientService.Add()
       ├─ Create patient
       ├─ Save to DB
       └─ Call patientHub.SendAsync(
          "QueueTicketUpdated")
                                   │
                                   │ WebSocket
                                   │ Binary Message
                                   ↓
                                                NotificationService
                                                └─ _hubConnection.On()
                                                   listener fires
                                                ├─ Extract: patientName
                                                └─ OnPatientQueueUpdated
                                                   .Invoke(patientName)
                                                   │
 50ms                                            ↓
                                             ToastHost
                                             ├─ Event handler called
                                             ├─ InvokeAsync() dispatch
                                             └─ ToastService
                                                .ShowToastAsync()
                                                   │
                                                   ↓
                                                JS.InvokeVoidAsync(
                                                "appToasts.show", ...)
                                                   │
                                                   │ JS Interop
                                                   ↓
                                                          window.appToasts
                                                          .show()
                                                          ├─ Create DOM
                                                          ├─ Add CSS
                                                          ├─ Show toast
                                                          └─ Auto-hide
                                                             after 4s
100ms                                                              ┌─────────┐
                                                                  │ TOAST   │
                                                                  │ VISIBLE │
                                                                  └─────────┘
```

---

## Component Responsibilities

### 1. NotificationService (Singleton)

**Location**: `Services/NotificationService.cs`

**Lifetime**: Singleton (one instance per app lifetime)

**Responsibility**: Manage SignalR connection and translate hub events to C# events

**Key Features**:
- ✅ Maintains single HubConnection to SignalR hub
- ✅ Connects on-demand via `EnsureStartedAsync()`
- ✅ Auto-reconnect with exponential backoff
- ✅ Registers listener for `"QueueTicketUpdated"` event
- ✅ Raises C# event `OnPatientQueueUpdated(string patientName)`
- ❌ Does NOT display UI
- ❌ Does NOT call JavaScript
- ❌ Does NOT know about pages or layouts

**Code Example**:
```csharp
public class NotificationService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    
    // C# event that subscribers can listen to
    public event Action<string>? OnPatientQueueUpdated;
    
    public async Task EnsureStartedAsync()
    {
        if (IsConnected) return;
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("/hubs/patient")
            .WithAutomaticReconnect()
            .Build();
        
        RegisterEventHandlers();
        await _hubConnection.StartAsync();
    }
    
    private void RegisterEventHandlers()
    {
        _hubConnection?.On<string>("QueueTicketUpdated", (patientName) =>
        {
            // Translate SignalR event to C# event
            OnPatientQueueUpdated?.Invoke(patientName);
        });
    }
}
```

---

### 2. ToastNotificationService (Event Publisher)

**Location**: `Services/ToastNotificationService.cs`

**Lifetime**: Scoped (per request/component instance)

**Responsibility**: Provide API for showing toasts; publish events

**Key Features**:
- ✅ Provides `ShowToastAsync(message, type, url)` method
- ✅ Raises event `OnToast(message, type, url)` for subscribers
- ✅ Convenience methods: `ShowSuccessAsync()`, `ShowErrorAsync()`, etc.
- ✅ Thread-safe event invocation with error handling
- ❌ Does NOT call JavaScript directly
- ❌ Does NOT manage SignalR connection
- ❌ Does NOT know about notifications.js

**Code Example**:
```csharp
public class ToastNotificationService
{
    // Event for toast display requests
    public event Func<string, string, string?, Task>? OnToast;
    
    public async Task ShowToastAsync(string message, string type = "info", string? url = null)
    {
        if (OnToast != null)
        {
            try
            {
                await OnToast.Invoke(message, type, url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
            }
        }
    }
    
    // Convenience methods
    public async Task ShowSuccessAsync(string message, string? url = null)
        => await ShowToastAsync(message, "success", url);
}
```

---

### 3. ToastHost.razor (Global Bridge Component)

**Location**: `Web/Components/ToastHost.razor`

**Placement**: Inside `DoctorLayout.razor` (rendered once, persists across page navigation)

**Lifetime**: Layout lifetime (not destroyed during page changes)

**Responsibility**: Bridge C# events to JavaScript; handle both SignalR and manual toasts

**Key Features**:
- ✅ Subscribes to `NotificationService.OnPatientQueueUpdated` (SignalR events)
- ✅ Optionally subscribes to `ToastService.OnToast` (manual toasts)
- ✅ Calls `ToastService.ShowToastAsync()` for SignalR events
- ✅ Calls JavaScript via `JS.InvokeVoidAsync("appToasts.show", ...)`
- ✅ Handles Blazor component lifecycle properly
- ✅ Unsubscribes on disposal to prevent memory leaks
- ❌ Does NOT manage SignalR connection
- ❌ Does NOT implement business logic

**Code Example**:
```razor
@using SmartClinic.Services
@using Microsoft.AspNetCore.SignalR.Client
@inject NotificationService SignalRService
@inject ToastNotificationService ToastService
@inject IJSRuntime JS
@implements IAsyncDisposable

@code {
    protected override void OnInitialized()
    {
        // Subscribe to SignalR events
        SignalRService.OnPatientQueueUpdated += HandlePatientQueueUpdatedAsync;
        
        // Subscribe to manual toasts (optional)
        ToastService.OnToast += HandleToastAsync;
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Ensure connection is established
            if (!SignalRService.IsConnected)
            {
                await SignalRService.EnsureStartedAsync();
            }
        }
    }
    
    private async void HandlePatientQueueUpdatedAsync(string patientName)
    {
        // Called when new patient added via SignalR
        await ToastService.ShowToastAsync(
            $"✓ {patientName} added to queue",
            "success"
        );
    }
    
    private async Task HandleToastAsync(string message, string type, string? url)
    {
        // Called by either SignalR or manual toast requests
        await JS.InvokeVoidAsync("appToasts.show", message, type, url);
    }
    
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        SignalRService.OnPatientQueueUpdated -= HandlePatientQueueUpdatedAsync;
        ToastService.OnToast -= HandleToastAsync;
        await ValueTask.CompletedTask;
    }
}
```

---

## Technical Flow

### Complete End-to-End Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 1: Backend Event Trigger                                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  // In PatientService.cs                                                   │
│  public async Task AddQueueTicketAsync(int doctorId, int patientId, ...)   │
│  {                                                                          │
│      var ticket = new QueueTicket { ... };                                 │
│      await _dbContext.QueueTickets.AddAsync(ticket);                       │
│      await _dbContext.SaveChangesAsync();                                  │
│                                                                             │
│      // Broadcast to all connected clients                                 │
│      await _hubContext.Clients.All.SendAsync(                              │
│          "QueueTicketUpdated",                                             │
│          new { doctorId, ticketId, patientName = "John Doe" }             │
│      );                                                                     │
│  }                                                                          │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 2: SignalR Network Transmission                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Backend sends:                                                             │
│  {                                                                          │
│    "target": "QueueTicketUpdated",                                         │
│    "arguments": [{ "doctorId": 1, "ticketId": 5, "patientName": "John" }] │
│  }                                                                          │
│                                                                             │
│  Through: WebSocket connection (low-latency, persistent)                   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 3: NotificationService Receives Event                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  // In NotificationService.RegisterEventHandlers()                         │
│  _hubConnection.On<JsonElement>("QueueTicketUpdated", (data) =>           │
│  {                                                                          │
│      string patientName = data.GetProperty("patientName").GetString();     │
│      int doctorId = data.GetProperty("doctorId").GetInt32();              │
│                                                                             │
│      // Filter: Only notify if event is for current doctor                 │
│      if (doctorId == 1)  // Current user's doctor ID                       │
│      {                                                                      │
│          // Raise C# event                                                 │
│          OnPatientQueueUpdated?.Invoke(patientName);                       │
│      }                                                                      │
│  });                                                                        │
│                                                                             │
│  ✓ OnPatientQueueUpdated event fired with patientName = "John Doe"        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 4: ToastHost Receives C# Event                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  // In ToastHost.OnInitialized()                                           │
│  SignalRService.OnPatientQueueUpdated += HandlePatientQueueUpdatedAsync;   │
│                                                                             │
│  // Handler called:                                                        │
│  private async void HandlePatientQueueUpdatedAsync(string patientName)     │
│  {                                                                          │
│      await InvokeAsync(async () =>                                         │
│      {                                                                      │
│          // Dispatch to ToastNotificationService                           │
│          await ToastService.ShowToastAsync(                                │
│              $"✓ {patientName} added to queue",                            │
│              "success"                                                      │
│          );                                                                │
│      });                                                                    │
│  }                                                                          │
│                                                                             │
│  ✓ ShowToastAsync() called with message and type                          │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 5: ToastService Publishes Event                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  // In ToastNotificationService.ShowToastAsync()                           │
│  public async Task ShowToastAsync(string message, string type, string? url) │
│  {                                                                          │
│      if (OnToast != null)                                                  │
│      {                                                                      │
│          // Raise event to all subscribers (ToastHost listens)            │
│          await OnToast.Invoke(message, type, url);                         │
│      }                                                                      │
│  }                                                                          │
│                                                                             │
│  ✓ OnToast event fired with (message, type="success", url=null)           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 6: ToastHost Calls JavaScript                                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  // In ToastHost.HandleToastAsync()                                        │
│  private async Task HandleToastAsync(string message, string type, string? url) │
│  {                                                                          │
│      // Call JavaScript function with parameters                          │
│      await JS.InvokeVoidAsync("appToasts.show", message, type, url);      │
│  }                                                                          │
│                                                                             │
│  ✓ JavaScript function "appToasts.show" invoked                            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 7: JavaScript Creates Toast UI                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  // In wwwroot/js/notifications.js                                         │
│  window.appToasts.show = function(message, type, url) {                    │
│      // Create toast container if not exists                               │
│      let container = document.getElementById('toast-container');           │
│      if (!container) {                                                     │
│          container = document.createElement('div');                        │
│          container.id = 'toast-container';                                 │
│          document.body.appendChild(container);                             │
│      }                                                                      │
│                                                                             │
│      // Create toast element                                               │
│      const toast = document.createElement('div');                          │
│      toast.className = `toast toast-${type}`;                              │
│      toast.innerText = message;                                            │
│      container.appendChild(toast);                                         │
│                                                                             │
│      // Add CSS classes for styling & animation                            │
│      // Set auto-dismiss timer                                             │
│      // Handle click navigation if URL provided                            │
│  }                                                                          │
│                                                                             │
│  ✓ Toast DOM element created and added to page                             │
│  ✓ CSS animation applied (fade-in + scale)                                │
│  ✓ Toast visible on screen!                                                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Timing Summary

```
Event occurs        → 0ms
  ↓
SignalR broadcast   → ~10ms (network latency)
  ↓
NotificationService receives   → ~15ms
  ↓
ToastHost handler executes     → ~20ms (minimal Blazor dispatch)
  ↓
ToastService.ShowAsync() calls ToastService.OnToast event → ~21ms
  ↓
HandleToastAsync() calls JS.InvokeVoidAsync() → ~22ms
  ↓
JavaScript executes appToasts.show() → ~25ms
  ↓
Toast visible on screen → ~75ms (includes animation duration)

Total latency: ~75ms (imperceptible to user)
```

---

## Code Implementation

### NotificationService Setup (Program.cs)

```csharp
// Register as Singleton (one connection for entire app)
builder.Services.AddSingleton<NotificationService>();

// In Global SignalR initialization (optional, but recommended)
_ = app.Services.GetRequiredService<NotificationService>().EnsureStartedAsync();
```

### ToastHost Layout Placement (DoctorLayout.razor)

```razor
@* DoctorLayout.razor *@
@inherits LayoutComponentBase

<div class="page">
    <!-- Toast Host: Global component for notifications -->
    <ToastHost />
    
    <!-- Sidebar navigation -->
    <aside class="sidebar">
        <!-- Navigation items -->
    </aside>
    
    <!-- Main content -->
    <main>
        @Body
    </main>
</div>
```

### ToastHost Complete Implementation

```razor
@using SmartClinic.Services
@using Microsoft.AspNetCore.SignalR.Client
@inject NotificationService SignalRService
@inject ToastNotificationService ToastService
@inject IJSRuntime JS
@implements IAsyncDisposable

@code {
    /// <summary>
    /// PHASE 1: Subscribe to events
    /// Called during component initialization (before render)
    /// </summary>
    protected override void OnInitialized()
    {
        // Subscribe to SignalR events (auto-toasts)
        SignalRService.OnPatientQueueUpdated += HandlePatientQueueUpdatedAsync;
        
        // Optional: Subscribe to manual toasts (if needed)
        ToastService.OnToast += HandleToastAsync;
        
        System.Diagnostics.Debug.WriteLine("[ToastHost] Subscribed to notification events");
    }

    /// <summary>
    /// PHASE 2: Ensure SignalR connection
    /// Called after component renders (JS is available)
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (!SignalRService.IsConnected)
            {
                await SignalRService.EnsureStartedAsync();
            }
            
            System.Diagnostics.Debug.WriteLine("[ToastHost] SignalR connection established");
        }
    }

    /// <summary>
    /// Handle SignalR events (automatic toast from backend)
    /// </summary>
    private async void HandlePatientQueueUpdatedAsync(string patientName)
    {
        await InvokeAsync(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ToastHost] Patient added: {patientName}");
                
                // Show toast
                await ToastService.ShowToastAsync(
                    $"✓ {patientName} added to queue",
                    "success"
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ToastHost] Error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Handle toast events (manual toasts from pages)
    /// </summary>
    private async Task HandleToastAsync(string message, string type, string? url)
    {
        try
        {
            // Call JavaScript to display toast
            await JS.InvokeVoidAsync("appToasts.show", message, type, url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ToastHost] JS invocation error: {ex.Message}");
        }
    }

    /// <summary>
    /// PHASE 3: Cleanup on disposal
    /// Called when layout is destroyed (rarely happens)
    /// </summary>
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        SignalRService.OnPatientQueueUpdated -= HandlePatientQueueUpdatedAsync;
        ToastService.OnToast -= HandleToastAsync;
        
        System.Diagnostics.Debug.WriteLine("[ToastHost] Unsubscribed from events");
        
        await ValueTask.CompletedTask;
    }
}
```

---

## Global Toast Display

### Why ToastHost Ensures Global Notifications

```
┌─────────────────────────────────────────┐
│ App.razor (Root)                        │
└──────────────────┬──────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────┐
│ DoctorLayout (Global Layout)            │
├─────────────────────────────────────────┤
│                                         │
│ <ToastHost /> ← PERSISTENT              │
│ (Subscribed to events, never destroyed) │
│                                         │
│ ┌───────────────────────────────────┐  │
│ │ @Body (Page content)              │  │
│ ├───────────────────────────────────┤  │
│ │ Index.razor (Dashboard)           │  │
│ │ ├─ NO notification code needed    │  │
│ │ └─ Toast still appears! ✓         │  │
│ │                                   │  │
│ │ MyPatient.razor (Queue page)      │  │
│ │ ├─ Subscribes locally (optional)  │  │
│ │ └─ Gets toast from ToastHost ✓    │  │
│ │                                   │  │
│ │ Examination.razor (Exam page)     │  │
│ │ ├─ NO notification code needed    │  │
│ │ └─ Toast still appears! ✓         │  │
│ └───────────────────────────────────┘  │
│                                         │
└─────────────────────────────────────────┘

Key Insight:
━━━━━━━━━━━
DoctorLayout is PERSISTENT (never destroyed on page navigation)
→ ToastHost is PERSISTENT (never unsubscribed)
→ ToastHost listens to OnPatientQueueUpdated FOREVER
→ Toast appears on ANY page, ANYTIME

Without ToastHost in layout:
- Each page would need to subscribe separately
- Pages recreated on navigation = subscriptions lost
- Fragile, error-prone, duplicated code
```

### Testing Global Notifications

```
Step 1: Navigate to Index.razor (dashboard)
        → ToastHost is active, listening

Step 2: Do NOT click any button (no local code to add patients)

Step 3: From ANOTHER browser tab/window, use MyPatient.razor
        → Click "Add Test Ticket"
        → Backend broadcasts "QueueTicketUpdated"

Step 4: Switch back to Index.razor tab
        → ✓ Toast appears automatically!
        → No code in Index.razor, no user action needed
        → ToastHost caught the event and displayed it

Result: GLOBAL notification ✓
```

---

## Optional Manual Toasts

### When to Use Manual Toasts

The `ToastService.OnToast` event is **optional**. It's useful only if you want pages to manually trigger toasts:

#### Scenario 1: SignalR-Only (Current Setup)

```csharp
// ❌ Manual toasts NOT used
// ✅ Only SignalR events trigger toasts

// Flow: Backend → SignalR → ToastHost → JS → Toast
// ToastService.OnToast is subscribed but never used
```

**When to use**: 
- Pure event-driven system
- Backend controls all notifications
- Pages have no custom toast logic

---

#### Scenario 2: With Manual Toasts (Optional Enhancement)

```csharp
// ✅ Manual toasts ARE used
// ✅ Pages can show toasts on user actions

@inject ToastNotificationService Toast

<button @onclick="SavePatient">Save Patient</button>

@code {
    private async Task SavePatient()
    {
        try
        {
            await PatientService.SaveAsync(...);
            
            // ✅ Show manual toast
            await Toast.ShowSuccessAsync("Patient saved!");
        }
        catch (Exception ex)
        {
            // ✅ Show error toast
            await Toast.ShowErrorAsync($"Error: {ex.Message}");
        }
    }
}
```

**When to use**:
- Pages need immediate feedback on user actions
- Local validation errors
- Success messages for operations
- Progress indicators

---

### How Manual Toasts Work

```
Page calls:
  await Toast.ShowToastAsync("Message", "success")
    ↓
ToastService.ShowToastAsync() raises:
  OnToast?.Invoke(message, type, url)
    ↓
ToastHost.HandleToastAsync() catches it:
  await JS.InvokeVoidAsync("appToasts.show", ...)
    ↓
JavaScript creates toast UI
```

### Decision Tree: SignalR vs Manual

```
Need to notify user?
│
├─ Backend triggered (patient added, status changed)?
│  └─ ✓ Use SignalR (automatic via ToastHost)
│
└─ User action triggered (button clicked, form submitted)?
   └─ ✓ Use Manual Toast (optional, page calls ShowToastAsync)
```

---

## Design Patterns & Best Practices

### Pattern 1: Separation of Concerns

```csharp
❌ BAD: Everything mixed together
public class BadToastService
{
    public void ShowToast(string message)
    {
        // Creates connection (❌ should be in NotificationService)
        var connection = new HubConnectionBuilder()...Build();
        
        // Calls JS directly (❌ should be in component)
        JS.InvokeVoidAsync("appToasts.show", message);
        
        // Manages styling (❌ should be in CSS/JS)
        ApplyCSS();
    }
}

✅ GOOD: Each layer has one responsibility
NotificationService  → Manage SignalR connection
ToastService         → Publish events
ToastHost           → Bridge events to JS
JavaScript          → Render UI and handle styling
```

### Pattern 2: Event-Driven Decoupling

```csharp
❌ BAD: Tight coupling
public class NotificationService
{
    [Inject] private ToastNotificationService ToastService { get; set; }
    [Inject] private IJSRuntime JS { get; set; }
    
    private void OnQueueTicketUpdated(string patientName)
    {
        // Directly calls other layers (❌ tightly coupled)
        ToastService.ShowAsync(...);
        JS.InvokeVoidAsync(...);
    }
}

✅ GOOD: Event-driven, loose coupling
public class NotificationService
{
    // Only raises events, doesn't care who listens
    public event Action<string>? OnPatientQueueUpdated;
    
    private void OnQueueTicketUpdated(string patientName)
    {
        OnPatientQueueUpdated?.Invoke(patientName);
    }
}

// ToastHost subscribes independently
SignalRService.OnPatientQueueUpdated += HandlePatientQueueUpdatedAsync;
```

### Pattern 3: Global Component Placement

```csharp
❌ BAD: ToastHost in each page
@page "/"
@layout MainLayout

<ToastHost />  // ❌ Recreated on each page, subscriptions lost

@code { }

❌ BAD: ToastHost in page @code
MyPatient.razor
@code {
    private async void HandleQueueUpdated()
    {
        await JS.InvokeVoidAsync(...);  // ❌ JS called in component
    }
}

✅ GOOD: ToastHost in global layout
DoctorLayout.razor
<ToastHost />  // ✅ Created once, persists forever

// ToastHost handles all pages automatically
```

### Pattern 4: Proper Lifecycle Management

```csharp
❌ BAD: Memory leaks
@code {
    protected override void OnInitialized()
    {
        // Subscribes
        Service.OnEvent += Handler;
        // ❌ Never unsubscribes = event handler stays in memory
    }
}

✅ GOOD: Proper cleanup
@code {
    protected override void OnInitialized()
    {
        Service.OnEvent += Handler;
    }
    
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        Service.OnEvent -= Handler;  // ✅ Unsubscribe
        await ValueTask.CompletedTask;
    }
}
```

---

## Troubleshooting Guide

### Issue 1: Toast Not Appearing on Index.razor

**Possible Causes**:

1. ❌ ToastHost not in layout
   ```razor
   <!-- DoctorLayout.razor -->
   <!-- Missing: <ToastHost /> -->
   ```
   **Fix**: Add `<ToastHost />` to `DoctorLayout.razor`

2. ❌ SignalR connection not started
   ```
   NotificationService.IsConnected = false
   ```
   **Fix**: Call `EnsureStartedAsync()` in Program.cs or ToastHost

3. ❌ Event not reaching ToastHost
   ```
   OnPatientQueueUpdated event fired but ToastHost doesn't respond
   ```
   **Fix**: Check subscription: `SignalRService.OnPatientQueueUpdated += ...`

4. ❌ JavaScript function missing
   ```
   window.appToasts is undefined
   ```
   **Fix**: Verify `notifications.js` is loaded in `App.razor`

---

### Issue 2: Duplicate Toasts

**Possible Causes**:

1. ❌ Multiple ToastHost subscriptions
   ```csharp
   OnInitialized() called multiple times
   → Event handler added multiple times
   → Same event triggers handler N times
   ```
   **Fix**: Check `if (firstRender)` in `OnAfterRenderAsync`

2. ❌ Multiple pages subscribing
   ```csharp
   // MyPatient.razor
   OnPatientQueueUpdated += Handler;
   
   // Index.razor (❌ also subscribes)
   OnPatientQueueUpdated += Handler;
   ```
   **Fix**: Only ToastHost should subscribe; pages don't need to

---

### Issue 3: Memory Leak

**Symptom**: Page slow over time, subscriptions accumulate

**Cause**: Subscribers not unsubscribing

```csharp
❌ BAD:
@code {
    protected override void OnInitialized()
    {
        Service.Event += Handler;  // Subscribes
        // Never unsubscribes
    }
}

✅ FIX:
@code {
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        Service.Event -= Handler;  // Unsubscribe
    }
}
```

---

### Issue 4: SignalR Connection Lost

**Symptom**: "Connection closed" in console, toasts stop appearing

**Cause**: Connection dropped, auto-reconnect may have failed

**Fix**:
```csharp
// NotificationService handles auto-reconnect
.WithAutomaticReconnect(new[]
{
    TimeSpan.Zero,
    TimeSpan.FromSeconds(2),
    TimeSpan.FromSeconds(5),
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(30)
})
```

If manual reconnection needed:
```csharp
if (!SignalRService.IsConnected)
{
    await SignalRService.EnsureStartedAsync();
}
```

---

## Summary & Best Practices Checklist

### Architecture Checklist

- [ ] **NotificationService**: Manages ONLY SignalR connection
- [ ] **ToastService**: Publishes ONLY events (no JS, no SignalR)
- [ ] **ToastHost**: Bridges ONLY events to JavaScript
- [ ] **Pages**: Focus on their content (no notification logic needed)

### Implementation Checklist

- [ ] ToastHost placed in DoctorLayout.razor
- [ ] NotificationService registered as Singleton
- [ ] ToastNotificationService registered as Scoped
- [ ] Global SignalR initialization in Program.cs (optional but recommended)
- [ ] ToastHost subscribes to `OnPatientQueueUpdated` event
- [ ] ToastHost implements `IAsyncDisposable` for cleanup
- [ ] JavaScript `appToasts.show()` function implemented

### Testing Checklist

- [ ] Toast appears on Index.razor (no code, no button click)
- [ ] Toast appears on MyPatient.razor
- [ ] Toast appears on other doctor pages
- [ ] No duplicate toasts
- [ ] No console errors
- [ ] Navigate between pages, toasts still work
- [ ] Close/reopen browser, connection re-establishes

### Performance Checklist

- [ ] Single HubConnection (no duplicates)
- [ ] Subscriptions properly cleaned up
- [ ] No memory leaks after 30+ navigations
- [ ] Toast latency < 100ms
- [ ] No blocking on UI thread

---

## Conclusion

This architecture achieves:

✅ **Global notifications** without per-page setup  
✅ **Clean separation** of concerns  
✅ **Event-driven** loose coupling  
✅ **Reliable** error handling  
✅ **Scalable** for future enhancements  
✅ **Maintainable** clear responsibilities  

The system elegantly bridges three layers:
- **Backend** (SignalR Hub)
- **Frontend C#** (Blazor Services & Components)
- **Frontend UI** (JavaScript & CSS)

Each layer has a single responsibility, making the system flexible, testable, and maintainable for production use.

---

**Document Version**: 1.0  
**Status**: Production Ready ✅  
**Last Updated**: 2024  
**Framework**: Blazor Server (.NET 8)
