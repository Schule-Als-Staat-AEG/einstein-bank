<script lang="ts">
	import { browser } from '$app/environment';
	import db from '$lib/connections';
	import { getSetting, getFile } from '$lib/settings';
	import { onDestroy, onMount } from 'svelte';

	let siteName = 'Einstein Bank';
	let tagline = 'Intelligent. Sicher. Einfach.';
	let menuOpen = false;
	let logo_url = '';
	let contactEmail = '';

	let loggedIn = false;

	function setLoggedIn() {
		loggedIn = !!db.authStore?.isValid;
	}

	function closeMenu() {
		menuOpen = false;
	}

	function onNavClick() {
		closeMenu();
	}

	onMount(async () => {
		if (!browser) return;

		setLoggedIn();
		const unsub = db.authStore.onChange(() => setLoggedIn(), true);
		onDestroy(() => unsub?.());

		try {
			logo_url = await getFile('logo');
			const name = await getSetting('display_name');
			const title = typeof name === 'string' && name.trim() ? name.trim() : 'Einstein Bank';
			document.title = title;
			siteName = title;
			contactEmail = (await getSetting('contact_email')) || '';
		} catch {
			document.title = 'Einstein Bank';
			siteName = 'Einstein Bank';
			contactEmail = '';
		}
	});
</script>

