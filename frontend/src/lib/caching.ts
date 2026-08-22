// The beatifull money saver
// Making sure we are not exceeding the network transfers from Linode

import { browser } from '$app/environment';

// You will mostly use 'fetchAndCacheFile' to get and cache remote files

function writeCache(key, data) {
	if (!browser) return;
	try {
		localStorage.setItem(
			key,
			JSON.stringify({
				timestamp: Date.now(),
				data: data
			})
		);
	} catch (error) {
		console.error('Error writing to cache:', error);
	}
}
function readCache(key, maxAgeMs) {
	if (!browser) return null;

	if (maxAgeMs === null) {
		maxAgeMs = DEFAULT_MAX_AGE;
	}
	try {
		const cached = localStorage.getItem(key);
		if (cached) {
			const parsed = JSON.parse(cached);
			if (Date.now() - parsed.timestamp < maxAgeMs) {
				return parsed.data;
			} else {
				// expired
				return null;
			}
		}
	} catch (error) {
		console.error('Error reading from cache:', error);
	}
}
function clearCache(key) {
	if (!browser) return;

	try {
		localStorage.removeItem(key);
	} catch (error) {
		console.error('Error clearing cache:', error);
	}
}

// ----- New: file caching helpers -----
// Use a separate prefix to avoid colliding with other cache entries
const FILE_CACHE_PREFIX = 'filecache:';
const DEFAULT_MAX_AGE = 1000 * 60; // 1 Minute by default
const MAX_STORE_BYTES = 200 * 1024 * 1024; // 200 MB per file safety limit

// New: total cache soft limit (delete oldest when exceeded)
const DEFAULT_TOTAL_CACHE_BYTES = 20 * 1024 * 1024; // 20 MB total file-cache by default

function arrayBufferToBase64(buffer) {
	if (!browser) return null;
	// convert in chunks to avoid stack issues on large arrays
	const bytes = new Uint8Array(buffer);
	const chunkSize = 0x8000;
	let binary = '';
	for (let i = 0; i < bytes.length; i += chunkSize) {
		binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
	}
	return btoa(binary);
}

/**
 * Save a Blob/File to localStorage under a key.
 * key: identifier (string)
 * blob: Blob or File instance
 * returns: true if saved, false otherwise
 */
async function saveFileToLocalStorage(key, blob) {
	if (!browser) return false;

	// New: try to free space / purge old files first (synchronous helper)
	try {
		// keep default limits; caller can use exported purgeOldFileCaches to tune
		purgeOldFileCaches();
	} catch (e) {
		// ignore purge errors
		console.warn('saveFileToLocalStorage: purge failed', e);
	}

	if (!blob || typeof blob.arrayBuffer !== 'function') {
		console.error('saveFileToLocalStorage: invalid blob');
		return false;
	}
	const size = blob.size || 0;
	if (size > MAX_STORE_BYTES) {
		console.warn(`saveFileToLocalStorage: file too large (${size} bytes), skipping cache`);
		return false;
	}
	try {
		const buffer = await blob.arrayBuffer();
		const base64 = arrayBufferToBase64(buffer);
		const data = { mime: blob.type || 'application/octet-stream', base64 };
		writeCache(FILE_CACHE_PREFIX + key, data);
		return true;
	} catch (err) {
		console.error('saveFileToLocalStorage error:', err);
		return false;
	}
}

// ----- New: cleanup / purge helpers -----

function getAllFileCacheKeys() {
	if (!browser) return [];

	const keys = [];
	for (let i = 0; i < localStorage.length; i++) {
		const k = localStorage.key(i);
		if (k && k.startsWith(FILE_CACHE_PREFIX)) keys.push(k);
	}
	return keys;
}

function parseCacheEntry(key) {
	if (!browser) return null;

	try {
		const raw = localStorage.getItem(key);
		if (!raw) return null;
		const parsed = JSON.parse(raw);
		const timestamp = parsed.timestamp || (parsed.data && parsed.data.timestamp) || 0;
		// estimate size in bytes:
		let size = 0;
		const data = parsed.data;
		if (data && data.base64) {
			// base64 to bytes approx
			size = Math.ceil((data.base64.length * 3) / 4);
		} else {
			// fallback to actual stored string byte size
			try {
				size = new Blob([raw]).size;
			} catch (e) {
				size = (raw && raw.length) || 0;
			}
		}
		return { key, timestamp, size };
	} catch (e) {
		return null;
	}
}

