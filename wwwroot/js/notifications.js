/**
 * Application Notification System
 * Simple JavaScript toast notifications
 * Decoupled from Blazor component lifecycle
 */

window.appNotifications = {
    showToast: function(message, type = "info") {
        console.log("========== TOAST DEBUG ==========");
        console.log("Toast message:", message);
        console.log("Toast type:", type);
        console.log("Timestamp:", new Date().toISOString());
        console.log("=================================");

        // Create toast container if it doesn't exist
        let container = document.getElementById("toast-container");
        if (!container) {
            container = document.createElement("div");
            container.id = "toast-container";
            container.style.position = "fixed";
            container.style.top = "20px";
            container.style.left = "50%";
            container.style.transform = "translateX(-50%)";
            container.style.zIndex = "9999";
            container.style.display = "flex";
            container.style.flexDirection = "column";
            container.style.gap = "10px";
            container.style.maxWidth = "90%";
            document.body.appendChild(container);
            console.log("[JS] Toast container created");
        }

        // Create toast element
        const toast = document.createElement("div");
        toast.className = "app-toast " + type;
        
        // Get colors based on type
        let bgColor, textColor, borderColor, iconColor;
        switch(type) {
            case "success":
                bgColor = "#10b981";
                textColor = "#fff";
                borderColor = "#059669";
                iconColor = "✓";
                break;
            case "error":
                bgColor = "#ef4444";
                textColor = "#fff";
                borderColor = "#dc2626";
                iconColor = "✕";
                break;
            case "warning":
                bgColor = "#f59e0b";
                textColor = "#fff";
                borderColor = "#d97706";
                iconColor = "⚠";
                break;
            default: // info
                bgColor = "#3b82f6";
                textColor = "#fff";
                borderColor = "#2563eb";
                iconColor = "ℹ";
        }

        // Style the toast
        toast.style.position = "fixed";
        toast.style.top = "20px";
        toast.style.left = "50%";
        toast.style.transform = "translateX(-50%)";
        toast.style.background = bgColor;
        toast.style.color = textColor;
        toast.style.padding = "12px 20px";
        toast.style.borderRadius = "8px";
        toast.style.zIndex = "9999";
        toast.style.boxShadow = "0 4px 10px rgba(0,0,0,0.2)";
        toast.style.border = "2px solid " + borderColor;
        toast.style.display = "flex";
        toast.style.alignItems = "center";
        toast.style.gap = "8px";
        toast.style.minWidth = "300px";
        toast.style.maxWidth = "500px";
        toast.style.animation = "slideDown 0.3s ease-out";
        toast.style.fontWeight = "500";
        toast.style.fontSize = "14px";

        // Add icon
        const icon = document.createElement("span");
        icon.textContent = iconColor;
        icon.style.fontSize = "18px";
        icon.style.flexShrink = "0";
        toast.appendChild(icon);

        // Add message
        const messageSpan = document.createElement("span");
        messageSpan.textContent = message;
        toast.appendChild(messageSpan);

        // Add close button
        const closeBtn = document.createElement("button");
        closeBtn.textContent = "×";
        closeBtn.style.background = "none";
        closeBtn.style.border = "none";
        closeBtn.style.color = textColor;
        closeBtn.style.fontSize = "24px";
        closeBtn.style.cursor = "pointer";
        closeBtn.style.padding = "0";
        closeBtn.style.marginLeft = "auto";
        closeBtn.style.opacity = "0.8";
        closeBtn.style.lineHeight = "1";
        closeBtn.onclick = function() {
            toast.style.animation = "slideUp 0.3s ease-out";
            setTimeout(() => toast.remove(), 300);
            console.log("[JS] Toast closed by user");
        };
        toast.appendChild(closeBtn);

        // Add to document
        document.body.appendChild(toast);
        console.log("[JS] Toast added to DOM");

        // Auto-remove after 4 seconds
        const timeoutId = setTimeout(() => {
            if (toast.parentElement) {
                toast.style.animation = "slideUp 0.3s ease-out";
                setTimeout(() => {
                    if (toast.parentElement) {
                        toast.remove();
                    }
                }, 300);
                console.log("[JS] Toast auto-removed after 4 seconds");
            }
        }, 4000);

        // Store timeout ID for cleanup
        toast.timeoutId = timeoutId;
    }
};

// Add CSS animations if not already present
if (!document.getElementById("toast-animations")) {
    const style = document.createElement("style");
    style.id = "toast-animations";
    style.textContent = `
        @keyframes slideDown {
            from {
                transform: translateX(-50%) translateY(-20px);
                opacity: 0;
            }
            to {
                transform: translateX(-50%) translateY(0);
                opacity: 1;
            }
        }
        
        @keyframes slideUp {
            from {
                transform: translateX(-50%) translateY(0);
                opacity: 1;
            }
            to {
                transform: translateX(-50%) translateY(-20px);
                opacity: 0;
            }
        }
    `;
    document.head.appendChild(style);
    console.log("[JS] Toast animations added");
}

console.log("[JS] Notification system loaded");