<div class="min-h-screen ui-noselect bg-[#0A0B0E] text-slate-100">
	<!-- AMBIENT BACKGROUND -->
	<div class="pointer-events-none fixed inset-0 -z-10 overflow-hidden">
		<div
			class="absolute -top-40 -left-40 h-125 w-125 rounded-full bg-[#E34747]/12 blur-[120px]"
		></div>
		<div
			class="absolute top-1/3 -right-32 h-100 w-100 rounded-full bg-[#E34747]/6 blur-[100px]"
		></div>
		<div
			class="absolute bottom-0 left-1/2 -translate-x-1/2 h-75 w-150 rounded-full bg-[#E34747]/8 blur-[100px]"
		></div>
		<div
			class="absolute inset-0 bg-[radial-gradient(ellipse_at_top,rgba(255,255,255,0.04),transparent_50%)]"
		></div>
		<!-- grid overlay -->
		<div
			class="absolute inset-0 opacity-[0.025]"
			style="background-image: linear-gradient(rgba(255,255,255,0.3) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.3) 1px, transparent 1px); background-size: 60px 60px;"
		></div>
	</div>

	<!-- NAV -->
	<nav class="sticky top-0 z-50 border-b border-white/8 bg-[#0A0B0E]/80 backdrop-blur-xl">
		<div class="max-w-6xl mx-auto px-5 py-4 flex items-center justify-between gap-3">
			<a
				href="#start"
				class="flex items-center gap-3 group"
				on:click={onNavClick}
				aria-label="Start"
			>
				<img
					src={logo_url}
					alt="Einstein Bank Logo"
					class="h-9 w-9 rounded-xl bg-[#E34747] flex items-center justify-center font-bold text-white text-sm shadow-lg shadow-[#E34747]/30 group-hover:shadow-[#E34747]/50 transition-shadow"
				/>
				<div class="flex flex-col leading-tight">
					<span
						class="font-semibold tracking-tight text-base text-white group-hover:text-white/90 transition-colors"
						>{siteName}</span
					>
					<span class="text-[10px] uppercase tracking-[0.2em] text-white/40">Digitale Filiale</span>
				</div>
			</a>

			<!-- Desktop Nav -->
			<div class="hidden md:flex items-center gap-6 text-sm">
				<div class="flex items-center gap-6 text-white/55">
					<a href="#features" class="hover:text-white transition-colors duration-200">Funktionen</a>
					<a href="#overview" class="hover:text-white transition-colors duration-200">Überblick</a>
					<a href="#faq" class="hover:text-white transition-colors duration-200">FAQ</a>
				</div>

				<div class="h-5 w-px bg-white/10"></div>

				<a
					href="/floorplan"
					class="inline-flex items-center gap-2 rounded-full border border-white/12 bg-white/5 hover:bg-white/10 px-4 py-2 text-sm text-white/80 hover:text-white transition-all duration-200"
				>
					<svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
						<path
							stroke-linecap="round"
							stroke-linejoin="round"
							stroke-width="2"
							d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
						/>
					</svg>
					Filiale
				</a>

				{#if !loggedIn}
					<a
						href="/login"
						class="inline-flex items-center gap-2 rounded-full bg-[#E34747] hover:bg-[#F05050] px-5 py-2 text-sm font-semibold text-white shadow-md shadow-[#E34747]/25 hover:shadow-[#E34747]/40 transition-all duration-200"
					>
						Anmelden
					</a>
				{:else}
					<a
						href="/dashboard"
						class="inline-flex items-center gap-2 rounded-full bg-[#E34747] hover:bg-[#F05050] px-5 py-2 text-sm font-semibold text-white shadow-md shadow-[#E34747]/25 hover:shadow-[#E34747]/40 transition-all duration-200"
					>
						Dashboard
					</a>
				{/if}
			</div>

			<!-- Mobile -->
			<div class="md:hidden flex items-center gap-2">
				<a
					href="/floorplan"
					class="inline-flex items-center rounded-full border border-white/12 bg-white/5 px-3 py-2 text-sm text-white/80"
				>
					Filiale
				</a>
				<button
					class="p-2 rounded-xl hover:bg-white/8 transition-colors"
					aria-label="Menü öffnen"
					aria-expanded={menuOpen}
					on:click={() => (menuOpen = !menuOpen)}
				>
					{#if !menuOpen}
						<svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								stroke-width="2"
								d="M4 6h16M4 12h16M4 18h16"
							/>
						</svg>
					{:else}
						<svg class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								stroke-width="2"
								d="M6 18L18 6M6 6l12 12"
							/>
						</svg>
					{/if}
				</button>
			</div>
		</div>

		{#if menuOpen}
			<div
				class="md:hidden border-t border-white/8 bg-[#0A0B0E]/98 backdrop-blur-xl animate-fadeIn"
			>
				<div class="max-w-6xl mx-auto px-5 py-4 space-y-1 text-sm">
					<a
						href="#features"
						on:click={onNavClick}
						class="flex items-center gap-3 px-4 py-3 rounded-2xl hover:bg-white/5 text-white/70 hover:text-white transition-all"
						>Funktionen</a
					>
					<a
						href="#overview"
						on:click={onNavClick}
						class="flex items-center gap-3 px-4 py-3 rounded-2xl hover:bg-white/5 text-white/70 hover:text-white transition-all"
						>Überblick</a
					>
					<a
						href="#faq"
						on:click={onNavClick}
						class="flex items-center gap-3 px-4 py-3 rounded-2xl hover:bg-white/5 text-white/70 hover:text-white transition-all"
						>FAQ</a
					>
					<div class="pt-3 border-t border-white/8 flex flex-col gap-2">
						{#if !loggedIn}
							<a
								on:click={onNavClick}
								href="/login"
								class="flex items-center justify-center rounded-full bg-[#E34747] hover:bg-[#F05050] py-3 font-semibold text-white transition-all"
							>
								Anmelden
							</a>
						{:else}
							<a
								on:click={onNavClick}
								href="/dashboard"
								class="flex items-center justify-center rounded-full bg-[#E34747] hover:bg-[#F05050] py-3 font-semibold text-white transition-all"
							>
								Dashboard
							</a>
						{/if}
					</div>
				</div>
			</div>
		{/if}
	</nav>

	<main>
		<!-- HERO -->
		<section id="start" class="scroll-mt-20 pt-16 pb-12 md:pt-24 md:pb-16">
			<div class="max-w-6xl mx-auto px-5">
				<div class="grid gap-12 lg:grid-cols-[1fr_420px] items-center">
					<!-- Left -->
					<div class="space-y-8 text-center lg:text-left">
						<div
							class="inline-flex items-center gap-2 rounded-full border border-[#E34747]/30 bg-[#E34747]/10 px-4 py-1.5 text-xs font-medium text-[#E34747]"
						>
							<span class="h-1.5 w-1.5 rounded-full bg-[#E34747] animate-pulse"></span>
							Schule · Alltag · Projekte
						</div>

						<div class="space-y-5">
							<h1
								class="text-4xl sm:text-5xl lg:text-6xl font-bold tracking-tight text-white leading-[1.08]"
							>
								Dein digitales<br />
								<span class="text-[#E34747]">Bankkonto.</span>
							</h1>
							<p
								class="text-base sm:text-lg text-white/50 max-w-lg mx-auto lg:mx-0 leading-relaxed"
							>
								{tagline} Eine moderne Banking-Oberfläche für Simulationen, Schulprojekte und Workshops.
							</p>
						</div>

						<div class="flex flex-col sm:flex-row gap-3 justify-center lg:justify-start">
							{#if !loggedIn}
								<a
									href="/login"
									class="inline-flex items-center justify-center gap-2 rounded-full bg-[#E34747] hover:bg-[#F05050] px-8 py-3.5 text-base font-semibold text-white shadow-xl shadow-[#E34747]/20 hover:shadow-[#E34747]/35 transition-all duration-200 w-full sm:w-auto"
								>
									Jetzt einloggen
									<svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
										<path
											stroke-linecap="round"
											stroke-linejoin="round"
											stroke-width="2.5"
											d="M13 7l5 5m0 0l-5 5m5-5H6"
										/>
									</svg>
								</a>
							{:else}
								<a
									href="/dashboard"
									class="inline-flex items-center justify-center gap-2 rounded-full bg-[#E34747] hover:bg-[#F05050] px-8 py-3.5 text-base font-semibold text-white shadow-xl shadow-[#E34747]/20 hover:shadow-[#E34747]/35 transition-all duration-200 w-full sm:w-auto"
								>
									Dashboard öffnen
									<svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
										<path
											stroke-linecap="round"
											stroke-linejoin="round"
											stroke-width="2.5"
											d="M13 7l5 5m0 0l-5 5m5-5H6"
										/>
									</svg>
								</a>
							{/if}
							<a
								href="/floorplan"
								class="font-bold inline-flex items-center justify-center gap-2 rounded-full border border-white/12 bg-white/5 hover:bg-white/10 px-8 py-3.5 text-base text-white/80 hover:text-white transition-all duration-200 w-full sm:w-auto"
							>
								Gebäudeplan
							</a>
						</div>

						<p class="text-[11px] text-white/30">
							Demo-Oberfläche. Keine echten Kontodaten auf der Landingpage.
						</p>
					</div>

					<!-- Right: Demo Card -->
					<div class="flex justify-center lg:justify-end">
						<div
							class="w-full max-w-sm rounded-4xl border border-white/10 bg-[#11131A] shadow-2xl shadow-black/50 overflow-hidden"
						>
							<!-- Card Header -->
							<div
								class="px-5 py-4 border-b border-white/8 flex items-center justify-between bg-white/3"
							>
								<div class="flex items-center gap-2.5">
									<img
										src={logo_url}
										alt="Einstein Bank Logo"
										class="h-9 w-9 rounded-xl bg-[#E34747] flex items-center justify-center font-bold text-white text-sm shadow-lg shadow-[#E34747]/30 group-hover:shadow-[#E34747]/50 transition-shadow"
									/>
									<div>
										<p class="text-sm font-semibold text-white">Max Mustermann</p>
										<p class="text-[11px] text-white/40">DE89 3704 0044 0532 0130 00</p>
									</div>
								</div>
								<span
									class="inline-flex items-center gap-1 rounded-full bg-emerald-500/15 border border-emerald-500/20 px-2.5 py-1 text-[10px] font-medium text-emerald-400"
								>
									<span class="h-1.5 w-1.5 rounded-full bg-emerald-400"></span>
									Aktiv
								</span>
							</div>

							<!-- Balance -->
							<div class="px-5 pt-5 pb-4">
								<p class="text-[11px] uppercase tracking-[0.2em] text-white/35 mb-1">Kontostand</p>
								<p class="text-3xl font-bold text-white tracking-tight">2.340,17 €</p>
								<p class="mt-1 text-xs text-emerald-400 flex items-center gap-1">
									<svg class="h-3 w-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
										<path
											stroke-linecap="round"
											stroke-linejoin="round"
											stroke-width="2.5"
											d="M5 10l7-7m0 0l7 7m-7-7v18"
										/>
									</svg>
									+50,00 € diese Woche
								</p>
							</div>

							<!-- Transactions -->
							<div class="px-5 pb-5 space-y-2">
								<p class="text-[11px] uppercase tracking-[0.18em] text-white/35 mb-3">
									Letzte Bewegungen
								</p>

								<div class="flex items-center justify-between py-2.5 border-b border-white/6">
									<div class="flex items-center gap-3">
										<div
											class="h-8 w-8 rounded-xl bg-emerald-500/10 border border-emerald-500/15 flex items-center justify-center"
										>
											<svg
												class="h-3.5 w-3.5 text-emerald-400"
												fill="none"
												stroke="currentColor"
												viewBox="0 0 24 24"
											>
												<path
													stroke-linecap="round"
													stroke-linejoin="round"
													stroke-width="2.5"
													d="M19 14l-7 7m0 0l-7-7m7 7V3"
												/>
											</svg>
										</div>
										<div>
											<p class="text-xs font-medium text-white">Taschengeld</p>
											<p class="text-[10px] text-white/35">Heute</p>
										</div>
									</div>
									<p class="text-xs font-semibold text-emerald-400">+50,00 €</p>
								</div>

								<div class="flex items-center justify-between py-2.5 border-b border-white/6">
									<div class="flex items-center gap-3">
										<div
											class="h-8 w-8 rounded-xl bg-[#E34747]/10 border border-[#E34747]/15 flex items-center justify-center"
										>
											<svg
												class="h-3.5 w-3.5 text-[#E34747]"
												fill="none"
												stroke="currentColor"
												viewBox="0 0 24 24"
											>
												<path
													stroke-linecap="round"
													stroke-linejoin="round"
													stroke-width="2.5"
													d="M5 10l7-7m0 0l7 7m-7-7v18"
												/>
											</svg>
										</div>
										<div>
											<p class="text-xs font-medium text-white">Schulshop</p>
											<p class="text-[10px] text-white/35">Gestern</p>
										</div>
									</div>
									<p class="text-xs font-semibold text-[#E34747]">-12,50 €</p>
								</div>

								<div class="flex items-center justify-between py-2.5">
									<div class="flex items-center gap-3">
										<div
											class="h-8 w-8 rounded-xl bg-emerald-500/10 border border-emerald-500/15 flex items-center justify-center"
										>
											<svg
												class="h-3.5 w-3.5 text-emerald-400"
												fill="none"
												stroke="currentColor"
												viewBox="0 0 24 24"
											>
												<path
													stroke-linecap="round"
													stroke-linejoin="round"
													stroke-width="2.5"
													d="M19 14l-7 7m0 0l-7-7m7 7V3"
												/>
											</svg>
										</div>
										<div>
											<p class="text-xs font-medium text-white">Überweisung</p>
											<p class="text-[10px] text-white/35">Mo, 12. Mai</p>
										</div>
									</div>
									<p class="text-xs font-semibold text-emerald-400">+100,00 €</p>
								</div>
							</div>

							<!-- Card Actions -->
							<div class="px-5 pb-5">
								<div class="grid grid-cols-2 gap-2">
									<a
										href="/floorplan"
										class="flex items-center justify-center gap-1.5 rounded-xl border border-white/10 bg-white/5 hover:bg-white/10 py-2.5 text-xs text-white/70 hover:text-white transition-all"
									>
										<svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												stroke-width="2"
												d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
											/>
										</svg>
										Filiale
									</a>
									<a
										href="#faq"
										class="flex items-center justify-center gap-1.5 rounded-xl border border-white/10 bg-white/5 hover:bg-white/10 py-2.5 text-xs text-white/70 hover:text-white transition-all"
									>
										<svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												stroke-width="2"
												d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
											/>
										</svg>
										Hilfe
									</a>
								</div>
							</div>
						</div>
					</div>
				</div>

				<!-- Stats Bar -->
				<div
					class="mt-14 grid grid-cols-2 md:grid-cols-4 gap-px rounded-2xl overflow-hidden border border-white/8 bg-white/8"
				>
					<div class="bg-[#0A0B0E] px-5 py-4 text-center">
						<p class="text-xl md:text-2xl font-bold text-white">100%</p>
						<p class="mt-0.5 text-[11px] text-white/40 uppercase tracking-wider">Kostenlos</p>
					</div>
					<div class="bg-[#0A0B0E] px-5 py-4 text-center">
						<p class="text-xl md:text-2xl font-bold text-white">3</p>
						<p class="mt-0.5 text-[11px] text-white/40 uppercase tracking-wider">Module</p>
					</div>
					<div class="bg-[#0A0B0E] px-5 py-4 text-center">
						<p class="text-xl md:text-2xl font-bold text-white">1</p>
						<p class="mt-0.5 text-[11px] text-white/40 uppercase tracking-wider">Filiale</p>
					</div>
					<div class="bg-[#0A0B0E] px-5 py-4 text-center">
						<p class="text-xl md:text-2xl font-bold text-[#E34747]">Live</p>
						<p class="mt-0.5 text-[11px] text-white/40 uppercase tracking-wider">Echtzeit</p>
					</div>
				</div>
			</div>
		</section>

		<!-- FEATURES -->
		<section id="features" class="scroll-mt-20 py-16 border-t border-white/8">
			<div class="max-w-6xl mx-auto px-5">
				<div class="flex flex-col sm:flex-row sm:items-end justify-between gap-4 mb-10">
					<div class="space-y-2">
						<p class="text-xs uppercase tracking-[0.2em] text-[#E34747] font-medium">
							Was du bekommst
						</p>
						<h2 class="text-2xl md:text-3xl font-bold tracking-tight text-white">
							Alle Funktionen. Kein Overhead.
						</h2>
						<p class="text-sm text-white/45 max-w-md">
							Klar strukturiert, sofort verständlich — für Einsteiger und Fortgeschrittene.
						</p>
					</div>
					<a
						href="/floorplan"
						class="hidden sm:inline-flex items-center gap-2 rounded-full border border-white/12 bg-white/5 hover:bg-white/10 px-5 py-2.5 text-sm text-white/70 hover:text-white transition-all"
					>
						Filiale öffnen
						<svg class="h-3.5 w-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								stroke-width="2"
								d="M13 7l5 5m0 0l-5 5m5-5H6"
							/>
						</svg>
					</a>
				</div>

				<div class="grid gap-4 md:grid-cols-3">
					<!-- Feature 1 -->
					<div
						class="group rounded-3xl border border-white/8 bg-white/3 hover:bg-white/6 hover:border-white/15 p-6 transition-all duration-300"
					>
						<div
							class="mb-5 h-11 w-11 rounded-2xl bg-[#E34747]/10 border border-[#E34747]/20 flex items-center justify-center group-hover:bg-[#E34747]/15 transition-colors"
						>
							<svg
								class="h-5 w-5 text-[#E34747]"
								fill="none"
								stroke="currentColor"
								viewBox="0 0 24 24"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									stroke-width="2"
									d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4"
								/>
							</svg>
						</div>
						<h3 class="text-base font-semibold text-white mb-2">Überweisen</h3>
						<p class="text-sm text-white/50 leading-relaxed">
							Senden, empfangen, Verlauf filtern. Alles in einem klaren Interface.
						</p>
					</div>

					<!-- Feature 2 -->
					<div
						class="group rounded-3xl border border-white/8 bg-white/3 hover:bg-white/6 hover:border-white/15 p-6 transition-all duration-300"
					>
						<div
							class="mb-5 h-11 w-11 rounded-2xl bg-[#E34747]/10 border border-[#E34747]/20 flex items-center justify-center group-hover:bg-[#E34747]/15 transition-colors"
						>
							<svg
								class="h-5 w-5 text-[#E34747]"
								fill="none"
								stroke="currentColor"
								viewBox="0 0 24 24"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									stroke-width="2"
									d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"
								/>
							</svg>
						</div>
						<h3 class="text-base font-semibold text-white mb-2">Karten</h3>
						<p class="text-sm text-white/50 leading-relaxed">
							Status, Sperre, Limits — alles schnell erreichbar und verständlich.
						</p>
					</div>

					<!-- Feature 3 -->
					<div
						class="group rounded-3xl border border-white/8 bg-white/3 hover:bg-white/6 hover:border-white/15 p-6 transition-all duration-300"
					>
						<div
							class="mb-5 h-11 w-11 rounded-2xl bg-[#E34747]/10 border border-[#E34747]/20 flex items-center justify-center group-hover:bg-[#E34747]/15 transition-colors"
						>
							<svg
								class="h-5 w-5 text-[#E34747]"
								fill="none"
								stroke="currentColor"
								viewBox="0 0 24 24"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									stroke-width="2"
									d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z"
								/>
							</svg>
						</div>
						<h3 class="text-base font-semibold text-white mb-2">Shops</h3>
						<p class="text-sm text-white/50 leading-relaxed">
							Produkte, Warenkorb, Bestellverwaltung. Ideal für Schulprojekte.
						</p>
					</div>
				</div>
			</div>
		</section>

		<!-- OVERVIEW -->
		<section id="overview" class="scroll-mt-20 py-16 border-t border-white/8">
			<div class="max-w-6xl mx-auto px-5">
				<div class="grid gap-8 lg:grid-cols-2 items-start">
					<div class="space-y-6">
						<div class="space-y-2">
							<p class="text-xs uppercase tracking-[0.2em] text-[#E34747] font-medium">Überblick</p>
							<h2 class="text-2xl md:text-3xl font-bold tracking-tight text-white">
								Gemacht für Lernbetrieb.
							</h2>
							<p class="text-sm md:text-base text-white/50 max-w-md leading-relaxed">
								{siteName} ist eine schlanke Banking-Oberfläche für Schulen und Workshops. Du findest
								alles in wenigen Klicks — ohne lange Einarbeitung.
							</p>
						</div>

						<div class="grid gap-3 sm:grid-cols-2">
							<div class="rounded-2xl border border-white/8 bg-white/3 p-4 space-y-1.5">
								<div class="flex items-center gap-2">
									<div class="h-6 w-6 rounded-lg bg-[#E34747]/15 flex items-center justify-center">
										<svg
											class="h-3 w-3 text-[#E34747]"
											fill="none"
											stroke="currentColor"
											viewBox="0 0 24 24"
										>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												stroke-width="2.5"
												d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
											/>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												stroke-width="2.5"
												d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"
											/>
										</svg>
									</div>
									<p class="text-xs font-semibold text-white/70 uppercase tracking-wider">UI</p>
								</div>
								<p class="text-sm text-white/70">Klare Hierarchie. Wenig Ablenkung.</p>
							</div>
							<div class="rounded-2xl border border-white/8 bg-white/3 p-4 space-y-1.5">
								<div class="flex items-center gap-2">
									<div class="h-6 w-6 rounded-lg bg-[#E34747]/15 flex items-center justify-center">
										<svg
											class="h-3 w-3 text-[#E34747]"
											fill="none"
											stroke="currentColor"
											viewBox="0 0 24 24"
										>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												stroke-width="2.5"
												d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
											/>
										</svg>
									</div>
									<p class="text-xs font-semibold text-white/70 uppercase tracking-wider">
										Sicherheit
									</p>
								</div>
								<p class="text-sm text-white/70">Schalter statt versteckte Einstellungen.</p>
							</div>
						</div>
					</div>

					<!-- Checklist Card -->
					<div
						class="rounded-3xl border border-white/10 bg-linear-to-br from-[#E34747]/8 via-white/3 to-transparent p-6 md:p-7"
					>
						<p class="text-[11px] uppercase tracking-[0.2em] text-white/40 mb-5">
							Was dich erwartet
						</p>
						<ul class="space-y-4">
							{#each ['Klare Kontobewegungen mit Filter und Suche.', 'Kartenstatus und Schutzoptionen in einem Screen.', 'Digitaler Filial-Grundriss zur Orientierung.', 'Echtzeit-Synchronisation über alle Geräte.'] as item}
								<li class="flex items-start gap-3">
									<div
										class="mt-0.5 h-5 w-5 shrink-0 rounded-full bg-[#E34747]/15 border border-[#E34747]/25 flex items-center justify-center"
									>
										<svg
											class="h-3 w-3 text-[#E34747]"
											fill="none"
											stroke="currentColor"
											viewBox="0 0 24 24"
										>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												stroke-width="3"
												d="M5 13l4 4L19 7"
											/>
										</svg>
									</div>
									<span class="text-sm text-white/65">{item}</span>
								</li>
							{/each}
						</ul>

						<div class="mt-7 pt-5 border-t border-white/8">
							<a
								href="/floorplan"
								class="flex items-center justify-center gap-2 rounded-full border border-white/12 bg-white/5 hover:bg-white/10 py-3 text-sm text-white/70 hover:text-white transition-all"
							>
								<svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
									<path
										stroke-linecap="round"
										stroke-linejoin="round"
										stroke-width="2"
										d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
									/>
								</svg>
								Grundriss öffnen
							</a>
						</div>
					</div>
				</div>
			</div>
		</section>

		<!-- FAQ -->
		<section id="faq" class="scroll-mt-20 py-16 border-t border-white/8">
			<div class="max-w-3xl mx-auto px-5">
				<div class="text-center space-y-2 mb-10">
					<p class="text-xs uppercase tracking-[0.2em] text-[#E34747] font-medium">
						Häufige Fragen
					</p>
					<h2 class="text-2xl md:text-3xl font-bold tracking-tight text-white">FAQ</h2>
					<p class="text-sm text-white/45">Kurz und direkt beantwortet.</p>
				</div>

				<div class="space-y-2">
					<details
						class="group rounded-2xl border border-white/8 bg-white/3 hover:border-white/12 transition-colors"
					>
						<summary
							class="cursor-pointer list-none flex items-center justify-between gap-4 px-5 py-4"
						>
							<span class="text-sm font-semibold text-white">Sind das echte Bankdaten?</span>
							<span
								class="h-6 w-6 shrink-0 rounded-full border border-white/15 bg-white/5 flex items-center justify-center text-white/50 group-open:rotate-45 transition-transform duration-200 text-base leading-none"
								>+</span
							>
						</summary>
						<div class="px-5 pb-4">
							<p class="text-sm text-white/50 leading-relaxed">
								Nein. Die Landingpage zeigt ausschließlich Demo-Inhalte. Echte Kontodaten sind nur
								nach dem Login sichtbar.
							</p>
						</div>
					</details>

					<details
						class="group rounded-2xl border border-white/8 bg-white/3 hover:border-white/12 transition-colors"
					>
						<summary
							class="cursor-pointer list-none flex items-center justify-between gap-4 px-5 py-4"
						>
							<span class="text-sm font-semibold text-white">Für wen ist {siteName}?</span>
							<span
								class="h-6 w-6 shrink-0 rounded-full border border-white/15 bg-white/5 flex items-center justify-center text-white/50 group-open:rotate-45 transition-transform duration-200 text-base leading-none"
								>+</span
							>
						</summary>
						<div class="px-5 pb-4">
							<p class="text-sm text-white/50 leading-relaxed">
								Für Schulprojekte, Simulationen, Workshops und alle, die Banking-Abläufe praktisch
								kennenlernen möchten.
							</p>
						</div>
					</details>

					<details
						class="group rounded-2xl border border-white/8 bg-white/3 hover:border-white/12 transition-colors"
					>
						<summary
							class="cursor-pointer list-none flex items-center justify-between gap-4 px-5 py-4"
						>
							<span class="text-sm font-semibold text-white">Wo finde ich die Filiale?</span>
							<span
								class="h-6 w-6 shrink-0 rounded-full border border-white/15 bg-white/5 flex items-center justify-center text-white/50 group-open:rotate-45 transition-transform duration-200 text-base leading-none"
								>+</span
							>
						</summary>
						<div class="px-5 pb-4">
							<p class="text-sm text-white/50 leading-relaxed">
								Als digitaler Grundriss unter dem Menüpunkt "Filiale" — erreichbar ohne Login.
							</p>
						</div>
					</details>

					<details
						class="group rounded-2xl border border-white/8 bg-white/3 hover:border-white/12 transition-colors"
					>
						<summary
							class="cursor-pointer list-none flex items-center justify-between gap-4 px-5 py-4"
						>
							<span class="text-sm font-semibold text-white">Kostet die Nutzung etwas?</span>
							<span
								class="h-6 w-6 shrink-0 rounded-full border border-white/15 bg-white/5 flex items-center justify-center text-white/50 group-open:rotate-45 transition-transform duration-200 text-base leading-none"
								>+</span
							>
						</summary>
						<div class="px-5 pb-4">
							<p class="text-sm text-white/50 leading-relaxed">
								Nein. {siteName} ist vollständig kostenlos für Schulen und Bildungseinrichtungen.
							</p>
						</div>
					</details>
				</div>
			</div>
		</section>

		<!-- CTA -->
		<section id="contact" class="scroll-mt-20 py-16 border-t border-white/8">
			<div class="max-w-5xl mx-auto px-5">
				<div class="relative rounded-[2rem] border border-white/10 overflow-hidden">
					<!-- CTA background -->
					<div
						class="absolute inset-0 bg-linear-to-br from-[#E34747]/12 via-[#E34747]/5 to-transparent"
					></div>
					<div
						class="absolute inset-0 bg-[radial-gradient(ellipse_at_top_left,rgba(227,71,71,0.15),transparent_60%)]"
					></div>
					<div
						class="absolute top-0 left-0 w-48 h-48 rounded-full bg-[#E34747]/10 blur-[60px]"
					></div>

					<div class="relative px-7 py-12 md:px-12 md:py-14 text-center">
						<div
							class="inline-flex items-center gap-2 rounded-full border border-[#E34747]/30 bg-[#E34747]/10 px-4 py-1.5 text-xs font-medium text-[#E34747] mb-5"
						>
							<span class="h-1.5 w-1.5 rounded-full bg-[#E34747] animate-pulse"></span>
							Jetzt starten
						</div>
						<h2 class="text-2xl md:text-4xl font-bold tracking-tight text-white mb-3">
							Bereit loszulegen?
						</h2>
						<p class="text-sm md:text-base text-white/50 max-w-lg mx-auto mb-8 leading-relaxed">
							Login und sofort Zugriff auf Dashboard, Karten, Überweisungen und mehr.
						</p>

						<div class="flex flex-col sm:flex-row justify-center gap-3 sm:gap-4">
							{#if !loggedIn}
								<a
									href="/login"
									class="inline-flex items-center justify-center gap-2 rounded-full bg-[#E34747] hover:bg-[#F05050] px-10 py-3.5 text-base font-semibold text-white shadow-xl shadow-[#E34747]/25 hover:shadow-[#E34747]/40 transition-all duration-200 w-full sm:w-auto"
								>
									Anmelden
									<svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
										<path
											stroke-linecap="round"
											stroke-linejoin="round"
											stroke-width="2.5"
											d="M13 7l5 5m0 0l-5 5m5-5H6"
										/>
									</svg>
								</a>
							{:else}
								<a
									href="/dashboard"
									class="inline-flex items-center justify-center gap-2 rounded-full bg-[#E34747] hover:bg-[#F05050] px-10 py-3.5 text-base font-semibold text-white shadow-xl shadow-[#E34747]/25 hover:shadow-[#E34747]/40 transition-all duration-200 w-full sm:w-auto"
								>
									Dashboard
									<svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
										<path
											stroke-linecap="round"
											stroke-linejoin="round"
											stroke-width="2.5"
											d="M13 7l5 5m0 0l-5 5m5-5H6"
										/>
									</svg>
								</a>
							{/if}
							<a
								href="mailto:{contactEmail}"
								class="inline-flex items-center justify-center gap-2 rounded-full border border-white/12 bg-white/5 hover:bg-white/10 px-10 py-3.5 text-base text-white/70 hover:text-white transition-all duration-200 w-full sm:w-auto"
							>
								Kontakt
							</a>
						</div>

						<p class="mt-6 text-[11px] text-white/30">
							Fiktive Oberfläche für Lern- und Demo-Zwecke.
						</p>
					</div>
				</div>
			</div>
		</section>
	</main>

	<!-- FOOTER -->
	<footer class="py-8 border-t border-white/8 bg-black/30">
		<div
			class="max-w-6xl mx-auto px-5 flex flex-col md:flex-row items-center justify-between gap-4"
		>
			<div class="flex items-center gap-3">
				<img
					src={logo_url}
					alt="Einstein Bank Logo"
					class="h-6 w-6 rounded-lg bg-[#E34747] flex items-center justify-center font-bold text-white text-sm shadow-lg shadow-[#E34747]/30 group-hover:shadow-[#E34747]/50 transition-shadow"
				/>
				<span class="text-xs text-white/35">© {new Date().getFullYear()} {siteName}</span>
			</div>

			<div class="flex flex-wrap justify-center gap-5 text-xs text-white/35">
				<a href="#start" class="hover:text-white/70 transition-colors" on:click={onNavClick}
					>Start</a
				>
				<a href="#features" class="hover:text-white/70 transition-colors" on:click={onNavClick}
					>Funktionen</a
				>
				<a href="#overview" class="hover:text-white/70 transition-colors" on:click={onNavClick}
					>Überblick</a
				>
				<a href="/policies/imprint" class="hover:text-white/70 transition-colors">Impressum</a>
				<a href="/policies/special-thanks" class="hover:text-white/70 transition-colors">Danke an</a
				>
			</div>
		</div>
	</footer>
</div>

<style>
	.ui-noselect {
		user-select: none;
	}

	:global(.select-text) {
		user-select: text;
	}

	.animate-fadeIn {
		animation: fadeIn 0.18s ease-out;
	}

	@keyframes fadeIn {
		from {
			opacity: 0;
			transform: translateY(-6px);
		}
		to {
			opacity: 1;
			transform: translateY(0);
		}
	}
</style>
