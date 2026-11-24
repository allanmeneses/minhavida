export async function copyText(value: string): Promise<boolean> {
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(value);
    return true;
  }

  if (typeof document === 'undefined') {
    return false;
  }

  const area = document.createElement('textarea');
  area.value = value;
  area.setAttribute('readonly', 'true');
  area.style.position = 'fixed';
  area.style.left = '-9999px';
  document.body.appendChild(area);
  area.select();
  const ok = document.execCommand('copy');
  document.body.removeChild(area);
  return ok;
}

export function calendarFeedUrl(apiUrl: string, feedUrl: string): string {
  if (feedUrl.startsWith('http://') || feedUrl.startsWith('https://')) {
    return feedUrl;
  }

  return `${apiUrl.replace(/\/$/, '')}${feedUrl.startsWith('/') ? feedUrl : `/${feedUrl}`}`;
}
