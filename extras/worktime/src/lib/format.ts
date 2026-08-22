export function formatDateTime(value?: string): string {
  if (!value) return '—';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'medium'
  }).format(date);
}

export function formatCurrency(value?: number): string {
  if (value === undefined || value === null) return '—';

  return new Intl.NumberFormat('en-GB', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value);
}

export function formatDuration(start: string, end: Date = new Date()): string {
  const startDate = new Date(start);
  if (Number.isNaN(startDate.getTime())) return '—';

  const totalSeconds = Math.max(0, Math.floor((end.getTime() - startDate.getTime()) / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  return [hours, minutes, seconds].map((part) => String(part).padStart(2, '0')).join(':');
}

export function errorMessage(error: unknown): string {
  if (error && typeof error === 'object') {
    const candidate = error as {
      message?: string;
      response?: { message?: string; data?: Record<string, { message?: string }> };
    };

    const fieldError = candidate.response?.data
      ? Object.values(candidate.response.data).find((entry) => entry?.message)?.message
      : undefined;

    return fieldError || candidate.response?.message || candidate.message || 'An unexpected error occurred.';
  }

  return 'An unexpected error occurred.';
}
