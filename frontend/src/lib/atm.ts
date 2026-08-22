import { browser } from '$app/environment';
import { isSuperuser } from './auth';

function isAtm(): boolean {
	if (!browser) return false;
	if (!localStorage) return false;
	//if(isSuperuser()) return false;
	return localStorage.getItem('atm_mode') === 'true';
}
function setAtmMode(isAtm: boolean) {
	if (!browser) return;
	if (!localStorage) return;
	localStorage.setItem('atm_mode', isAtm ? 'true' : 'false');
}
function setAtmLogoutTime(timeInSeconds: number) {
	if (!browser) return;
	if (!localStorage) return;
	localStorage.setItem('atm_logout_time', timeInSeconds.toString());
}
function getAtmLogoutTime(defaultTimeInSeconds: number = 120): number {
	if (!browser) return defaultTimeInSeconds;
	if (!localStorage) return defaultTimeInSeconds;
	const timeStr = localStorage.getItem('atm_logout_time');
	if (timeStr) {
		const time = parseInt(timeStr);
		return isNaN(time) ? defaultTimeInSeconds : time;
	}
	return defaultTimeInSeconds;
}
export { isAtm, setAtmMode, setAtmLogoutTime, getAtmLogoutTime };