/**
 * Purge old file-cache entries:
 * - remove items older than maxAgeMs
 * - ensure total cached bytes <= maxTotalBytes by deleting oldest entries first
 * Options:
 *   maxAgeMs: milliseconds (default DEFAULT_MAX_AGE)
 *   maxTotalBytes: bytes (default DEFAULT_TOTAL_CACHE_BYTES)
 *
 * Returns a summary { freedBytes, remainingBytes }
 */
function purgeOldFileCaches({
	maxAgeMs = DEFAULT_MAX_AGE,
	maxTotalBytes = DEFAULT_TOTAL_CACHE_BYTES
} = {}) {
	if (!browser) return { freedBytes: 0, remainingBytes: 0 };

	const now = Date.now();
	const keys = getAllFileCacheKeys();
	const entries = keys.map(parseCacheEntry).filter(Boolean);

	let freed = 0;

	// 1) delete by age
	const keep = [];
	for (const e of entries) {
		if (e.timestamp && now - e.timestamp > maxAgeMs) {
			try {
				localStorage.removeItem(e.key);
				freed += e.size;
			} catch (err) {
				// ignore single failures
			}
		} else {
			keep.push(e);
		}
	}

	// 2) enforce total size
	let total = keep.reduce((s, e) => s + e.size, 0);
	if (total > maxTotalBytes) {
		// delete oldest first
		keep.sort((a, b) => (a.timestamp || 0) - (b.timestamp || 0));
		for (const e of keep) {
			if (total <= maxTotalBytes) break;
			try {
				localStorage.removeItem(e.key);
				total -= e.size;
				freed += e.size;
			} catch (err) {
				// ignore and continue
			}
		}
	}

	return { freedBytes: freed, remainingBytes: total };
}

/**
 * Remove all file-cache entries (force clear).
 */
function deleteAllFileCaches() {
	if (!browser) return { removed: 0 };

	const keys = getAllFileCacheKeys();
	let removed = 0;
	for (const k of keys) {
		try {
			localStorage.removeItem(k);
			removed++;
		} catch (e) {
			// ignore
		}
	}
	return { removed };
}

// ----- Existing: fetch helpers -----

/**
 * Small helper: try a HEAD request to see if a server URL still exists.
 * Returns true if the server responds with a successful status.
 */
async function checkUrlExists(url, timeoutMs = 5000) {
	if (!browser) {
		return false;
	}
	try {
		const controller = new AbortController();
		const id = setTimeout(() => controller.abort(), timeoutMs);
		// Some servers may not accept HEAD; treat non-OK as "not exists"
		const resp = await fetch(url, { method: 'HEAD', signal: controller.signal });
		clearTimeout(id);
		return resp.ok;
	} catch (err) {
		// If HEAD is blocked or fails, try a lightweight GET as a fallback
		try {
			const controller2 = new AbortController();
			const id2 = setTimeout(() => controller2.abort(), timeoutMs);
			const resp2 = await fetch(url, {
				method: 'GET',
				headers: { Range: 'bytes=0-0' },
				signal: controller2.signal
			});
			clearTimeout(id2);
			return resp2.ok;
		} catch (err2) {
			return false;
		}
	}
}

/**
 * Get a data: URL for a cached file.
 * key: identifier (string)
 * maxAgeMs: maximum age in ms (optional)
 * returns: dataUrl string or null
 */
function getFileDataUrl(key, maxAgeMs = DEFAULT_MAX_AGE) {
	if (!browser) return null;

	if (maxAgeMs === null) {
		maxAgeMs = DEFAULT_MAX_AGE;
	}
	try {
		const cached = readCache(FILE_CACHE_PREFIX + key, maxAgeMs);
		if (!cached) return null;
		// If cached is an object with base64, return data URL
		if (cached.base64 && cached.mime) {
			return `data:${cached.mime};base64,${cached.base64}`;
		}
		// If cached is a plain URL (string) or object with url field, return that directly.
		// Note: caller may expect a URL; we do not verify here to keep function lightweight.
		if (typeof cached === 'string') return cached;
		if (typeof cached === 'object' && cached.url) return cached.url;
		return null;
	} catch (err) {
		console.error('getFileDataUrl error:', err);
		return null;
	}
}

