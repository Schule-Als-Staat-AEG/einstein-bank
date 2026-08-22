<script lang="ts">
	import AdminSite from '$modules/AdminSite.svelte';
	import db, { server } from '$lib/connections';
	import { HubConnectionBuilder } from '@microsoft/signalr';
	import { AnsiUp } from 'ansi_up';
	import { onMount } from 'svelte';
	import { browser } from '$app/environment';
	import { Key, Wifi, WifiOff, WifiSync } from '@lucide/svelte';

	let tab = 'settings';

	// Console log state
	let logLines = [];
	let command = '';
	let logBox;
	const ansi_up = new AnsiUp();

	let execModal: HTMLDialogElement;

	let identity = '';
	let password = '';

	// SignalR connection and status
	let connection: any = null;
	let status: 'disconnected' | 'connecting' | 'connected' | 'reconnecting' = 'disconnected';

	onMount(() => {
		if (!browser) return;
		if (!connection) {
			// try auto-connect
			setupSignalR();
		}
	});

	async function setupSignalR() {
		const token = db.authStore.token; // your stored token
		if (!token) {
			status = 'disconnected';
			logLines = [
				...logLines,
				ansi_up.ansi_to_html('\u001b[33mNo token available — cannot connect\u001b[0m')
			];
			return;
		}

		status = 'connecting';

		// create/replace connection
		connection = new HubConnectionBuilder()
			.withUrl(`${server}/api/admin/log?access_token=${encodeURIComponent(token)}`)
			.withAutomaticReconnect()
			.build();

		connection.on('log', (msg) => {
			logLines = [...logLines, msg];
			setTimeout(() => {
				if (logBox) logBox.scrollTop = logBox.scrollHeight;
			}, 0);
		});

		connection.onreconnecting(() => {
			status = 'reconnecting';
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[33mReconnecting...\u001b[0m')];
		});

		connection.onreconnected(() => {
			status = 'connected';
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[32mReconnected\u001b[0m')];
		});

		connection.onclose(() => {
			status = 'disconnected';
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[31mDisconnected\u001b[0m')];
			connection = null;
		});
		connection.onconnected = () => {
			status = 'connected';
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[32mConnected to server log\u001b[0m')];
		};

		try {
			await connection.start();
			status = 'connected';
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[32mConnected to server log\u001b[0m')];
		} catch (e) {
			status = 'disconnected';
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[31mFailed to connect\u001b[0m')];
			connection = null;
		}
	}

	async function connect() {
		if (status === 'connected' || status === 'connecting' || status === 'reconnecting') return;
		await setupSignalR();
	}

	async function disconnect() {
		if (!connection) {
			status = 'disconnected';
			return;
		}
		try {
			await connection.stop();
			status = 'disconnected';
			connection = null;
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[33mManual disconnect\u001b[0m')];
		} catch (e) {
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[31mError during disconnect\u001b[0m')];
		}
	}

	async function reconnect() {
		if (connection) {
			try {
				disconnect();
				status = 'reconnecting';
				await connection.start();
				status = 'connected';
			} catch (e) {
				status = 'disconnected';
				logLines = [...logLines, ansi_up.ansi_to_html('\u001b[31mReconnect failed\u001b[0m')];
			}
		} else {
			await setupSignalR();
		}
	}

	async function sendCommand() {
		if (!command.trim()) return;
		try {
			// Check if the command is exec, exec-append or exec-terminate and show modal if so
			const cmd = command.trim().split(' ')[0];
			if (password == '' || identity == '') {
				if (['exec', 'exec-append', 'exec-terminate'].includes(cmd)) {
					execModal.showModal();
					return;
				}
			}
			await fetch(server + '/api/admin/cmd', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json',
					Authorization: `Bearer ${db.authStore.token}`
				},
				body: JSON.stringify({ command: command, identity: identity, password: password })
			});
			command = '';
		} catch (e) {
			logLines = [...logLines, ansi_up.ansi_to_html('\u001b[31mError sending command\u001b[0m')];
			console.error(e);
		}
	}

	function handleKeydown(e) {
		if (e.key === 'Enter') {
			sendCommand();
		}
	}

	function openAuthModal() {
		execModal.showModal();
	}

	// Font size control (in em)
	let fontSize = 0.8; // default em
</script>

<title>Admin - Server</title>
<AdminSite>
	<!-- Controls: Connect / Disconnect / Reconnect and font-size -->
	<div class="flex items-center gap-2 mb-3 justify-between">
		<h1 class="text-2xl font-bold">Server</h1>
		<div class="flex flex-row items-center">
			<div class="flex gap-1">
				<div class="tooltip tooltip-bottom" data-tip="Authenticate for exec commands">
					<button
						class="btn btn-sm btn-ghost"
						on:click={openAuthModal}
						aria-label="Authenticate for exec"
					>
						<Key />
					</button>
				</div>
				<div class="tooltip tooltip-bottom" data-tip="Connect">
					<button class="btn btn-sm btn-ghost" on:click={connect} aria-label="Connect">
						<Wifi />
					</button>
				</div>
				<div class="tooltip tooltip-bottom" data-tip="Disconnect">
					<button class="btn btn-sm btn-ghost" on:click={disconnect} aria-label="Disconnect">
						<WifiOff />
					</button>
				</div>
				<div class="tooltip tooltip-bottom" data-tip="Reconnect">
					<button class="btn btn-sm btn-ghost" on:click={reconnect} aria-label="Reconnect">
						<WifiSync />
					</button>
				</div>
			</div>

			<div class="ml-4 flex items-center gap-2">
				<!-- svelte-ignore a11y_label_has_associated_control -->
				<label class="text-sm">Font</label>
				<input
					type="range"
					min="0.6"
					max="1.6"
					step="0.05"
					bind:value={fontSize}
					class="range range-sm"
				/>
			</div>

			{#if status === 'connected'}
				<span class="ml-4 badge badge-success">Connected</span>
			{:else if status === 'connecting'}
				<span class="ml-4 badge badge-warning">Connecting...</span>
			{:else if status === 'reconnecting'}
				<span class="ml-4 badge badge-warning">Reconnecting...</span>
			{:else}
				<span class="ml-4 badge badge-error">Disconnected</span>
			{/if}
		</div>
	</div>

	{#if status !== 'connected'}
		<!-- Disconnected panel -->
		<div class="bg-base-200 rounded border border-base-300 p-6 text-center mb-3">
			<!-- Big no-connection icon -->
			<WifiOff class="mx-auto mb-4 text-red-500" size="64" aria-hidden="true" />
			<p class="mb-4">Server console is not connected.</p>
			{#if status === 'connecting' || status === 'reconnecting'}
				<button class="btn btn-primary" on:click={connect} disabled> Connect </button>
			{:else}
				<button class="btn btn-primary" on:click={reconnect}> Reconnect </button>
			{/if}
		</div>
	{:else}
		<!-- Terminal log -->
		<div
			bind:this={logBox}
			class="bg-black text-white font-mono rounded border border-base-300 mb-2 p-2 overflow-y-auto"
			style="height: calc(80vh - 10rem); font-size: {fontSize}em; white-space: pre-wrap; tab-size: 4; -moz-tab-size: 4;"
		>
			{@html logLines.map((line) => ansi_up.ansi_to_html(line)).join('\n')}
		</div>
	{/if}

	<!-- Command line input -->
	<div class="flex">
		<input
			class="flex-1 bg-base-200 text-black font-mono rounded-l px-3 py-2 border border-base-300 outline-none"
			type="text"
			bind:value={command}
			on:keydown={handleKeydown}
			placeholder="Enter command or type 'help' for available commands..."
			autocomplete="off"
		/>
		<button class="bg-blue-600 text-white px-4 py-2 rounded-r" on:click={sendCommand}>Send</button>
	</div>

	<!-- Open the modal using ID.showModal() method -->
	<dialog id="passwordModal" class="modal" bind:this={execModal}>
		<div class="modal-box">
			<h3 class="text-lg font-bold">EXEC AUTHENTICATION</h3>
			<fieldset class="grid grid-cols-1 gap-4 py-4">
				<div class="flex flex-col gap-1">
					<label for="identity" class="text-sm">Identity</label>
					<input
						id="identity"
						type="text"
						bind:value={identity}
						class="input input-bordered w-full"
					/>
				</div>
				<div class="flex flex-col gap-1">
					<label for="password" class="text-sm">Password</label>
					<input
						id="password"
						type="password"
						bind:value={password}
						class="input input-bordered w-full"
					/>
				</div>
				<div class="flex flex-col gap-1">
					<button
						class="btn btn-primary"
						on:click={() => {
							execModal.close();
						}}>Speichern</button
					>
				</div>
			</fieldset>
		</div>
		<form method="dialog" class="modal-backdrop">
			<button>close</button>
		</form>
	</dialog>
</AdminSite>
