<script lang="ts">
	import { authenticate, authWithOTP } from '$lib/auth';
	import { goto } from '$app/navigation';
	import { CircleX, Info, Eye, EyeOff } from '@lucide/svelte';
	import { isAtm, setAtmLogoutTime } from '$lib/atm';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { getSetting } from '$lib/settings';

	let siteName = 'Einstein Bank';
	let registry = false;

	let identity = '';
	let password = '';
	let otp = '';
	let mfaId = '';
	let otpId = '';

	let menu: 'login' | 'otp' = 'login';

	let error = false;
	let errorMsg = '';

	let info = false;
	let infoMsg = '';

	let loading = false;
	let showPassword = false;

	let isAtmMode = false;

	async function handleLogin() {
		if (loading) return;
		loading = true;
		isAtmMode = isAtm();

		if (!password && identity !== '') {
			if (identity === '#atm#') {
				let mode = localStorage.getItem('atm_mode');
				if (mode == null) {
					localStorage.setItem('atm_mode', 'true');
					info = true;
					infoMsg = 'ATM-Modus aktiviert.';
					loading = false;
					return;
				} else {
					if (mode === 'true') {
						localStorage.setItem('atm_mode', 'false');
						infoMsg = 'ATM-Modus deaktiviert.';
					} else {
						localStorage.setItem('atm_mode', 'true');
						infoMsg = 'ATM-Modus aktiviert.';
					}
				}
				isAtmMode = isAtm();
				info = true;
				error = false;
				loading = false;
				return;
			}
			if (identity.startsWith('#atm#time#')) {
				const time = identity.split('#')[3];
				if (time && !isNaN(Number(time))) {
					setAtmLogoutTime(Number(time));
					info = true;
					infoMsg = `ATM-Logout-Zeit auf ${time} Sekunden gesetzt.`;
					loading = false;
					return;
				}
			}
		}

		if (!identity || !password) {
			error = true;
			errorMsg = 'Bitte füllen Sie alle Felder aus.';
			loading = false;
			return;
		}

		let result = [false, '', ''] as [boolean, string, string];
		try {
			result = await authenticate(identity, password);
			if (!result[0]) {
				if (result[1] !== '' && result[2] !== '') {
					mfaId = result[1];
					otpId = result[2];
					menu = 'otp';
					info = true;
					infoMsg = 'Einmaliges Passwort erforderlich. Bitte geben Sie Ihren OTP-Code ein.';
					loading = false;
					return;
				} else {
					error = true;
					errorMsg = 'Anmeldung fehlgeschlagen. Bitte überprüfen Sie Ihre Anmeldedaten.';
					loading = false;
				}
				return;
			}
			goto('/dashboard');
		} catch (err: any) {
			console.error('Login error:', err);
			error = true;
			errorMsg = 'Anmeldung fehlgeschlagen.';
			loading = false;
		}

		loading = false;
	}
	async function handleOTP() {
		if (loading) return;
		loading = true;
		if (!otp) {
			error = true;
			errorMsg = 'Bitte geben Sie den OTP-Code ein.';
			loading = false;
			return;
		}

		let success = false;
		try {
			success = await authWithOTP(identity, password, mfaId, otpId, otp);
			if (!success) {
				error = true;
				errorMsg = 'OTP-Authentifizierung fehlgeschlagen. Bitte überprüfen Sie Ihren OTP-Code.';
				loading = false;
				return;
			}
			goto('/dashboard');
		} catch (err: any) {
			console.error('OTP error:', err);
			error = true;
			errorMsg = 'OTP-Authentifizierung fehlgeschlagen.';
			loading = false;
		}
	}
	onMount(async () => {
		if (!browser) return;
		try {
			const name = await getSetting('display_name');
			const title = typeof name === 'string' && name.trim() ? name.trim() : 'Einstein Bank';
			siteName = title;
			registry = await getSetting('passwords_changeable');
			console.log('Registry: ' + registry);
		} catch {
			siteName = 'Einstein Bank';
		}
		isAtmMode = isAtm();
		console.log('ATM Mode: ' + isAtmMode);
		// Get username from query parameter if it exists
		if (browser) {
			let username = new URLSearchParams(window.location.search).get('username');
			if (username) {
				identity = username;
			}
		}
	});
