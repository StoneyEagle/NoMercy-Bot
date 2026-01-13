/**
 * Utility functions for widget development
 */

/**
 * Check if a color is too light (for text contrast)
 */
export function tooLight(color: string, threshold: number = 140): boolean {
    if (!color)
        return false;

    // Remove # if present
    const hex = color.replace('#', '');

    // Parse RGB values
    const r = Number.parseInt(hex.substr(0, 2), 16);
    const g = Number.parseInt(hex.substr(2, 2), 16);
    const b = Number.parseInt(hex.substr(4, 2), 16);

    // Calculate brightness
    const brightness = (r * 299 + g * 587 + b * 114) / 1000;

    return brightness > threshold;
}

/**
 * Convert a name to kebab-case
 */
export function toKebabCase(str: string): string {
    return str
        .toLowerCase()
        .replace(/\s+/g, '-')
        .replace(/[^a-z0-9-]/g, '');
}

/**
 * Convert a name to PascalCase
 */
export function toPascalCase(str: string): string {
    return str
        .replace(/\s+/g, ' ')
        .split(' ')
        .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
        .join('');
}

/**
 * Generate CSS custom properties for color theming
 */
export function generateColorProperties(baseColor: string) {
    return {
        '--color-300': `hsl(from ${baseColor} h calc(s * .30) l)`,
        '--color-500': `hsl(from ${baseColor} h calc(s * .50) l)`,
        '--color-700': `hsl(from ${baseColor} h s calc(l * .70))`,
    };
}

/**
 * Format timestamp for display
 */
export function formatTimestamp(date: Date): string {
    return date.toLocaleTimeString('en-US', {
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
    });
}

/**
 * Debounce function for performance optimization
 */
export function debounce<T extends (...args: any[]) => void>(
    func: T,
    wait: number,
): (...args: Parameters<T>) => void {
    let timeout: NodeJS.Timeout;

    return (...args: Parameters<T>) => {
        clearTimeout(timeout);
        timeout = setTimeout(() => func(...args), wait);
    };
}
