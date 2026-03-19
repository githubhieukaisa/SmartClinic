/**
 * Enhanced Toast Notification System - Centered Modal Toast
 * 
 * FIXES Applied:
 * ✓ Perfect centering (no transform conflicts)
 * ✓ Compact size (smaller text, padding)
 * ✓ Overlay cleanup (removes pointer-events when hidden)
 * ✓ Minimal logging (only important events)
 * ✓ Proper lifecycle management
 * 
 * Usage:
 *   window.appToasts.show(message, type, url?)
 *   window.appToasts.show("Patient added", "success")
 *   window.appToasts.show("Critical error!", "error")
 */

window.appToasts = {
    config: {
        duration: 4000,              // Auto-dismiss after 4 seconds
        toastContainerId: 'toast-container',
        overlayId: 'toast-overlay',
        useOverlay: true,
        enableLogging: false          // Set to true for debugging
    },

    /**
     * Show a centered modal toast notification with overlay
     */
    show: function(message, type = 'info', url = null) {
        this.log('show()', { message, type, url });

        // Create overlay if needed
        if (this.config.useOverlay) {
            this.showOverlay();
        }

        // Get or create container
        const container = this.getOrCreateContainer();

        // Create toast element
        const toast = document.createElement('div');
        toast.className = this.getTailwindClasses(type);

        // Create compact content
        const icon = this.getIcon(type);
        
        toast.innerHTML = `
            <div class="flex items-center gap-3 flex-1">
                <span class="text-2xl font-bold flex-shrink-0">${icon}</span>
                <span class="text-sm font-medium">${message}</span>
            </div>
            <button type="button" class="ml-3 text-xl flex-shrink-0 hover:opacity-70 transition-opacity" aria-label="Close notification">
                ×
            </button>
        `;

        this.log('Toast created');

        // Navigation handler
        if (url) {
            const messageArea = toast.querySelector('.flex-1');
            messageArea.style.cursor = 'pointer';
            messageArea.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.log('Toast navigating', { url });
                this.removeAllToasts();
                window.location.href = url;
            });
        }

        // Close button
        const closeBtn = toast.querySelector('button');
        closeBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.log('Close button clicked');
            this.removeToast(toast);
        });

        // Add to DOM
        container.appendChild(toast);
        this.log('Toast added to DOM');

        // Trigger animation
        void toast.offsetHeight; // Force reflow
        toast.classList.add('toast-animate-in');

        // Auto-dismiss with hover pause
        let timeoutId = null;
        let isPaused = false;

        const startTimer = () => {
            if (isPaused) return;
            if (timeoutId) clearTimeout(timeoutId);
            
            timeoutId = setTimeout(() => {
                this.removeToast(toast);
            }, this.config.duration);
        };

        toast.addEventListener('mouseenter', () => {
            if (timeoutId) clearTimeout(timeoutId);
            isPaused = true;
        });

        toast.addEventListener('mouseleave', () => {
            isPaused = false;
            startTimer();
        });

        startTimer();
        this.log('Toast ready');
    },

    /**
     * Remove individual toast
     */
    removeToast: function(toast) {
        if (!toast || !toast.parentElement) return;

        // Animate out
        toast.classList.remove('toast-animate-in');
        toast.classList.add('toast-animate-out');

        // Remove after animation
        setTimeout(() => {
            if (toast.parentElement) {
                toast.remove();
                this.log('Toast removed');
                
                // Check if container is empty
                const container = document.getElementById(this.config.toastContainerId);
                if (container && container.children.length === 0) {
                    this.hideOverlay();
                }
            }
        }, 300);
    },

    /**
     * Remove all toasts at once
     */
    removeAllToasts: function() {
        const container = document.getElementById(this.config.toastContainerId);
        if (!container) return;

        this.log('Removing all toasts');
        const toasts = Array.from(container.querySelectorAll('[class*="bg-"]'));
        
        toasts.forEach(toast => {
            toast.classList.remove('toast-animate-in');
            toast.classList.add('toast-animate-out');
        });

        setTimeout(() => {
            toasts.forEach(toast => {
                if (toast.parentElement) toast.remove();
            });
            this.hideOverlay();
        }, 300);
    },

    /**
     * Get or create the centered toast container
     */
    getOrCreateContainer: function() {
        let container = document.getElementById(this.config.toastContainerId);

        if (!container) {
            container = document.createElement('div');
            container.id = this.config.toastContainerId;

            // FIXED: Proper centering without transform conflicts
            // Using absolute positioning that doesn't conflict with animation transforms
            container.style.cssText = `
                position: fixed;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
                z-index: 9999;
                display: flex;
                flex-direction: column;
                gap: 12px;
                max-width: 500px;
                width: 90%;
                pointer-events: auto;
            `;

            container.setAttribute('role', 'region');
            container.setAttribute('aria-live', 'assertive');
            container.setAttribute('aria-label', 'Notifications');

            document.body.appendChild(container);
            this.log('Container created');
        }

        return container;
    },

    /**
     * Show the overlay backdrop - FIXED pointer-events handling
     */
    showOverlay: function() {
        let overlay = document.getElementById(this.config.overlayId);

        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = this.config.overlayId;

            // FIXED: Use CSS to manage pointer-events
            overlay.style.cssText = `
                position: fixed;
                top: 0;
                right: 0;
                bottom: 0;
                left: 0;
                z-index: 9998;
                background-color: rgba(0, 0, 0, 0.3);
                backdrop-filter: blur(4px);
                -webkit-backdrop-filter: blur(4px);
                opacity: 0;
                transition: opacity 0.3s ease-out;
                pointer-events: none;
            `;

            document.body.appendChild(overlay);
            this.log('Overlay created');
        }

        // Show overlay
        overlay.style.opacity = '1';
        overlay.style.pointerEvents = 'auto'; // Only when visible!
        this.log('Overlay shown');

        // Close all on overlay click (escape hatch)
        if (!overlay._clickHandlerSet) {
            overlay.addEventListener('click', () => {
                this.log('Overlay clicked, closing toasts');
                this.removeAllToasts();
            });
            overlay._clickHandlerSet = true;
        }

        return overlay;
    },

    /**
     * Hide the overlay - FIXED to actually disable pointer events
     */
    hideOverlay: function() {
        const overlay = document.getElementById(this.config.overlayId);
        if (!overlay) return;

        this.log('Hiding overlay');
        
        // Fade out
        overlay.style.opacity = '0';
        
        // Disable pointer events IMMEDIATELY (not after animation)
        // This is the KEY fix for blocking interactions!
        overlay.style.pointerEvents = 'none';

        this.log('Overlay hidden and pointer-events disabled');
    },

    /**
     * Get Tailwind CSS classes for compact modal toast
     */
    getTailwindClasses: function(type) {
        // FIXED: Compact sizing instead of large modal
        const baseClasses = [
            'bg-white',
            'border',           // Thinner border
            'shadow-lg',        // Still nice shadow
            'rounded-xl',       // Rounded corners
            'p-4',              // Compact padding (reduced from p-8)
            'flex',
            'items-center',
            'gap-3',
            'w-full',
            'transition-all',
            'duration-300',
            'pointer-events-auto'
        ];

        const typeStyles = {
            success: {
                border: 'border-green-300',
                bg: 'bg-green-50'
            },
            error: {
                border: 'border-red-300',
                bg: 'bg-red-50'
            },
            warning: {
                border: 'border-amber-300',
                bg: 'bg-amber-50'
            },
            info: {
                border: 'border-blue-300',
                bg: 'bg-blue-50'
            }
        };

        const style = typeStyles[type] || typeStyles.info;

        return [
            ...baseClasses,
            style.border,
            style.bg
        ].join(' ');
    },

    /**
     * Get icon with proper color
     */
    getIcon: function(type) {
        const icons = {
            success: { char: '✓', color: 'text-green-600' },
            error: { char: '✕', color: 'text-red-600' },
            warning: { char: '⚠', color: 'text-amber-600' },
            info: { char: 'ℹ', color: 'text-blue-600' }
        };

        const icon = icons[type] || icons.info;
        return `<span class="${icon.color}">${icon.char}</span>`;
    },

    /**
     * Logging utility - only when enabled
     */
    log: function(message, data) {
        if (!this.config.enableLogging) return;
        console.log(`[Toast] ${message}`, data || '');
    }
};

