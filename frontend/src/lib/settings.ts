import { browser } from '$app/environment';
import db from '$lib/connections';
import { fetchAndCacheFile, readCache, writeCache } from '$lib/caching';

export async function getSetting(key, forceFetch = false) {
	if (!browser) return null; // Ensure this runs only in the browser
	try {
		let result;
		if (readCache('setting#' + key, 1500) == null || forceFetch) {
			result = await db
				.collection('settings')
				.getFirstListItem(`key="${key}"`, { requestKey: null });
			writeCache('setting#' + key, result);
		} else {
			result = readCache('setting#' + key, 2000);
		}
		return result?.value || null;
	} catch (error) {
		console.error('Error fetching setting:', error);
		return null;
	}
}
export async function getFile(key, options = {}) {
	if (!browser) return null; // Ensure this runs only in the browser
	try {
		let result;
		if (readCache('setting#' + key, 1500) == null) {
			result = await db
				.collection('settings')
				.getFirstListItem(`key="${key}"`, { requestKey: null });
			writeCache('setting#' + key, result);
		} else {
			result = readCache('setting#' + key, 2000);
		}
		let data = db.files.getURL(result, result.file, options) || null;
		data = await fetchAndCacheFile(data, 'setting_file#' + key);
		return data;
	} catch (error) {
		console.error('Error fetching file:', error);
		return null;
	}
}
