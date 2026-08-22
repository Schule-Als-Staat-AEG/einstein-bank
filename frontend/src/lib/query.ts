export function toSafeQuery(input: string): string {
    // Make a pocketbase filter safe
    // \ becomes nothing
    return input.replaceAll(/\\/g, '');
}