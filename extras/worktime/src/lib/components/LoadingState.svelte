<script lang="ts">
  export let label = 'Loading…';
</script>

<div class="flex min-h-52 flex-col items-center justify-center gap-3 text-base-content/60">
  <span class="loading loading-spinner loading-lg"></span>
  <span class="text-sm">{label}</span>
</div>
