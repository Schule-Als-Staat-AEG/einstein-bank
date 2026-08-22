<script lang="ts">
	import { onMount, tick } from 'svelte';
	import {
		ArrowLeft,
		Building2,
		ChevronRight,
		LocateFixed,
		MapPin,
		Minus,
		Plus,
		RotateCcw,
		Search,
		X
	} from '@lucide/svelte';

	import Raumplan1 from '$lib/assets/images/Raumplan_1.svg';
	import Raumplan2 from '$lib/assets/images/Raumplan_2.svg';
	import rooms from '$lib/rooms.json';
	import { goto } from '$app/navigation';

	type FloorId = 1 | 2;

	type Floor = {
		id: FloorId;
		name: string;
		image: string;
	};

	type CompanyLocation = {
		id: string;
		name: string;
		room: string;
		floor: FloorId;
		x: number;
		y: number;
		aliases?: string[];
		description?: string;
	};

	type SvgBounds = {
		minX: number;
		minY: number;
		width: number;
		height: number;
	};

	const floors: Floor[] = [
		{ id: 1, name: 'Etage 1', image: Raumplan1 },
		{ id: 2, name: 'Etage 2', image: Raumplan2 }
	];

	const companies = rooms as CompanyLocation[];

	/**
	 * Festes Koordinatensystem der beiden Raumplan-SVGs.
	 *
	 * Die x-/y-Werte in rooms.json müssen innerhalb dieser Grenzen liegen:
	 * Etage 1: x = 0–297, y = 0–210
	 * Etage 2: x = 0–297, y = 0–148.7
	 */
	const boundsByFloor: Record<FloorId, SvgBounds> = {
		1: {
			minX: 0,
			minY: 0,
			width: 297,
			height: 210
		},
		2: {
			minX: 0,
			minY: 0,
			width: 297,
			height: 148.7
		}
	};

	let activeFloor: FloorId = 1;
	let selectedCompany: CompanyLocation | null = null;

	let searchTerm = '';
	let searchFocused = false;
	let activeSearchResult = -1;
	let searchInput: HTMLInputElement;

	let zoom = 1;
	const minimumZoom = 1;
	const maximumZoom = 8;
	const zoomStep = 0.5;

	let planViewport: HTMLDivElement;
	let planSvg: SVGSVGElement;

	let isPanning = false;
	let pointerStartX = 0;
	let pointerStartY = 0;
	let scrollStartLeft = 0;
	let scrollStartTop = 0;

	let pastPage = "";

	$: currentFloor = floors.find((floor) => floor.id === activeFloor) ?? floors[0];
	$: currentBounds = boundsByFloor[activeFloor];
	$: currentViewBox = `${currentBounds.minX} ${currentBounds.minY} ${currentBounds.width} ${currentBounds.height}`;
	$: floorCompanies = companies.filter((company) => company.floor === activeFloor);
	$: normalizedSearch = normalize(searchTerm);
	$: filteredCompanies = normalizedSearch
		? companies
				.filter((company) =>
					[company.name, company.room, ...(company.aliases ?? [])].some((value) =>
						normalize(value).includes(normalizedSearch)
					)
				)
				.slice(0, 8)
		: [];
	$: markerRadius = Math.max(currentBounds.width * 0.009, 5);


	function normalize(value: string) {
		return value
			.toLocaleLowerCase('de-DE')
			.trim()
			.normalize('NFD')
			.replace(/\p{Diacritic}/gu, '');
	}


	async function changeFloor(floorId: FloorId) {
		activeFloor = floorId;
		if (selectedCompany?.floor !== floorId) selectedCompany = null;
		await resetView();
	}

	async function selectCompany(company: CompanyLocation) {
		selectedCompany = company;
		searchTerm = company.name;
		searchFocused = false;
		activeSearchResult = -1;

		if (activeFloor !== company.floor) {
			activeFloor = company.floor;
			await tick();
		}

		if (zoom < 1.6) {
			zoom = 1.6;
			await tick();
		}

		await centerOnCompany(company);
	}

	async function centerOnCompany(company: CompanyLocation) {
		await tick();
		if (!planViewport || !planSvg || company.floor !== activeFloor) return;

		const bounds = boundsByFloor[company.floor];
		const targetX = ((company.x - bounds.minX) / bounds.width) * planSvg.clientWidth;
		const targetY = ((company.y - bounds.minY) / bounds.height) * planSvg.clientHeight;

		planViewport.scrollTo({
			left: targetX - planViewport.clientWidth / 2,
			top: targetY - planViewport.clientHeight / 2,
			behavior: 'smooth'
		});
	}

	async function centerSelectedCompany() {
		if (selectedCompany) await centerOnCompany(selectedCompany);
	}

	async function setZoom(nextZoom: number) {
		const clamped = Math.min(maximumZoom, Math.max(minimumZoom, nextZoom));
		if (clamped === zoom) return;

		if (!planViewport) {
			zoom = clamped;
			return;
		}

		const oldWidth = Math.max(planViewport.scrollWidth, 1);
		const oldHeight = Math.max(planViewport.scrollHeight, 1);
		const centerRatioX = (planViewport.scrollLeft + planViewport.clientWidth / 2) / oldWidth;
		const centerRatioY = (planViewport.scrollTop + planViewport.clientHeight / 2) / oldHeight;

		zoom = clamped;
		await tick();

		planViewport.scrollTo({
			left: centerRatioX * planViewport.scrollWidth - planViewport.clientWidth / 2,
			top: centerRatioY * planViewport.scrollHeight - planViewport.clientHeight / 2
		});
	}

	async function resetView() {
		zoom = 1;
		await tick();
		planViewport?.scrollTo({ left: 0, top: 0, behavior: 'smooth' });
	}

	function clearSearch() {
		searchTerm = '';
		selectedCompany = null;
		activeSearchResult = -1;
		searchInput?.focus();
	}

	function handleSearchFocusOut(event: FocusEvent) {
		const section = event.currentTarget as HTMLElement;
		const nextTarget = event.relatedTarget as Node | null;

		if (!nextTarget || !section.contains(nextTarget)) {
			searchFocused = false;
			activeSearchResult = -1;
		}
	}

	function handleSearchKeydown(event: KeyboardEvent) {
		if (!filteredCompanies.length) {
			if (event.key === 'Escape') {
				searchFocused = false;
				searchInput?.blur();
			}
			return;
		}

		if (event.key === 'ArrowDown') {
			event.preventDefault();
			activeSearchResult = (activeSearchResult + 1) % filteredCompanies.length;
		}

		if (event.key === 'ArrowUp') {
			event.preventDefault();
			activeSearchResult =
				activeSearchResult <= 0 ? filteredCompanies.length - 1 : activeSearchResult - 1;
		}

		if (event.key === 'Enter') {
			event.preventDefault();
			const company = filteredCompanies[activeSearchResult >= 0 ? activeSearchResult : 0];
			if (company) void selectCompany(company);
		}

		if (event.key === 'Escape') {
			searchFocused = false;
			activeSearchResult = -1;
			searchInput?.blur();
		}
	}

	function handleMarkerKeydown(event: KeyboardEvent, company: CompanyLocation) {
		if (event.key === 'Enter' || event.key === ' ') {
			event.preventDefault();
			void selectCompany(company);
		}
	}

	function startPan(event: PointerEvent) {
		if (event.button !== 0 || !planViewport) return;

		const target = event.target as Element;
		if (target.closest('[data-map-control]') || target.closest('[data-company-marker]')) return;

		isPanning = true;
		pointerStartX = event.clientX;
		pointerStartY = event.clientY;
		scrollStartLeft = planViewport.scrollLeft;
		scrollStartTop = planViewport.scrollTop;
		planViewport.setPointerCapture(event.pointerId);
	}

	function movePan(event: PointerEvent) {
		if (!isPanning || !planViewport) return;
		event.preventDefault();
		planViewport.scrollLeft = scrollStartLeft - (event.clientX - pointerStartX);
		planViewport.scrollTop = scrollStartTop - (event.clientY - pointerStartY);
	}

	function stopPan(event: PointerEvent) {
		if (!isPanning || !planViewport) return;
		isPanning = false;
		if (planViewport.hasPointerCapture(event.pointerId)) {
			planViewport.releasePointerCapture(event.pointerId);
		}
	}
	onMount(() => {
		let queryRefer = new URLSearchParams(window.location.search).get("refer");
		if (queryRefer) {
			pastPage = queryRefer;
		}
	});
