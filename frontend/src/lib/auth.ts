import { browser } from '$app/environment';
import db from '$lib/connections';
import 'svelte';
import { isAtm } from './atm';
import type { ClientResponseError } from 'pocketbase';
import { clearCache } from './caching';

function isSuperuser(): boolean {
	if (!browser) return false;
	if (localStorage) {
		let isSup = localStorage?.getItem('isSuperuser');
		return isSup === 'true';
	} else {
		return false;
	}
}
function _getSuperuserTarget() {
	if (!browser) return null;
	if (localStorage !== undefined) {
		return localStorage.getItem('superuserTarget');
	} else {
		console.error('LOCALSTORAGE UNDEFINED. EVERYTHING WILL BREAK! ABORT ABORT! WE ARE SCREWED!');
		return null;
	}
}
function getAuthStore() {
	if (!browser) return null;
	let isSup: boolean = isSuperuser();
	if (isSup && localStorage !== undefined) {
		let storeStr = localStorage?.getItem('userStore');
		if (storeStr !== null) {
			return JSON.parse(storeStr);
		}
	} else if (isSup === false) {
		return db.authStore;
	} else if (localStorage !== undefined) {
		console.error('LOCALSTORAGE UNDEFINED. EVERYTHING WILL BREAK! ABORT ABORT! WE ARE SCREWED!');
		return null;
	}
}
function getUserData() {
	if (!browser) return null;
	if (localStorage !== undefined) {
		let userDataStr = localStorage?.getItem('userData');
		if (userDataStr !== null) {
			return JSON.parse(userDataStr);
		}
	} else {
		console.error('LOCALSTORAGE UNDEFINED. EVERYTHING WILL BREAK! ABORT ABORT! WE ARE SCREWED!');
		return null;
	}
}
function logout() {
	if (!browser) return;
	if (localStorage !== undefined) {
		localStorage.removeItem('isSuperuser');
		localStorage.removeItem('superuserAuthStamp');
		localStorage.removeItem('userStore');
		localStorage.removeItem('userAuthStamp');
		localStorage.removeItem('superuserTarget');
		localStorage.removeItem('adminticketnotifications');
		localStorage.removeItem('userData');

		clearCache('lastWealthAffected');
		clearCache('lastWealthBalance');
		clearCache('lastWealthCheck');
		clearCache('lastWealthDeductable');
	}
	db.authStore.clear();
	if (isAtm()) {
		localStorage.setItem('locale', 'de');
	}
}
async function authenticate(
	identity: string,
	password: string
): Promise<[boolean, string, string]> {
	if (!browser) return [false, '', ''];
	identity = identity.trim();
	if (identity.startsWith('admin#')) {
		identity = identity.substring(6); // Remove "admin#" prefix

		// Superuser authentication
		let record: any;
		try {
			record = await db.collection('_superusers').authWithPassword(identity, password);
			let userData: any = await db.collection('user_data').getOne(record.record.user);
			localStorage.setItem('userData', JSON.stringify(userData));
		} catch (error) {
			if (error.status === 401) {
				const otpRsp = await db.collection('_superusers').requestOTP(identity);
				return [false, error.data.mfaId, otpRsp.otpId];
			}
			if (error.status === 400) {
				return [false, 'Ungültige Eingabe', ''];
			}
		}

		// Write superuser to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('isSuperuser', 'true');
		}

		// Write timestamp of last auth to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('superuserAuthStamp', Date.now().toString());
		}

		// Check if impersonation target exists
		record = record.record;
		if (record.user === undefined || record.user === null || record.user === '') {
			return;
			const data = await db.collection('users').create({
				name: record.email,
				email: record.email,
				password: password,
				passwordConfirm: password,
				verified: true,
				locked: false
			});
			record.user = data.id;

			// Update the _superusers record with the new user id
			await db.collection('_superusers').update(record.id, {
				user: record.user
			});
		}

		// Impersonate the user
		let impDatbase: any = await db.collection('users').impersonate(record.user, 604800); // 7 days
		let userData: any = await db.collection('user_data').getOne(record.user);

		let data: any = {};
		data.record = impDatbase.authStore.baseModel;
		data.token = impDatbase.authStore.token;

		// Write impersonation token & timestamp & target to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('userStore', JSON.stringify(data));
			localStorage.setItem('userData', JSON.stringify(userData));
			localStorage.setItem('userAuthStamp', Date.now().toString());
			localStorage.setItem('superuserTarget', record.user);
		}
	} else // Normal user authentication
	{
		// Write normal user to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('isSuperuser', 'false');
		}

		// User authentication
		try {
			await db.collection('users').authWithPassword(identity, password);
			let data = await db.collection('user_data').getOne(db.authStore.record!.id);
			localStorage.setItem('userData', JSON.stringify(data));
		} catch (error) {
			if (error.status === 400) {
				return [false, 'Ungültige Eingabe', ''];
			}
			if (error.status === 401) {
				const otpRsp = await db.collection('users').requestOTP(identity);
				return [false, error.data.mfaId, otpRsp.otpId];
			}
		}

		// Write timestamp of last auth to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('userAuthStamp', Date.now().toString());
		}
	}
	return [true, '', ''];
}
async function authWithOTP(
	identity: string,
	password: string,
	mfaId: string,
	otpId: string,
	otp: string
): Promise<boolean> {
	if (!browser) return false;
	identity = identity.trim();
	if (identity.startsWith('admin#')) {
		identity = identity.substring(6); // Remove "admin#" prefix

		// Superuser authentication
		let record: any;
		try {
			record = await db.collection('_superusers').authWithOTP(otpId, otp, { mfaId: mfaId });
			let userData: any = await db.collection('user_data').getOne(record.record.user);
			localStorage.setItem('userData', JSON.stringify(userData));
		} catch (error) {
			return false;
		}

		// Write superuser to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('isSuperuser', 'true');
		}

		// Write timestamp of last auth to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('superuserAuthStamp', Date.now().toString());
		}

		// Check if impersonation target exists
		record = record.record;
		if (record.user === undefined || record.user === null || record.user === '') {
			return;
			const data = await db.collection('users').create({
				name: record.email,
				email: record.email,
				password: password,
				passwordConfirm: password,
				verified: true,
				locked: false
			});
			record.user = data.id;

			// Update the _superusers record with the new user id
			await db.collection('_superusers').update(record.id, {
				user: record.user
			});
		}

		// Impersonate the user
		let impDatbase: any = await db.collection('users').impersonate(record.user, 604800); // 7 days
		let userData = await db.collection('user_data').getOne(record.user);
		let data: any = {};
		data.record = impDatbase.authStore.baseModel;
		data.token = impDatbase.authStore.token;

		// Write impersonation token & timestamp & target to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('userData', JSON.stringify(userData));
			localStorage.setItem('userStore', JSON.stringify(data));
			localStorage.setItem('userAuthStamp', Date.now().toString());
			localStorage.setItem('superuserTarget', record.user);
		}
	} else // Normal user authentication
	{
		// Check if the password is in quotes. If it is remove them
		if (password.startsWith('"') && password.endsWith('"')) {
			password = password.substring(1, password.length - 1);
		}
		
		// Write normal user to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('isSuperuser', 'false');
		}

		// User authentication
		try {
			await db.collection('users').authWithOTP(otpId, otp, { mfaId: mfaId });
			let userData = await db.collection('user_data').getOne(db.authStore.record!.id);
			localStorage.setItem('userData', JSON.stringify(userData));
		} catch (error) {
			return false;
		}

		// Write timestamp of last auth to local storage
		if (localStorage !== undefined) {
			localStorage.setItem('userAuthStamp', Date.now().toString());
		}
	}
	return true;
}
async function checkAuthRefresh() {
	if (!browser) return;
	// get timestamp of last auth
	let isSup: boolean = isSuperuser();
	let lastAuthStampStr: string | null;
	let lastImpStampStr: string | null = null;

	if (isSup) {
		lastAuthStampStr = localStorage?.getItem('superuserAuthStamp');
		lastImpStampStr = localStorage?.getItem('userAuthStamp');
	} else {
		lastAuthStampStr = localStorage?.getItem('userAuthStamp');
	}
	if (lastAuthStampStr === null) {
		throw new Error('No authentication timestamp found');
	}
	if (Date.now() - parseInt(lastAuthStampStr) > 3 * 60 * 1000) {
		// 60 minutes
		authRefresh();
		return;
	}
	if (isSup && lastImpStampStr !== null) {
		if (Date.now() - parseInt(lastImpStampStr) > 3 * 60 * 1000) {
			// 30 minutes
			authRefresh();
			return;
		}
	}
}
async function authRefresh() {
	if (!browser) return;
	// Get if superuser
	let isSup: boolean = isSuperuser();
	if (isSup) {
		// Refresh superuser auth timestamp
		if (localStorage !== undefined) {
			localStorage.setItem('superuserAuthStamp', Date.now().toString());
		}
		// Refresh superuser
		await db.collection('_superusers').authRefresh();

		// Refresh impersonation token & timestamp
		const store = getAuthStore();
		if (store === null) {
			throw new Error('No auth store found for superuser impersonation refresh');
		}
		let impDatbase: any = await db.collection('users').impersonate(store.record.id, 604800); // 7 days
		let userData: any = await db.collection('user_data').getOne(store.record.id);
		let data: any = {};
		data.record = impDatbase.authStore.baseModel;
		data.token = impDatbase.authStore.token;

		if (localStorage !== undefined) {
			localStorage.setItem('userStore', JSON.stringify(data));
			localStorage.setItem('userData', JSON.stringify(userData));
			localStorage.setItem('userAuthStamp', Date.now().toString());
			localStorage.setItem('superuserTarget', store.record.id);
		}
	} else {
		// Refresh user auth timestamp
		if (localStorage !== undefined) {
			localStorage.setItem('userAuthStamp', Date.now().toString());
		}
		await db.collection('users').authRefresh();
		const data = await db.collection('user_data').getOne(db.authStore.record!.id);
		localStorage.setItem('userData', JSON.stringify(data));
	}
}
async function _superuserAuthenticateAs(userid: string) {
	if (!browser) return;
	// Ensure we are a superuser
	if (!isSuperuser()) {
		throw new Error('Not a superuser');
	}
	clearCache('lastWealthAffected');
	clearCache('lastWealthBalance');
	clearCache('lastWealthCheck');
	clearCache('lastWealthDeductable');
	// Impersonate the user
	const impDatbase: any = await db.collection('users').impersonate(userid, 604800); // 7 days
	const userData: any = await db.collection('user_data').getOne(userid);
	let data: any = {};
	data.record = impDatbase.authStore.baseModel;
	data.token = impDatbase.authStore.token;
	// Write impersonation token & timestamp & target to local storage
	if (localStorage !== undefined) {
		localStorage.setItem('userStore', JSON.stringify(data));
		localStorage.setItem('userData', JSON.stringify(userData));
		localStorage.setItem('userAuthStamp', Date.now().toString());
		localStorage.setItem('superuserTarget', userid);
	}
}
async function _superuserSetAuth(userid: string) {
	if (!browser) return;
	// Ensure we are a superuser
	if (!isSuperuser()) {
		throw new Error('Not a superuser');
	}
	await _superuserAuthenticateAs(userid);
	// Write the target user to the _superusers record
	await db.collection('_superusers').update(db.authStore.record!.id, {
		user: userid
	});
}
async function refreshStores() {
	if (!browser) return;
	if (isSuperuser()) {
		await db.collection('_superusers').authRefresh();
		const store = getAuthStore();
		if (store === null) {
			throw new Error('No auth store found for superuser impersonation refresh');
		}
		let impDatbase: any = await db.collection('users').impersonate(store.record.id, 604800); // 7 days
		let userData: any = await db.collection('user_data').getOne(store.record.id);
		let data: any = {};
		data.record = impDatbase.authStore.baseModel;
		data.token = impDatbase.authStore.token;

		if (localStorage !== undefined) {
			localStorage.setItem('userStore', JSON.stringify(data));
			localStorage.setItem('userData', JSON.stringify(userData));
			localStorage.setItem('userAuthStamp', Date.now().toString());
			localStorage.setItem('superuserTarget', store.record.id);
		}
	} else {
		await db.collection('users').authRefresh();
		const data = await db.collection('user_data').getOne(db.authStore.record!.id);
		localStorage.setItem('userData', JSON.stringify(data));
	}
}
export {
	getAuthStore,
	getUserData,
	isSuperuser,
	authenticate,
	authWithOTP,
	checkAuthRefresh,
	authRefresh,
	logout,
	_superuserAuthenticateAs,
	_superuserSetAuth,
	_getSuperuserTarget,
	refreshStores
};
