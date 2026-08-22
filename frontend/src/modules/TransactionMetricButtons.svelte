<script lang="ts">
	export let value: 'count' | 'amount' = 'count';
</script>

<div
	class="grid grid-cols-2 gap-1 rounded-xl border border-base-300 bg-base-100/70 p-1"
	role="group"
	aria-label="Messgröße auswählen"
>
	<button
		type="button"
		class={`h-8 rounded-lg px-3 text-xs transition-colors ${
			value === 'count'
				? 'bg-[#E34747]/15 font-medium text-[#E34747]'
				: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
		}`}
		aria-pressed={value === 'count'}
		on:click={() => {
			value = 'count';
		}}
	>
		Anzahl
	</button>

	<button
		type="button"
		class={`h-8 rounded-lg px-3 text-xs transition-colors ${
			value === 'amount'
				? 'bg-[#E34747]/15 font-medium text-[#E34747]'
				: 'text-slate-400 hover:bg-base-200 hover:text-slate-200'
		}`}
		aria-pressed={value === 'amount'}
		on:click={() => {
			value = 'amount';
		}}
	>
		Volumen
	</button>
</div>