// ============================================================================
// ANIMATIONS - Injected into document head
// ============================================================================

function initializeToastAnimations() {
    const styleId = 'toast-animations-style';

    if (document.getElementById(styleId)) return;

    const style = document.createElement('style');
    style.id = styleId;
    style.textContent = `
        /* ====================================================================
           ANIMATIONS FOR CENTERED MODAL TOAST - FIXED
           ==================================================================== */
        
        /* Fade In + Scale Up (no transform conflict) */
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

        /* Fade Out + Scale Down */
        @keyframes toastFadeOutScale {
            from {
                opacity: 1;
                scale: 1;
            }
            to {
                opacity: 0;
                scale: 0.9;
            }
        }

        /* ====================================================================
           ANIMATION CLASSES
           ==================================================================== */
        
        .toast-animate-in {
            animation: toastFadeInScale 0.3s cubic-bezier(0.23, 1, 0.320, 1);
            animation-fill-mode: forwards;
        }

        .toast-animate-out {
            animation: toastFadeOutScale 0.3s ease-in;
            animation-fill-mode: forwards;
        }

        /* ====================================================================
           FALLBACK STYLES - CSS only (if Tailwind fails)
           ==================================================================== */
        
        #toast-container {
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            z-index: 9999;
            display: flex;
            flex-direction: column;
            gap: 12px;
            max-width: 500px;
            width: 90%;
            pointer-events: auto;
        }

        #toast-container > div {
            position: relative;
            background: white;
            border: 1px solid #cbd5e1;
            border-radius: 12px;
            padding: 16px;
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.1);
            display: flex;
            align-items: center;
            gap: 12px;
            font-size: 14px;
            transition: all 0.3s ease-out;
            pointer-events: auto;
        }

        #toast-container > div:hover {
            box-shadow: 0 15px 30px -5px rgba(0, 0, 0, 0.15);
        }

        /* Toast type colors */
        #toast-container > div.success {
            border-color: #86efac;
            background: #f0fdf4;
        }

        #toast-container > div.error {
            border-color: #fca5a5;
            background: #fef2f2;
        }

        #toast-container > div.warning {
            border-color: #fcd34d;
            background: #fffbeb;
        }

        #toast-container > div.info {
            border-color: #93c5fd;
            background: #f0f9ff;
        }

        #toast-container button {
            background: none;
            border: none;
            cursor: pointer;
            padding: 0;
            margin: 0;
            font-size: 20px;
        }

        #toast-overlay {
            position: fixed;
            top: 0;
            right: 0;
            bottom: 0;
            left: 0;
            z-index: 9998;
            background-color: rgba(0, 0, 0, 0.3);
            backdrop-filter: blur(4px);
            -webkit-backdrop-filter: blur(4px);
            opacity: 0;
            transition: opacity 0.3s ease-out;
            pointer-events: none;
        }

        /* Responsive */
        @media (max-width: 640px) {
            #toast-container {
                width: 95%;
            }

            #toast-container > div {
                padding: 12px;
                font-size: 13px;
                gap: 10px;
            }
        }
    `;

    document.head.appendChild(style);
}

// Initialize on document load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeToastAnimations);
} else {
    initializeToastAnimations();
}

// Final check
window.addEventListener('load', initializeToastAnimations);

console.log('[Toast] System ready');