/**
 * Fetch a remote file and cache it, or return cached data if available and fresh.
 * url: remote url
 * key: cache key to store under (if omitted, url is used)
 * maxAgeMs: optional freshness window
 * returns: dataUrl string or null
 */
async function fetchAndCacheFile(url, key = null, maxAgeMs = DEFAULT_MAX_AGE) {
	if (!browser) return null;

	if (maxAgeMs === null) {
		maxAgeMs = DEFAULT_MAX_AGE;
	}

	// New: purge old caches proactively whenever a file is fetched
	try {
		purgeOldFileCaches();
	} catch (err) {
		// ignore purge errors but don't stop the fetch
		console.warn('fetchAndCacheFile: purge failed', err);
	}

	const cacheKey = FILE_CACHE_PREFIX + (key || url);
	// check cache first
	const cached = readCache(cacheKey, maxAgeMs);

	// Fast path: cached base64 data -> return immediately
	if (cached != null && cached.base64) {
		return `data:${cached.mime};base64,${cached.base64}`;
	}

	// If cached is a server URL (string) or object with url, verify it still exists.
	let cachedUrl = null;
	if (cached) {
		if (typeof cached === 'string') cachedUrl = cached;
		else if (typeof cached === 'object' && cached.url) cachedUrl = cached.url;
	}
	if (cachedUrl) {
		try {
			const exists = await checkUrlExists(cachedUrl);
			if (exists) {
				// Refresh the cache timestamp so this entry isn't purged immediately
				try {
					writeCache(cacheKey, cached);
				} catch (e) {
					// ignore write failures
				}
				// still valid on server; return cached URL
				return cachedUrl;
			}
			// else: cached URL 404s -> fall through to re-fetch from origin and update cache
			console.warn('fetchAndCacheFile: cached URL not found on server, re-fetching:', cachedUrl);
		} catch (err) {
			console.warn('fetchAndCacheFile: error verifying cached url, will re-fetch:', err);
		}
	}

	try {
		if (url == null) {
			return null;
		}
		const resp = await fetch(url.toString());
		if (!resp.ok) {
			console.warn('fetchAndCacheFile: fetch failed', resp.status);
			// If we had a cached URL (even if verification failed) return it as last resort
			if (cachedUrl) return cachedUrl;
			return null;
		}
		const blob = await resp.blob();

		// Try to persist to localStorage (base64). If successful, return data URL from cache.
		const saved = await saveFileToLocalStorage(key || url, blob);
		if (saved) {
			const cached2 = readCache(cacheKey, maxAgeMs);
			if (cached2 && cached2.base64) return `data:${cached2.mime};base64,${cached2.base64}`;
		} else {
			// If we couldn't persist, optionally store the server URL as a lightweight cache entry
			// so we can validate later. Store as { url, timestamp } via writeCache helper.
			try {
				writeCache(cacheKey, { url, timestamp: Date.now() });
			} catch (err) {
				// ignore
			}
		}

		// fallback to object URL (non-persistent)
		return URL.createObjectURL(blob);
	} catch (err) {
		console.error('fetchAndCacheFile error:', err);
		// If fetch failed but we have a cached URL, return it as a fallback
		if (cachedUrl) return cachedUrl;
		return null;
	}
}
function deleteAllCaches() {
	if (!browser) return;

	try {
		// Get all keys in localStorage and remove those that are our cache entries
		for (let i = 0; i < localStorage.length; i++) {
			const key = localStorage.key(i);
			if (key && (key.startsWith(FILE_CACHE_PREFIX) || key.startsWith('setting#'))) {
				localStorage.removeItem(key);
			}
		}
	} catch (error) {
		console.error('Error clearing all caches:', error);
	}
}
// Export the new helpers
export {
	writeCache,
	readCache,
	clearCache,
	saveFileToLocalStorage,
	getFileDataUrl,
	fetchAndCacheFile,
	purgeOldFileCaches,
	deleteAllFileCaches,
	deleteAllCaches
};
