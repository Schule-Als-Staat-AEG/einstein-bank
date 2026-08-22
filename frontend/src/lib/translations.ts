import { browser } from '$app/environment';
import { register, init, locale } from 'svelte-i18n';

load();

function load() {
	// Language configuration
	// Please follow this format future me

	// console.log("Loading translations...");
	register('en', () => import('./locales/en.json'));
	register('de', () => import('./locales/de.json'));
	register('ch', () => import('./locales/de_chen.json'));

	if (!browser) {
		return;
	}
	if (!localStorage) {
		return;
	}
	let savedLocale = typeof localStorage !== 'undefined' ? localStorage.getItem('locale') : 'de';

	if (localStorage) {
		if (localStorage.getItem('locale') === null) {
			localStorage.setItem('locale', 'de');
			savedLocale = 'de';
			console.log('No locale found, setting to default (de)');
		}
	}

	init({
		fallbackLocale: 'de',
		initialLocale: savedLocale || 'de'
	});

	locale.subscribe((value) => {
		if (typeof localStorage !== 'undefined' && value) {
			localStorage.setItem('locale', value);
			console.log(`Locale set to ${value}`);
		}
	});
}
