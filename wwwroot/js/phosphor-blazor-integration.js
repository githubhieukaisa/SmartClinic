// Phosphor Icons + Blazor Integration
// This script ensures Phosphor Icons are properly rendered after Blazor navigation

(function() {
    'use strict';

    let phosphorObserver = null;

    // Initialize Phosphor Icons observer after DOM is ready
    window.initPhosphorIconsObserver = function() {
        // Stop existing observer if any
        if (phosphorObserver) {
            phosphorObserver.disconnect();
        }

        // Create a MutationObserver to detect when new icons are added to the DOM
        const observerConfig = {
            childList: true,      // Watch for added/removed nodes
            subtree: true,        // Watch all descendants
            attributes: false,
            characterData: false
        };

        phosphorObserver = new MutationObserver(function(mutations) {
            // Check if any new <i> tags with Phosphor classes were added
            let hasNewIcons = false;

            for (let mutation of mutations) {
                if (mutation.type === 'childList') {
                    // Check added nodes
                    mutation.addedNodes.forEach(node => {
                        if (node.nodeType === 1) { // Element node
                            // Check if this is an icon or contains icons
                            if (node.classList && node.classList.contains('ph-bold')) {
                                hasNewIcons = true;
                            }
                            // Check descendants
                            if (node.querySelectorAll && node.querySelectorAll('[class*="ph-"]').length > 0) {
                                hasNewIcons = true;
                            }
                        }
                    });
                }
            }

            // If new icons detected, trigger Phosphor re-initialization
            if (hasNewIcons) {
                window.reinitPhosphorIcons();
            }
        });

        // Start observing the main content area
        const mainElement = document.querySelector('main') || document.body;
        phosphorObserver.observe(mainElement, observerConfig);
    };

    // Reinitialize Phosphor Icons - called after Blazor navigation
    window.reinitPhosphorIcons = function() {
        try {
            // Method 1: Try to reload the Phosphor script
            const phosphorScript = document.querySelector('script[src*="phosphor-icons"]');
            if (phosphorScript && phosphorScript.src) {
                // Create a new script element to reload Phosphor
                const newScript = document.createElement('script');
                newScript.src = phosphorScript.src;
                newScript.async = true;
                document.head.appendChild(newScript);
                
                // Clean up after a short delay
                setTimeout(() => {
                    if (newScript.parentNode) {
                        newScript.parentNode.removeChild(newScript);
                    }
                }, 100);
                return;
            }

            // Method 2: Try CSS-based re-rendering
            // Phosphor might work with CSS animations/transitions
            const iconElements = document.querySelectorAll('[class*="ph-"]');
            iconElements.forEach(el => {
                // Trigger reflow by changing and resetting display
                const originalDisplay = el.style.display || '';
                el.style.display = 'none';
                // Force reflow
                void el.offsetHeight;
                el.style.display = originalDisplay;
            });

            console.log('Phosphor Icons reinitialized for', iconElements.length, 'elements');
        } catch (error) {
            console.warn('Error reinitializing Phosphor Icons:', error);
        }
    };

    // Called by Blazor component after rendering
    window.phosphorIconsReinit = function() {
        window.reinitPhosphorIcons();
    };

    // Initialize observer when page loads
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', window.initPhosphorIconsObserver);
    } else {
        // DOM is already loaded
        setTimeout(window.initPhosphorIconsObserver, 100);
    }
})();
