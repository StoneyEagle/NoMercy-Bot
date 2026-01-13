import { ref } from 'vue';

// Type definitions matching your chat overlay settings
interface WidgetSettings {
}

// Get settings from injected global variables
function getWidgetSettings(): WidgetSettings {
    if (typeof window !== 'undefined' && window.WIDGET_SETTINGS) {
        return window.WIDGET_SETTINGS as WidgetSettings;
    }

    // Fallback defaults matching your ChatOverlay
    return {};
}

// Reactive settings store
const widgetSettings = ref<WidgetSettings>(getWidgetSettings());

// Function to update settings (for hot-reload via SignalR)
export function updateWidgetSettings(newSettings: Partial<WidgetSettings>) {
    widgetSettings.value = { ...widgetSettings.value, ...newSettings };
}

// Export the entire settings object for direct access
export { widgetSettings };

// Widget metadata from global variables
export const widgetId = typeof window !== 'undefined' ? window.WIDGET_ID || '01KCC89B7Z14M1Z0QEC8PDAGZB' : '01KCC89B7Z14M1Z0QEC8PDAGZB';
export const widgetName = typeof window !== 'undefined' ? window.WIDGET_NAME || 'BSOD' : 'BSOD';
export const widgetVersion = typeof window !== 'undefined' ? window.WIDGET_VERSION || '1.0.0' : '1.0.0';

// Global type declarations
declare global {
    interface Window {
        WIDGET_SETTINGS?: WidgetSettings;
        WIDGET_ID?: string;
        WIDGET_NAME?: string;
        WIDGET_VERSION?: string;
    }
}
