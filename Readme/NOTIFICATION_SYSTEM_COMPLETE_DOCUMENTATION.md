# 📚 Blazor Hospital Management System - Toast Notification System Documentation

## Table of Contents

1. [System Overview](#system-overview)
2. [Architecture Diagram](#architecture-diagram)
3. [Core Components](#core-components)
4. [Service Responsibilities](#service-responsibilities)
5. [Lifecycle and Data Flow](#lifecycle-and-data-flow)
6. [Component Interaction](#component-interaction)
7. [Toast Types and Styling](#toast-types-and-styling)
8. [Best Practices](#best-practices)
9. [Integration Examples](#integration-examples)
10. [Troubleshooting Guide](#troubleshooting-guide)

---

## System Overview

The notification system is a **multi-layer architecture** that combines:

- **Backend**: ASP.NET Core services (C#)
- **Frontend**: Blazor components (C#/Razor)
- **UI Layer**: JavaScript toast system with DOM manipulation
- **Real-time**: SignalR for server-to-client communication
- **State Management**: Singleton service acting as a state store

### Key Design Principles

1. **Separation of Concerns**: Each layer has a single responsibility
2. **State Store Pattern**: Singleton service maintains notification state
3. **Event-Driven**: Services raise events, components listen and respond
4. **Non-blocking**: JavaScript toast system doesn't block UI interactions
5. **Thread-Safe**: Uses `ConcurrentQueue` for background thread safety
6. **Resilient**: Works even if components are recreated or disposed

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Blazor Application                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ DoctorLayout.razor (Global Layout)                          │  │
│  │                                                              │  │
│  │  ┌─────────────────────────────────────────────────────┐   │  │
│  │  │ ToastHost.razor (Toast Container Component)        │   │  │
│  │  │                                                     │   │  │
│  │  │ - Reads from GlobalNotificationService state      │   │  │
│  │  │ - Subscribes to notification events               │   │  │
│  │  │ - Calls JS.InvokeVoidAsync()                      │   │  │
│  │  │ - Manages toast lifecycle                         │   │  │
│  │  └──────────────────┬──────────────────────────────────┘   │  │
│  │                     │                                       │  │
│  │                     ▼                                       │  │
│  │  ┌──────────────────────────────────────────────┐          │  │
│  │  │ JavaScript Toast System (notifications.js)  │          │  │
│  │  │                                              │          │  │
│  │  │ ├─ window.appToasts.show()                  │          │  │
│  │  │ ├─ Toast container management               │          │  │
│  │  │ ├─ Overlay backdrop                         │          │  │
│  │  │ ├─ CSS animations                           │          │  │
│  │  │ └─ User interactions (click, hover)         │          │  │
│  │  └──────────────────────────────────────────────┘          │  │
│  │                                                              │  │
│  │  ┌─────────────────────────────────────────────────────┐   │  │
│  │  │ Individual Doctor Pages                           │   │  │
│  │  │ (Index.razor, MyPatient.razor, etc.)             │   │  │
│  │  │                                                     │   │  │
│  │  │ - Inject GlobalNotificationService                │   │  │
│  │  │ - Inject ToastNotificationService                 │   │  │
│  │  │ - Call service methods to show toasts             │   │  │
│  │  └─────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ DI Container (Program.cs)                                    │  │
│  │                                                              │  │
│  │ ├─ GlobalNotificationService (Singleton)                    │  │
│  │ ├─ ToastNotificationService (Scoped)                        │  │
│  │ ├─ NotificationService (Scoped)                            │  │
│  │ └─ PatientService (Scoped)                                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │
                              │ SignalR
                              │
┌─────────────────────────────────────────────────────────────────────┐
│                     ASP.NET Core Backend                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ PatientHub.cs (SignalR Hub)                                 │  │
│  │                                                              │  │
│  │ - Receives client connections                              │  │
│  │ - Broadcasts events to connected clients                   │  │
│  │ - Examples:                                                 │  │
│  │   • QueueTicketCreated                                     │  │
│  │   • PatientStatusChanged                                   │  │
│  │   • ExaminationCompleted                                   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Business Logic Services                                     │  │
│  │                                                              │  │
│  │ - PatientService                                           │  │
│  │ - ExaminationService                                       │  │
│  │ - PrescriptionService                                      │  │
│  │ - etc.                                                      │  │
│  │                                                              │  │
│  │ These services:                                             │  │
│  │ ├─ Inject GlobalNotificationService                        │  │
│  │ ├─ Call .Show() to add notifications                       │  │
│  │ └─ Notifications added to state store                      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### File Structure

```
SmartClinic/
├── Services/
│   ├── GlobalNotificationService.cs          # Singleton state store
│   ├── ToastNotificationService.cs           # Event publisher
│   ├── NotificationService.cs                # SignalR client
│   └── PatientService.cs                     # Business logic
│
├── Web/
│   ├── Components/
│   │   └── ToastHost.razor                   # Toast container component
│   ├── Layout/
│   │   └── DoctorLayout.razor                # Main layout with <ToastHost />
│   ├── Pages/
│   │   └── Doctor/
│   │       ├── Index.razor                   # Dashboard
│   │       ├── MyPatient.razor               # Patient queue
│   │       └── ...
│   └── App.razor                             # App root
│
├── Hubs/
│   └── PatientHub.cs                         # SignalR hub
│
├── wwwroot/
│   └── js/
│       └── notifications.js                  # JavaScript toast system
│
└── Program.cs                                 # DI configuration
```

---

## Service Responsibilities

### 1. GlobalNotificationService (Singleton State Store)

**Purpose**: Central state management for all notifications in the application.

**Key Characteristics**:
- **Lifetime**: Singleton (one instance for entire app lifetime)
- **Thread-Safe**: Uses `ConcurrentQueue<Notification>`
- **Stateful**: Maintains list of active notifications
- **Accessible**: Injected into components and services

**Responsibilities**:

```csharp
public class GlobalNotificationService
{
    // PUBLIC API
    public void Show(string message, string type = "info")
    // → Adds notification to internal queue
    // → Fires OnNotificationAdded event
    // → Thread-safe, can be called from background threads
    
    public IReadOnlyList<Notification> GetNotifications()
    // → Returns current list of active notifications
    // → Components read directly from state (not relying on events)
    
    public void Remove(string notificationId)
    // → Removes specific notification by ID
    // → Fires OnNotificationRemoved event
    
    public void ClearAll()
    // → Removes all active notifications
    
    public int NotificationCount
    // → Property: current count of notifications
    
    // EVENTS (Optional - components can subscribe)
    public event Action OnNotificationAdded
    public event Action OnNotificationRemoved
}
```

**Data Model**:

```csharp
public class Notification
{
    public string Id { get; set; }           // Unique identifier
    public string Message { get; set; }      // Notification text
    public string Type { get; set; }         // 'info', 'success', 'warning', 'error'
    public DateTime CreatedAt { get; set; }  // When created
}
```

**Why Singleton?**

- **Persistence**: Notifications persist across component lifecycle
- **Consistency**: Single source of truth for notification state
- **Performance**: No repeated instantiation
- **Reliability**: Works even if components are recreated or disposed

---

### 2. ToastNotificationService (Event Publisher)

**Purpose**: Provides high-level API for showing toast notifications with optional URL navigation.

**Key Characteristics**:
- **Lifetime**: Scoped (new instance per request/page)
- **Event-Driven**: Raises `OnToast` event when toast should be shown
- **JS-Independent**: Service doesn't call JavaScript directly
- **URL Support**: Toasts can navigate to a URL when clicked

**Responsibilities**:

```csharp
public class ToastNotificationService
{
    // PUBLIC API
    public async Task ShowToastAsync(
        string message, 
        string type = "info", 
        string? url = null)
    // → Raises OnToast event with message, type, and optional URL
    // → No JS call here - deferred to component
    
    public async Task ShowSuccessAsync(string message, string? url = null)
    public async Task ShowErrorAsync(string message, string? url = null)
    public async Task ShowWarningAsync(string message, string? url = null)
    public async Task ShowInfoAsync(string message, string? url = null)
    // → Convenience methods with predefined types
    
    // EVENT
    public event Func<string, string, string?, Task>? OnToast
    // → Raised when toast should be shown
    // → Signature: (message, type, url) => Task
    // → Handled by ToastHost.razor component
}
```

**Why Separate from GlobalNotificationService?**

- **Concerns**: ToastNotificationService is UI-focused (events, JS)
- **Flexibility**: Can have different notification types (toasts, alerts, etc.)
- **Decoupling**: Services can use GlobalNotificationService, UI uses ToastNotificationService
- **Lifecycle**: Scoped lifetime is appropriate for component-level notifications

---

### 3. NotificationService (SignalR Client)

**Purpose**: Manages SignalR connection and listens for real-time events from backend.

**Key Characteristics**:
- **Lifetime**: Scoped
- **Connection**: Establishes SignalR HubConnection
- **Event Listeners**: Sets up handlers for backend events
- **Resilient**: Handles connection failures gracefully

**Responsibilities**:

```csharp
public class NotificationService
{
    public HubConnection? _hubConnection { get; set; }
    
    // PUBLIC API
    public async Task EnsureStartedAsync()
    // → Ensures connection is established
    // → Creates HubConnection if needed
    // → Starts connection if not already started
    
    public bool IsConnected
    // → Property: whether connection is active
    
    // USAGE (in components)
    _hubConnection?.On("QueueTicketCreated", async (string patientName) =>
    {
        // Handle incoming event from backend
        await toastService.ShowSuccessAsync($"✓ {patientName} added");
    });
}
```

**Connection Lifecycle**:

```
1. Component initializes
   ↓
2. NotificationService.EnsureStartedAsync() called
   ↓
3. HubConnection created (if not exists)
   ↓
4. Event listeners registered (On<EventName>)
   ↓
5. Connection started
   ↓
6. Component receives real-time events from backend
```

---

### 4. PatientService (Business Logic)

**Purpose**: Handles patient-related operations and demonstrates integration with notification system.

**Example Integration**:

```csharp
public class PatientService
{
    private readonly GlobalNotificationService _notificationService;
    private readonly IHubContext<PatientHub> _hubContext;
    
    public async Task AddQueueTicketAsync(...)
    {
        // 1. Perform business logic
        var ticket = new QueueTicket { ... };
        await _dbContext.QueueTickets.AddAsync(ticket);
        await _dbContext.SaveChangesAsync();
        
        // 2. Add notification to state store
        _notificationService.Show(
            $"Patient {ticket.PatientName} added to queue",
            "success"
        );
        
        // 3. Broadcast to connected clients via SignalR
        await _hubContext.Clients
            .User(ticket.DoctorId.ToString())
            .SendAsync("QueueTicketCreated", ticket.PatientName);
    }
}
```

---

## Lifecycle and Data Flow

### Scenario: Doctor Adds Patient to Queue

```
┌─ BACKEND (C#) ─────────────────────────────────────────────────────┐
│                                                                     │
│ 1. User clicks "Add Patient" button                                │
│    ↓                                                                │
│ 2. Index.razor calls: await PatientService.AddQueueTicketAsync()   │
│    ↓                                                                │
│ 3. PatientService:                                                 │
│    ├─ Creates QueueTicket in database                              │
│    ├─ Calls: GlobalNotificationService.Show(message, type)         │
│    │  └─→ Adds to _notifications queue (state store)               │
│    │  └─→ Fires OnNotificationAdded event                          │
│    └─ Calls: _hubContext.Clients.SendAsync("QueueTicketCreated") │
│       └─→ Sends SignalR message to connected client               │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
                                ▲
                                │ SignalR WebSocket
                                │
┌─ FRONTEND (Blazor + JS) ───────────────────────────────────────────┐
│                                                                     │
│ 4. NotificationService receives SignalR event "QueueTicketCreated"│
│    ↓                                                                │
│ 5. Event listener executes:                                        │
│    ├─ connection.On("QueueTicketCreated", async (name) =>         │
│    │  {                                                             │
│    │    await ToastService.ShowSuccessAsync(message);             │
│    │    └─→ Raises OnToast event                                   │
│    │  }                                                             │
│    └─ Component sees notification in GlobalNotificationService    │
│                                                                     │
│ 6. ToastHost.razor (listening to OnToast event):                  │
│    ├─ Event handler fires                                          │
│    ├─ Calls: JS.InvokeVoidAsync("appToasts.show", msg, type, url)│
│    └─→ Passes to JavaScript                                       │
│                                                                     │
│ 7. JavaScript (notifications.js):                                  │
│    ├─ window.appToasts.show(message, type, url) executes         │
│    ├─ Creates overlay element                                      │
│    ├─ Creates toast element                                        │
│    ├─ Adds CSS classes for styling                                 │
│    ├─ Applies animations (fade + scale)                            │
│    ├─ Sets auto-dismiss timer (4000ms)                            │
│    ├─ Binds click handlers (navigation, close)                     │
│    └─→ Toast appears on screen!                                   │
│                                                                     │
│ 8. User interaction:                                               │
│    ├─ Hover: auto-dismiss timer pauses                             │
│    ├─ Leave: auto-dismiss timer resumes                            │
│    ├─ Click message: navigates to URL                              │
│    ├─ Click ×: removes toast immediately                           │
│    └─ After 4 seconds: toast fades out and is removed             │
│                                                                     │
│ 9. JavaScript removes DOM elements:                                │
│    ├─ Toast element removed                                        │
│    ├─ If no more toasts: overlay removed                           │
│    └─ overlay.style.pointerEvents = 'none' (allows clicks behind) │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

### Timeline Diagram

```
Time:  0ms         500ms        1000ms        2000ms        4000ms
       │           │            │             │             │
       ▼           ▼            ▼             ▼             ▼
       │───────────│────────────│─────────────│─────────────│
       │                                                     │
User   ├─ Clicks Add Patient
Calls  │
       ▼
Blazor ├─ Backend processes request
       │  └─→ GlobalNotificationService.Show() called
       │
       ├─ SignalR broadcasts event
       │
       ▼
JS     ├─ Receives QueueTicketCreated event
       │
       ├─ ToastService.ShowSuccessAsync() → OnToast event
       │
       ├─ ToastHost handler → JS.InvokeVoidAsync()
       │
       ├─ appToasts.show() executes
       │  └─→ Creates and displays toast
       │
Toast  │  
       ├─────────────────────────────────────┐
       │ Toast visible on screen             │
       │ (fade + scale animation 0-400ms)    │
       │                                      │
       │ Auto-dismiss timer: 4000ms          │
       │ (User can hover to pause)           │
       │                                      │
       │ After 4000ms:                        │
       │ (fade out + scale down 300ms)       │
       │                                      │
       └──────────────────────────────────────┘
                                              
       ▼
Done   Toast removed from DOM
       Overlay pointer-events disabled
       State cleaned up
```

---

## Component Interaction

### ToastHost.razor - The Bridge Component

**Location**: `Web/Components/ToastHost.razor`

**Purpose**: Bridges Blazor (C#) and JavaScript (JS) for toast notifications.

**Key Methods**:

```csharp
@code {
    // INITIALIZATION
    protected override void OnInitialized()
    {
        // Subscribe to ToastNotificationService events
        ToastService.OnToast += HandleToastAsync;
    }
    
    // SIGNALR SETUP
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Ensure SignalR connection is established
            if (!SignalRService.IsConnected)
            {
                await SignalRService.EnsureStartedAsync();
            }
            
            // Register event listeners for specific notifications
            var connection = SignalRService._hubConnection;
            
            connection?.On("QueueTicketCreated", async (string patientName) =>
            {
                await ToastService.ShowSuccessAsync(
                    $"✓ {patientName} added to queue",
                    "/doctor/my-patients"  // Optional: click to navigate
                );
            });
            
            // More event listeners...
        }
    }
    
    // EVENT HANDLER
    private async Task HandleToastAsync(
        string message, 
        string type, 
        string? url)
    {
        // Called when ToastService.ShowToastAsync() is invoked
        // This is where JS.InvokeVoidAsync is safe to call
        // (Component is on client, not prerendering)
        
        await JS.InvokeVoidAsync("appToasts.show", message, type, url);
    }
    
    // CLEANUP
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Unsubscribe from event to prevent memory leaks
        ToastService.OnToast -= HandleToastAsync;
        await ValueTask.CompletedTask;
    }
}
```

**Why This Component is Essential**:

1. **JS Interop Safety**: Only calls JS in component lifecycle (not prerendering)
2. **Event Bridge**: Connects event-based service to JS function
3. **SignalR Integration**: Sets up real-time event listeners
4. **Global Scope**: Placed in `DoctorLayout.razor` so all pages have access
5. **Lifecycle Management**: Subscribes and unsubscribes properly

---

### How Pages Use the System

#### Example: Index.razor (Dashboard)

```razor
@page "/"
@inject GlobalNotificationService GlobalNotification
@inject ToastNotificationService Toast

<button @onclick="AddPatient">Add Patient</button>

@code {
    private async Task AddPatient()
    {
        try
        {
            // Option 1: Call PatientService (which handles notifications internally)
            await PatientService.AddQueueTicketAsync(...);
            // PatientService will:
            // 1. Add notification to GlobalNotificationService
            // 2. Broadcast via SignalR
            // 3. ToastHost will display it
            
            // Option 2: Manually show toast
            await Toast.ShowSuccessAsync("Patient added!", "/doctor/my-patients");
            // ToastHost will:
            // 1. Receive OnToast event
            // 2. Call JS
            // 3. JS displays toast
        }
        catch (Exception ex)
        {
            await Toast.ShowErrorAsync($"Error: {ex.Message}");
        }
    }
}
```

---

## Toast Types and Styling

### Available Toast Types

```
┌─────────────────────────────────────────────────────────┐
│ SUCCESS (Green)                                         │
│ ✓ Patient John Smith added to queue              × │
│ border: green-300, bg: green-50, icon: ✓ green-600   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ ERROR (Red)                                             │
│ ✕ Failed to save examination data                 × │
│ border: red-300, bg: red-50, icon: ✕ red-600         │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ WARNING (Orange)                                        │
│ ⚠ Patient's vital signs are abnormal             × │
│ border: amber-300, bg: amber-50, icon: ⚠ amber-600   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ INFO (Blue)                                             │
│ ℹ Doctor is currently examining patient          × │
│ border: blue-300, bg: blue-50, icon: ℹ blue-600      │
└─────────────────────────────────────────────────────────┘
```

### CSS Classes Applied

**TailwindCSS Classes**:

```css
/* Base classes (all toasts) */
bg-white              /* White background */
border                /* 1px border */
shadow-lg             /* Large shadow */
rounded-xl            /* Rounded corners */
p-4                   /* Padding: 16px */
flex                  /* Flexbox layout */
items-center          /* Center items vertically */
gap-3                 /* Gap between items */
w-full                /* Full width */
transition-all        /* Smooth transitions */
duration-300          /* 300ms animation */
pointer-events-auto   /* Clickable */

/* Type-specific colors */
/* Success */
border-green-300      /* Green border */
bg-green-50           /* Light green background */

/* Error */
border-red-300
bg-red-50

/* Warning */
border-amber-300
bg-amber-50

/* Info */
border-blue-300
bg-blue-50
```

### Animations

```css
/* Fade In + Scale Up */
@keyframes toastFadeInScale {
    from {
        opacity: 0;
        scale: 0.9;
    }
    to {
        opacity: 1;
        scale: 1;
    }
}

/* Applied when toast appears */
.toast-animate-in {
    animation: toastFadeInScale 0.3s cubic-bezier(0.23, 1, 0.320, 1);
}

/* Applied when toast disappears */
.toast-animate-out {
    animation: toastFadeOutScale 0.3s ease-in;
}
```

---

## Best Practices

### 1. Use GlobalNotificationService for Background Operations

**For backend services** that add notifications (thread-safe):

```csharp
// In PatientService, ExaminationService, etc.
private readonly GlobalNotificationService _notificationService;

public async Task SomeOperationAsync()
{
    // Do work...
    
    // Add notification to state store
    _notificationService.Show("Operation completed", "success");
    // This is thread-safe and works from any context
}
```

### 2. Use ToastNotificationService for User-Initiated Actions

**For Blazor pages/components** that respond to user actions:

```razor
@inject ToastNotificationService Toast

<button @onclick="HandleClick">Click Me</button>

@code {
    private async Task HandleClick()
    {
        try
        {
            // Perform action...
            await Toast.ShowSuccessAsync("Done!", "/some-path");
        }
        catch (Exception ex)
        {
            await Toast.ShowErrorAsync($"Error: {ex.Message}");
        }
    }
}
```

### 3. Handle SignalR Events in ToastHost.razor

**Register event listeners in `OnAfterRenderAsync`**:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await SignalRService.EnsureStartedAsync();
        
        var connection = SignalRService._hubConnection;
        
        // Event from backend
        connection?.On("PatientAdmitted", async (int patientId, string name) =>
        {
            // Show toast automatically
            await ToastService.ShowInfoAsync(
                $"{name} has been admitted"
            );
        });
    }
}
```

### 4. Read from State Store, Not Just Events

**Components should call GetNotifications() directly**:

```csharp
// Instead of relying only on events:
public List<Notification> CurrentNotifications 
    => _globalNotificationService.GetNotifications().ToList();

// In render:
@foreach (var notif in CurrentNotifications)
{
    <NotificationItem Notification="notif" />
}
```

This ensures notifications aren't lost even if component is recreated.

### 5. Disable Logging in Production

**In notifications.js**:

```javascript
window.appToasts = {
    config: {
        enableLogging: false  // Set to true only for debugging
    }
}
```

### 6. Use Type-Specific Convenience Methods

**Instead of**:
```csharp
await Toast.ShowToastAsync("Success!", "success");
```

**Use**:
```csharp
await Toast.ShowSuccessAsync("Success!");  // Clearer intent
```

---

## Integration Examples

### Example 1: Show Toast on Button Click

```razor
@page "/example"
@inject ToastNotificationService Toast

<button @onclick="ShowNotification">Click Me</button>

@code {
    private async Task ShowNotification()
    {
        await Toast.ShowSuccessAsync("You clicked the button!", "/doctor/my-patients");
    }
}
```

### Example 2: Real-Time Notifications via SignalR

```csharp
// In PatientHub.cs
public async Task NotifyNewPatient(int doctorId, string patientName)
{
    await Clients.User(doctorId.ToString())
        .SendAsync("QueueTicketCreated", patientName);
}

// In ToastHost.razor
connection?.On("QueueTicketCreated", async (string name) =>
{
    await ToastService.ShowSuccessAsync(
        $"✓ {name} added to queue",
        "/doctor/my-patients"
    );
});
```

### Example 3: Notification from Business Logic Service

```csharp
public class ExaminationService
{
    private readonly GlobalNotificationService _notificationService;
    
    public async Task CompleteExaminationAsync(int ticketId)
    {
        // Do work...
        var result = await _dbContext.SaveChangesAsync();
        
        if (result > 0)
        {
            // Add to global state
            _notificationService.Show(
                "Examination completed successfully",
                "success"
            );
        }
    }
}
```

### Example 4: Error Handling with Toasts

```razor
@inject ToastNotificationService Toast
@inject PatientService PatientService

<button @onclick="SaveChanges">Save</button>

@code {
    private async Task SaveChanges()
    {
        try
        {
            var result = await PatientService.UpdatePatientAsync(...);
            
            if (result.Success)
            {
                await Toast.ShowSuccessAsync("Patient updated!");
            }
            else
            {
                await Toast.ShowErrorAsync($"Error: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            await Toast.ShowErrorAsync($"Unexpected error: {ex.Message}");
        }
    }
}
```

---

## Troubleshooting Guide

### Problem: Toast Doesn't Appear

**Check list**:

1. Is `<ToastHost />` included in `DoctorLayout.razor`?
   ```razor
   <ToastHost />  <!-- Must be present -->
   ```

2. Is `notifications.js` loaded in `App.razor`?
   ```html
   <script src="js/notifications.js"></script>
   ```

3. Are services registered in `Program.cs`?
   ```csharp
   builder.Services.AddScoped<ToastNotificationService>();
   builder.Services.AddSingleton<GlobalNotificationService>();
   ```

4. Enable logging to debug:
   ```javascript
   // In notifications.js
   window.appToasts.config.enableLogging = true;
   ```

5. Check browser console (F12) for JavaScript errors

### Problem: Overlay Blocks Clicks

**Cause**: `pointer-events` not disabled on hidden overlay

**Fix**: Already handled in refactored version:
```javascript
hideOverlay: function() {
    overlay.style.opacity = '0';
    overlay.style.pointerEvents = 'none';  // Critical!
}
```

### Problem: Toast Text Overflows on Mobile

**Already handled** with media query in CSS:
```css
@media (max-width: 640px) {
    #toast-container {
        width: 95%;
    }
    
    #toast-container > div {
        padding: 12px;
        font-size: 13px;
    }
}
```

### Problem: Excessive Console Logs

**Solution**: Disable logging in `notifications.js`:
```javascript
window.appToasts.config.enableLogging = false;
```

### Problem: Toast Doesn't Auto-Dismiss

**Check**: Has duration passed?
```javascript
config: {
    duration: 4000  // 4 seconds
}
```

**Check**: Is hover pausing it?
- Toast pauses on hover
- Resume on mouse leave
- This is intentional behavior

---

## Summary

The notification system is a **well-architected, multi-layer solution** that:

1. **Separates concerns**: Services, components, and JavaScript each have specific roles
2. **Maintains state**: GlobalNotificationService acts as the source of truth
3. **Enables real-time**: SignalR integration for server-to-client communication
4. **Bridges technologies**: JavaScript toast system with Blazor components
5. **Handles lifecycle**: Proper subscription/unsubscription and cleanup
6. **Works reliably**: Thread-safe, handles edge cases, gracefully degrades

### Key Takeaways

- **GlobalNotificationService**: State store (Singleton)
- **ToastNotificationService**: Event publisher (Scoped)
- **ToastHost.razor**: Event subscriber & JS bridge (Component)
- **notifications.js**: UI rendering & user interaction (JavaScript)
- **SignalR**: Real-time backend-to-client communication

This architecture ensures **scalability, maintainability, and reliability** across your HMS application.

---

## Additional Resources

- [TailwindCSS Documentation](https://tailwindcss.com)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [JavaScript Interop](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability)

---

**Document Version**: 1.0  
**Last Updated**: 2024  
**System**: SmartClinic Hospital Management System  
**Status**: Production Ready ✅
