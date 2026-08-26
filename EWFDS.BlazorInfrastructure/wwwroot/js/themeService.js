// Theme Service JavaScript Module
// Handles dynamic theme switching for Telerik Kendo UI themes

export function setTheme(themeUrl, themeName) {
    const linkId = 'kendo-theme-link';

    // Remove any existing static Telerik/Kendo theme links (except our managed one)
    // This prevents conflicts with initial static stylesheet links in App.razor
    const existingLinks = document.querySelectorAll('link[rel="stylesheet"]');
    existingLinks.forEach(link => {
        const href = link.href || '';
        // Remove static Telerik Blazor theme links (but not our managed one)
        if (link.id !== linkId && 
            (href.includes('blazor.cdn.telerik.com') && href.includes('kendo-theme')) ||
            (href.includes('kendo.cdn.telerik.com/themes'))) {
            link.remove();
            console.log(`Removed conflicting theme link: ${href}`);
        }
    });

    let themeLink = document.getElementById(linkId);
    const isNewLink = !themeLink;

    if (isNewLink) {
        // Create the link element if it doesn't exist
        themeLink = document.createElement('link');
        themeLink.id = linkId;
        themeLink.rel = 'stylesheet';
        themeLink.type = 'text/css';
    }

    // Listen for stylesheet load to refresh charts
    themeLink.onload = function() {
        console.log(`Theme stylesheet loaded: ${themeName}`);
        // Force repaint of Telerik charts after theme loads
        refreshCharts();
    };

    themeLink.onerror = function() {
        console.error(`Failed to load theme: ${themeUrl}`);
    };

    // Update the href to load the new theme
    themeLink.href = themeUrl;

    if (isNewLink) {
        // Insert after the head or as first child of head
        const head = document.head || document.getElementsByTagName('head')[0];
        head.appendChild(themeLink);
    }

    // Update the body class for additional styling hooks
    updateBodyThemeClass(themeName);

    console.log(`Theme changed to: ${themeName} (${themeUrl})`);
}

function refreshCharts() {
    // Force Telerik Charts to redraw by triggering a resize event
    // This causes the charts to recalculate their styles from the new theme
    setTimeout(() => {
        window.dispatchEvent(new Event('resize'));
        console.log('Triggered chart refresh via resize event');
    }, 50);
}

function updateBodyThemeClass(themeName) {
    const body = document.body;

    // Remove all existing theme classes
    const existingThemeClasses = Array.from(body.classList)
        .filter(className => className.startsWith('theme-'));

    existingThemeClasses.forEach(className => {
        body.classList.remove(className);
    });

    // Add new theme class
    body.classList.add(`theme-${themeName}`);
}

export function getCurrentTheme() {
    const linkId = 'kendo-theme-link';
    const themeLink = document.getElementById(linkId);

    if (themeLink) {
        return themeLink.href;
    }

    return null;
}

// Helper function to preload theme for smoother transitions
export function preloadTheme(themeUrl) {
    const link = document.createElement('link');
    link.rel = 'preload';
    link.as = 'style';
    link.href = themeUrl;
    document.head.appendChild(link);
}
