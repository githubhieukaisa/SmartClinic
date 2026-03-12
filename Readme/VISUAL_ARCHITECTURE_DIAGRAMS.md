# PUBLISHER-SUBSCRIBER PATTERN FOR BLAZOR - VISUAL DIAGRAMS

## 🏗️ System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Blazor Application                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │            DoctorLayout.razor (Global)                  │  │
│  │  ┌─────────────────────────────────────────────────────┐│  │
│  │  │        <ToastHost /> - Event Listener              ││  │
│  │  │  ┌────────────────────────────────────────────────┐││  │
│  │  │  │ Subscribes to: ToastService.OnToast event     │││  │
│  │  │  │ Handler: HandleToastAsync()                   │││  │
│  │  │  │ Calls JS: JS.InvokeVoidAsync()               │││  │
│  │  │  └────────────────────────────────────────────────┘││  │
│  │  └─────────────────────────────────────────────────────┘│  │
│  │                       ↑                                  │  │
│  │                       │ @Body                            │  │
│  │                       │                                  │  │
│  │      ┌─────────────────────────────────┐              │  │
│  │      │  Individual Pages                │              │  │
│  │      │  ✓ Index.razor                  │              │  │
│  │      │  ✓ MyPatient.razor              │              │  │
│  │      │  ✓ Queue.razor                  │              │  │
│  │      │  (All get toast support!)       │              │  │
│  │      └─────────────────────────────────┘              │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │          ToastNotificationService (Event Publisher)     │  │
│  │  ┌──────────────────────────────────────────────────────┐  │
│  │  │  public event Func<string, string, Task>? OnToast   │  │
│  │  │  public async Task ShowToastAsync(message, type)    │  │
│  │  │  public async Task ShowSuccessAsync(message)        │  │
│  │  │  public async Task ShowErrorAsync(message)          │  │
│  │  │  public async Task ShowWarningAsync(message)        │  │
│  │  │  public async Task ShowInfoAsync(message)           │  │
│  │  └──────────────────────────────────────────────────────┘  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                                │
                                ↓
                        ┌──────────────────┐
                        │  JavaScript      │
                        │  showToast()     │
                        │  Creates DOM     │
                        │  Toast element   │
                        └──────────────────┘
                                │
                                ↓
                        ┌──────────────────┐
                        │  User sees       │
                        │  Toast! 🎉       │
                        └──────────────────┘
```

---

## 🔄 Event Flow Sequence Diagram

```
User/Service                ToastService           ToastHost              JS Runtime         DOM
    │                            │                    │                      │              │
    │  await ShowSuccess()        │                    │                      │              │
    ├───────────────────────────→ │                    │                      │              │
    │                             │                    │                      │              │
    │                     OnToast.Invoke()             │                      │              │
    │                             ├───────────────────→│                      │              │
    │                             │                    │                      │              │
    │                             │           HandleToastAsync()              │              │
    │                             │                    │                      │              │
    │                             │          JS.InvokeVoidAsync()             │              │
    │                             │                    ├─────────────────────→│              │
    │                             │                    │                      │              │
    │                             │                    │         showToast()  │              │
    │                             │                    │                      ├─────────────→│
    │                             │                    │                      │              │
    │                             │                    │                      │  Create      │
    │                             │                    │                      │  Element    │
    │                             │                    │                      │              │
    │                             │                    │                      │     Display │
    │                             │                    │                      │              │
    │                             │                    │                      │  Toast ✓   │
    │                             │                    │                      │  Visible   │
```

---

## ⚙️ Component Interaction Diagram

```
                      ┌─────────────────────────────────┐
                      │  DoctorLayout.razor             │
                      │  (Includes <ToastHost />)       │
                      └──────────────┬──────────────────┘
                                     │
                    ┌────────────────┼────────────────┐
                    ↓                ↓                ↓
            ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
            │ Index.razor  │ │MyPatient.razor│ │Other Pages   │
            │  (Dashboard) │ │(Patient Queue)│ │ (All Doctor) │
            └──────────────┘ └──────────────┘ └──────────────┘
                    │                ↓                │
                    └────────────────┼────────────────┘
                                     │
                        (All inject ToastService)
                                     │
                    ┌────────────────↓────────────────┐
                    │ ToastNotificationService        │
                    │ (Event Publisher)               │
                    │                                 │
                    │ - OnToast event                │
                    │ - ShowToastAsync()              │
                    │ - ShowSuccessAsync()            │
                    │ - ShowErrorAsync()              │
                    │ - ShowWarningAsync()            │
                    │ - ShowInfoAsync()               │
                    └────────────────┬────────────────┘
                                     │
                    (Event is raised - OnToast.Invoke())
                                     │
                    ┌────────────────↓────────────────┐
                    │ ToastHost.razor                 │
                    │ (Event Subscriber)              │
                    │                                 │
                    │ - Subscribes in OnInitialized   │
                    │ - Handler: HandleToastAsync()   │
                    │ - Unsubscribes in DisposeAsync()│
                    └────────────────┬────────────────┘
                                     │
                    (Calls JS safely in component)
                                     │
                    ┌────────────────↓────────────────┐
                    │ JavaScript Interop              │
                    │ JS.InvokeVoidAsync()            │
                    └────────────────┬────────────────┘
                                     │
                    ┌────────────────↓────────────────┐
                    │ window.appNotifications         │
                    │ .showToast(message, type)       │
                    └────────────────┬────────────────┘
                                     │
                    ┌────────────────↓────────────────┐
                    │ DOM                             │
                    │ Toast Element Created           │
                    │ ✓ Success / ✕ Error /⚠ Warning│
                    └─────────────────────────────────┘
