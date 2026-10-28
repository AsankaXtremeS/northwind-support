const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const dateFormatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' });

export function formatDateTime(isoString: string | null): string {
  return isoString ? dateTimeFormatter.format(new Date(isoString)) : '-';
}

export function formatDate(isoString: string | null): string {
  return isoString ? dateFormatter.format(new Date(isoString)) : '-';
}

/** "In 3 hours" / "5 hours ago", for due dates. */
export function formatRelativeToNow(isoString: string | null): string {
  if (!isoString) {
    return '-';
  }

  const minutes = Math.round((new Date(isoString).getTime() - Date.now()) / 60_000);
  const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

  if (Math.abs(minutes) < 60) {
    return relative.format(minutes, 'minute');
  }

  if (Math.abs(minutes) < 60 * 48) {
    return relative.format(Math.round(minutes / 60), 'hour');
  }

  return relative.format(Math.round(minutes / (60 * 24)), 'day');
}

/** Turns an enum name such as "InProgress" into "In progress". */
export function humanize(value: string): string {
  const spaced = value.replace(/([a-z])([A-Z])/g, '$1 $2');

  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/** A length of time in minutes as "4 h", "45 min" or "1 h 30 min". */
export function formatMinutes(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours === 0) {
    return `${minutes} min`;
  }

  return minutes === 0 ? `${hours} h` : `${hours} h ${minutes} min`;
}