</script>

<svelte:head>
	<title>Raumplan</title>
	<meta name="description" content="Interaktiver Raumplan mit Unternehmenssuche" />
</svelte:head>

<div class="min-h-screen bg-base-200 px-3 py-4 sm:px-6 sm:py-6">
	<div class="mx-auto flex w-full max-w-[1600px] flex-col gap-4">
		<header class="flex flex-col gap-3 rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between sm:p-6">
			<div class="flex items-center gap-3">
				<div class="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-content">
					<Building2 size={26} />
				</div>
				<div>
					<h1 class="text-2xl font-bold sm:text-3xl">Raumplan</h1>
					<p class="text-sm text-base-content/60">Salve! Hier können Sie die Räume unserer Schule finden und nach Orten suchen</p>
				</div>
			</div>

			<div class="flex flex-wrap items-center gap-2">
				<div class="flex items-center gap-2 text-sm text-base-content/60">
					<MapPin size={17} />
					<span>{companies.length} Standorte eingetragen</span>
				</div>

				<button on:click={() => { pastPage != "" ? goto(pastPage) : goto("/")}} class="btn btn-ghost border border-base-300">
					<ArrowLeft size={18} />
					{ pastPage != "" ? "Zurück" : "Startseite" }
				</button>
			</div>
		</header>

		<div class="grid gap-4 lg:grid-cols-[340px_minmax(0,1fr)]">
			<aside class="flex flex-col gap-4">
				<section
					class="relative rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm"
					on:focusout={handleSearchFocusOut}
				>
					<label for="company-search" class="mb-2 block text-sm font-semibold">Unternehmen suchen</label>

					<div class="relative">
						<Search size={19} class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-base-content/50" />
						<input
							bind:this={searchInput}
							id="company-search"
							type="search"
							placeholder="Name oder Raumnummer"
							class="input input-bordered w-full pl-10 pr-10"
							bind:value={searchTerm}
							autocomplete="off"
							on:focus={() => (searchFocused = true)}
							on:input={() => {
								searchFocused = true;
								activeSearchResult = -1;
							}}
							on:keydown={handleSearchKeydown}
						/>

						{#if searchTerm}
							<button type="button" class="btn btn-circle btn-ghost btn-sm absolute right-1.5 top-1/2 -translate-y-1/2" aria-label="Suche leeren" on:click={clearSearch}>
								<X size={17} />
							</button>
						{/if}
					</div>

					{#if searchFocused && normalizedSearch}
						<div class="absolute left-4 right-4 top-[calc(100%-8px)] z-30 max-h-80 overflow-y-auto rounded-xl border border-base-300 bg-base-100 p-2 shadow-xl">
							{#if filteredCompanies.length}
								{#each filteredCompanies as company, index (company.id)}
									<button
										type="button"
										class={[
											'flex w-full items-center gap-3 rounded-lg px-3 py-3 text-left transition',
											index === activeSearchResult ? 'bg-primary text-primary-content' : 'hover:bg-base-200'
										].join(' ')}
										on:mousedown={(event) => event.preventDefault()}
										on:click={() => selectCompany(company)}
									>
										<div class={[
											'flex size-9 shrink-0 items-center justify-center rounded-full',
											index === activeSearchResult ? 'bg-primary-content/20' : 'bg-primary/10 text-primary'
										].join(' ')}>
											<MapPin size={18} />
										</div>

										<div class="min-w-0 flex-1">
											<div class="truncate font-semibold">{company.name}</div>
											<div class={[
												'text-sm',
												index === activeSearchResult ? 'text-primary-content/75' : 'text-base-content/60'
											].join(' ')}>
												{company.room} · Etage {company.floor}
											</div>
										</div>
										<ChevronRight size={18} class="shrink-0" />
									</button>
								{/each}
							{:else}
								<div class="px-3 py-5 text-center">
									<p class="font-semibold">Kein Unternehmen gefunden</p>
									<p class="mt-1 text-sm text-base-content/60">Prüfe den Namen oder suche nach einer Raumnummer.</p>
								</div>
							{/if}
						</div>
					{/if}
				</section>

				<section class="rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm">
					<h2 class="mb-3 text-sm font-semibold">Etage auswählen</h2>
					<div class="grid grid-cols-2 gap-2">
						{#each floors as floor (floor.id)}
							<button
								type="button"
								class={[
									'btn h-auto min-h-14 flex-col gap-0.5',
									activeFloor === floor.id ? 'btn-primary' : 'btn-ghost border border-base-300'
								].join(' ')}
								on:click={() => changeFloor(floor.id)}
							>
								<span class="text-base">{floor.name}</span>
								<span class={[
									'text-xs font-normal',
									activeFloor === floor.id ? 'text-primary-content/70' : 'text-base-content/50'
								].join(' ')}>
									{companies.filter((company) => company.floor === floor.id).length} Unternehmen
								</span>
							</button>
						{/each}
					</div>
				</section>

				{#if selectedCompany}
					<section class="rounded-2xl border border-primary/30 bg-primary/5 p-4 shadow-sm">
						<div class="flex items-start gap-3">
							<div class="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary text-primary-content">
								<MapPin size={20} />
							</div>
							<div class="min-w-0 flex-1">
								<p class="text-xs font-semibold uppercase tracking-wide text-primary">Ausgewählter Standort</p>
								<h2 class="mt-0.5 text-lg font-bold">{selectedCompany.name}</h2>
								<p class="mt-1 text-base-content/70">Raum <strong>{selectedCompany.room}</strong>, Etage {selectedCompany.floor}</p>
								{#if selectedCompany.description}
									<p class="mt-2 text-sm text-base-content/60">{selectedCompany.description}</p>
								{/if}
							</div>
						</div>

						<button type="button" class="btn btn-primary btn-sm mt-4 w-full" on:click={centerSelectedCompany}>
							<LocateFixed size={17} />
							Auf dem Plan zentrieren
						</button>
					</section>
				{/if}

				<section class="hidden rounded-2xl border border-base-300 bg-base-100 p-4 text-sm text-base-content/60 shadow-sm lg:block">
					<p class="font-semibold text-base-content">Bedienung</p>
					<p class="mt-2">Ziehe den Plan mit der Maus oder dem Finger. Mit den Schaltflächen kannst du hinein- und herauszoomen.</p>
				</section>
			</aside>

			<main class="min-w-0 overflow-hidden rounded-2xl border border-base-300 bg-base-100 shadow-sm">
				<div class="flex flex-wrap items-center justify-between gap-3 border-b border-base-300 px-3 py-3 sm:px-4">
					<div>
						<p class="font-bold">{currentFloor.name}</p>
						<p class="text-xs text-base-content/55">Punkt auswählen oder Plan verschieben</p>
					</div>

					<div data-map-control class="flex items-center gap-1 rounded-xl border border-base-300 bg-base-200 p-1">
						<button type="button" class="btn btn-square btn-ghost btn-sm" aria-label="Herauszoomen" disabled={zoom <= minimumZoom} on:click={() => setZoom(zoom - zoomStep)}>
							<Minus size={18} />
						</button>
						<span class="w-14 text-center text-sm font-semibold">{Math.round(zoom * 100)} %</span>
						<button type="button" class="btn btn-square btn-ghost btn-sm" aria-label="Hineinzoomen" disabled={zoom >= maximumZoom} on:click={() => setZoom(zoom + zoomStep)}>
							<Plus size={18} />
						</button>
						<div class="mx-1 h-5 w-px bg-base-300"></div>
						<button type="button" class="btn btn-square btn-ghost btn-sm" aria-label="Ansicht zurücksetzen" on:click={resetView}>
							<RotateCcw size={17} />
						</button>
					</div>
				</div>

				<div
					bind:this={planViewport}
					class={[
						'relative h-[58vh] min-h-[390px] overflow-auto bg-base-200/60 touch-none sm:h-[68vh]',
						isPanning ? 'cursor-grabbing' : 'cursor-grab'
					].join(' ')}
					role="application"
					aria-label={`Interaktiver Raumplan, ${currentFloor.name}`}
					on:pointerdown={startPan}
					on:pointermove={movePan}
					on:pointerup={stopPan}
					on:pointercancel={stopPan}
				>
					<svg
						bind:this={planSvg}
						viewBox={currentViewBox}
						class="block h-auto max-w-none select-none"
						style={`width: ${zoom * 100}%; min-width: 100%;`}
						aria-label={`Raumplan für ${currentFloor.name}`}
					>
						<image
							href={currentFloor.image}
							x={currentBounds.minX}
							y={currentBounds.minY}
							width={currentBounds.width}
							height={currentBounds.height}
							preserveAspectRatio="none"
						/>

						{#if selectedCompany && selectedCompany.floor === activeFloor}
							<g
								data-company-marker
								role="button"
								tabindex="0"
								aria-label={`${selectedCompany.name}, Raum ${selectedCompany.room}`}
								class="cursor-pointer outline-none"
								on:pointerdown={(event) => event.stopPropagation()}
								on:click={() => selectCompany(selectedCompany)}
								on:keydown={(event) => handleMarkerKeydown(event, selectedCompany)}
							>
								<title>{selectedCompany.name} – Raum {selectedCompany.room}</title>

								<circle
									cx={selectedCompany.x}
									cy={selectedCompany.y}
									r={markerRadius * 2 / 2.5}
									class="fill-primary/20 stroke-primary"
									stroke-width={markerRadius * 0.18 / 2.5}
								/>

								<circle
									cx={selectedCompany.x}
									cy={selectedCompany.y}
									r={markerRadius * 1.45 / 2.5}
									class="fill-base-100/90 stroke-base-100"
									stroke-width={markerRadius * 0.45 / 2.5}
								/>

								<circle
									cx={selectedCompany.x}
									cy={selectedCompany.y}
									r={markerRadius / 2.5}
									class="fill-primary stroke-primary-content"
									stroke-width={markerRadius * 0.18 / 2.5}
								/>
							</g>
						{/if}
					</svg>

					{#if !floorCompanies.length}
						<div class="pointer-events-none absolute inset-0 flex items-center justify-center p-6">
							<div class="rounded-xl border border-base-300 bg-base-100/95 px-5 py-4 text-center shadow-lg backdrop-blur">
								<p class="font-semibold">Noch keine Unternehmen eingetragen</p>
								<p class="mt-1 text-sm text-base-content/60">Füge Standorte im Array <code>companies</code> hinzu.</p>
							</div>
						</div>
					{/if}
				</div>

				<div class="flex items-center justify-between gap-3 border-t border-base-300 px-4 py-3 text-xs text-base-content/50">
					<span>{floorCompanies.length} {floorCompanies.length === 1 ? 'Standort' : 'Standorte'} auf dieser Etage</span>

					<!-- Nach über einem Jahr hat es „Salve“ doch noch in den Raumplan geschafft. -->
					<span class="select-none opacity-30 transition-opacity duration-300 hover:opacity-100" title="Es lebt noch.">Salve</span>
				</div>
			</main>
		</div>
	</div>
</div>