```

---

## 🔀 Prerendering Phase vs Client Phase

```
PRERENDERING PHASE (Server-side)
╔════════════════════════════════════════════════════════╗
║  Service Execution:                                    ║
║  - ShowToastAsync() is called                         ║
║  - OnToast event is raised                            ║
║  ✅ NO JavaScript interop happens                      ║
║                                                        ║
║  ❌ JSRuntime would FAIL here!                         ║
║  ❌ JS.InvokeVoidAsync() would throw error            ║
║  ❌ Component lifecycle not running                    ║
╚════════════════════════════════════════════════════════╝
                         ↓
CLIENT PHASE (Browser)
╔════════════════════════════════════════════════════════╗
║  Component Lifecycle:                                  ║
║  - OnInitialized() called                             ║
║  - Subscribe to event                                 ║
║  - Handler receives event                             ║
║  - JS.InvokeVoidAsync() is called                     ║
║  ✅ JSRuntime is AVAILABLE!                           ║
║  ✅ Safe to call JavaScript                           ║
║  ✅ Toast appears in browser                          ║
╚════════════════════════════════════════════════════════╝
```

---

## 📊 Traditional vs Event-Based Approach

```
TRADITIONAL APPROACH (Broken)
═══════════════════════════════════════════

Prerendering Phase:
┌─────────────────────────────────┐
│ Service                         │
│ - ShowToast()                  │
│ - JS.InvokeVoidAsync()  ❌      │
│ JSRuntime not available!       │
└─────────────────────────────────┘
         ↓
ERROR: "JavaScript interop calls cannot be issued..."


EVENT-BASED APPROACH (Works)
════════════════════════════════════════════

Prerendering Phase:
┌─────────────────────────────────┐
│ Service                         │
│ - ShowToastAsync()              │
│ - OnToast.Invoke()  ✅          │
│ Just raises event, no JS        │
└─────────────────────────────────┘
         ↓
Client Phase:
┌─────────────────────────────────┐
│ Component (ToastHost)           │
│ - HandleToastAsync()            │
│ - JS.InvokeVoidAsync()  ✅      │
│ Safe! JSRuntime available       │
└─────────────────────────────────┘
         ↓
✅ Toast appears successfully
```

---

## 🎯 Usage Patterns

### Pattern 1: From Service
```
Service
  │
  ├─ Inject ToastNotificationService
  │
  ├─ await toast.ShowSuccessAsync()
  │
  ├─ Event raised
  │
  └─→ ToastHost handles it
       │
       └─→ JS called
            │
            └─→ Toast shown ✓
```

### Pattern 2: From Page
```
Page
  │
  ├─ @inject ToastNotificationService
  │
  ├─ Async method calls toast
  │
  ├─ Event raised
  │
  └─→ ToastHost handles it
       │
       └─→ JS called
            │
            └─→ Toast shown ✓
```

### Pattern 3: From SignalR
```
SignalR Hub
  │
  ├─ Inject ToastNotificationService
  │
  ├─ await toast.ShowInfoAsync()
  │
  ├─ Event raised (in hub context)
  │
  └─→ ToastHost on client handles it
       │
       └─→ JS called
            │
            └─→ Toast shown ✓
```

---

## 🔐 Thread Safety

```
Service (could be any thread)
  │
  ├─ Thread 1: await toast.ShowAsync()
  ├─ Thread 2: await toast.ShowAsync()
  ├─ Thread 3: await toast.ShowAsync()
  │
  └─→ Events raised (thread-safe in .NET)
       │
       ├─→ ToastHost (always main UI thread)
       │   │
       │   └─→ JS.InvokeVoidAsync() (UI thread safe)
       │
       └─→ Toast shown ✓
```

---

## 📈 Scalability

```
Single ToastNotificationService (Scoped)
  │
  ├─ Dashboard page injects it
  ├─ MyPatient page injects it
  ├─ Queue page injects it
  ├─ Any service injects it
  ├─ SignalR hub injects it
  │
  └─→ Single ToastHost component (in layout)
       │
       └─→ All calls funnel through this component
            │
            └─→ Efficient, centralized, scalable ✓
```

---

## 🎨 Toast Types

```
┌────────────────────────────────────────┐
│ ✓ Success (Green)                     │
│ await toast.ShowSuccessAsync()        │
└────────────────────────────────────────┘

┌────────────────────────────────────────┐
│ ✕ Error (Red)                         │
│ await toast.ShowErrorAsync()          │
└────────────────────────────────────────┘

┌────────────────────────────────────────┐
│ ⚠ Warning (Orange)                    │
│ await toast.ShowWarningAsync()        │
└────────────────────────────────────────┘

┌────────────────────────────────────────┐
│ ℹ Info (Blue)                         │
│ await toast.ShowInfoAsync()           │
└────────────────────────────────────────┘
```

---

## 🚀 Deployment

```
Development → Testing → Staging → Production
    │           │          │          │
    ✓ Works   ✓ Works    ✓ Works    ✓ Works
    locally   in tests   in staging  in prod
    
    No prerendering errors at any stage!
```

---

This is the **proper, enterprise-grade way** to implement toast notifications in Blazor Server! 🎯