</script>

<title>Login</title>
<div
	class="login-screen w-full flex items-center justify-center bg-[#1E1E21] relative overflow-hidden"
>
	<!-- Glow Background -->
	<div class="absolute inset-0 -z-10 pointer-events-none">
		<div
			class="absolute w-96 h-96 bg-[#E34747]/25 rounded-full blur-[140px] -top-15 -left-10"
		></div>
		<div
			class="absolute w-130 h-130 bg-[#E34747]/10 rounded-full blur-[180px] -bottom-30 -right-20"
		></div>
	</div>

	<!-- Glass Card -->
	<form
		class="login-card w-full max-w-md p-8 rounded-2xl border border-white/10 shadow-2xl bg-white/5 backdrop-blur-xl animate-fade"
		on:submit|preventDefault={handleLogin}
		id="login-form"
		autocomplete={isAtmMode ? 'off' : 'on'}
	>
		{#if !isAtmMode}
			<button
				type="button"
				class="button btn bg-white/5 border-white/10 shadow-none text-white"
				on:click={() => goto('/')}
			>
				&lt;
			</button>
		{/if}

		<!-- Logo -->
		<div class="flex justify-center mb-6">
			<div class="p-4 rounded-2xl bg-white/5 border border-white/10 backdrop-blur-xl">
				<svg class="w-12 h-12 text-[#E34747]" viewBox="0 0 24 24">
					<path fill="currentColor" d="M12 2L2 7v2h20V7zm8 6H4v11h16zm-7 3h2v5h-2zm-4 0h2v5H9z" />
				</svg>
			</div>
		</div>

		<h1 class="text-center text-3xl font-bold text-white">{siteName}</h1>
		<p class="text-center text-gray-400 mt-1 mb-6">Willkommen zurück</p>

		<!-- Error Alert -->
		{#if error}
			<div
				class="flex items-center gap-3 px-4 py-3 rounded-xl text-sm mb-4 bg-red-900/30 border border-red-700/40 text-red-300"
			>
				<CircleX class="w-5 h-5" />
				<span>{errorMsg}</span>
			</div>
		{/if}

		<!-- Info Alert -->
		{#if info}
			<div
				class="flex items-center gap-3 px-4 py-3 rounded-xl text-sm mb-4 bg-green-900/30 border border-green-700/40 text-green-300"
			>
				<Info class="w-5 h-5" />
				<span>{infoMsg}</span>
			</div>
		{/if}

		{#if menu === 'login'}
			<!-- Identity Input -->
			<div class="relative mb-6">
				<input
					id="identity"
					type="text"
					bind:value={identity}
					placeholder=""
					class="w-full h-14 px-4 pt-5 bg-white/5 border border-white/10 rounded-xl text-white
                       focus:outline-none focus:border-[#E34747]"
					autocomplete={isAtmMode ? 'off' : 'username'}
				/>
				<label
					for="identity"
					class="absolute left-4 top-2 text-xs text-gray-400 pointer-events-none"
				>
					Anmeldename (z.B. 5a-1 oder unt-1)
				</label>
			</div>

			<!-- Password Input -->
			<div class="relative mb-6">
				<input
					id="password"
					type={showPassword ? 'text' : 'password'}
					bind:value={password}
					placeholder=" "
					class="w-full h-14 px-4 pt-5 bg-white/5 border border-white/10 rounded-xl text-white
                       focus:outline-none focus:border-[#E34747]"
					autocomplete={isAtmMode ? 'off' : 'current-password'}
				/>
				<label
					for="password"
					class="absolute left-4 top-2 text-xs text-gray-400 pointer-events-none"
				>
					Passwort
				</label>

				<button
					type="button"
					on:click={() => (showPassword = !showPassword)}
					class="absolute right-4 top-4 text-gray-400 hover:text-white button"
				>
					{#if showPassword}
						<EyeOff class="w-5 h-5" />
					{:else}
						<Eye class="w-5 h-5" />
					{/if}
				</button>
			</div>
			<div class="flex flex-col items-center text-center mb-6 gap-4">
				<p class="text-sm text-gray-400">
						Der Anmeldename steht auf der Bankkarte unter Kartenhalter.
				</p>
				<p class="text-xs text-gray-400">
					Mit dem fortfahren der Anmeldung stimmen Sie unseren <a
						href="/policies/terms-of-service"
						class="underline">Nutzungsbedingungen</a
					>
					und der <a href="/policies/privacy" class="underline">Datenschutzerklärung</a> zu.
				</p>
			</div>
			<!-- Login Button -->
			<button
				type="submit"
				disabled={loading}
				class="w-full h-14 rounded-xl bg-[#E34747] hover:bg-[#ff5c5c] text-white
                   font-semibold text-lg transition flex items-center justify-center button"
			>
				{#if loading}
					<span class="loading loading-spinner loading-md"></span>
				{:else}
					Anmelden
				{/if}
			</button>
		{:else}
			<!-- OTP Input -->

			<div class="relative mb-6">
				<input
					id="otp"
					type="text"
					bind:value={otp}
					placeholder=" "
					class="w-full h-14 px-4 pt-5 bg-white/5 border border-white/10 rounded-xl text-white
                       focus:outline-none focus:border-[#E34747]"
					autocomplete="off"
				/>
				<label for="otp" class="absolute left-4 top-2 text-xs text-gray-400 pointer-events-none">
					Einmaliges Passwort
				</label>
			</div>

			<button
				type="button"
				on:click={handleOTP}
				disabled={loading}
				class="w-full h-14 rounded-xl bg-[#E34747] hover:bg-[#ff5c5c] text-white
                   font-semibold text-lg transition flex items-center justify-center button"
			>
				OTP bestätigen
			</button>
		{/if}

		{#if registry}
			<button
				type="button"
				on:click={() => goto('/first-login')}
				class="w-full h-14 rounded-xl bg-white/5 border border-white/10 text-white font-semibold text-lg transition flex items-center justify-center mt-4 button"
			>
				Zur Registrierung
			</button>
		{/if}
	</form>
</div>

<style>
	* {
		user-select: none;
	}

	/* iOS/iPad Safari viewport fix + safe areas */
	.login-screen {
		/* Fallbacks */
		min-height: 100vh;
		/* Better on mobile browsers */
		min-height: 100svh;
		min-height: 100dvh;

		/* prevent cutoffs by Safari bars + notches */
		padding-top: max(16px, env(safe-area-inset-top));
		padding-bottom: max(16px, env(safe-area-inset-bottom));
		padding-left: max(16px, env(safe-area-inset-left));
		padding-right: max(16px, env(safe-area-inset-right));
	}

	/* If the height is too small (landscape), allow scrolling */
	.login-card {
		max-height: calc(100dvh - 32px - env(safe-area-inset-top) - env(safe-area-inset-bottom));
		overflow: auto;
		-webkit-overflow-scrolling: touch;
	}

	/* avoid iOS zoom on focus (inputs should be >= 16px) */
	input {
		font-size: 16px;
	}

	.animate-fade {
		animation: fadeIn 0.35s ease-out;
	}

	@keyframes fadeIn {
		from {
			opacity: 0;
			transform: translateY(10px);
		}
		to {
			opacity: 1;
			transform: translateY(0);
		}
	}

	.button:hover {
		cursor: pointer !important;
	}
</style>
