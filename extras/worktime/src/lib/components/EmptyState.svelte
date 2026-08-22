<script lang="ts">
  import Inbox from '@lucide/svelte/icons/inbox';
  export let title = 'Nothing here yet';
  export let description = '';
</script>

<div class="flex min-h-52 flex-col items-center justify-center rounded-box border border-dashed border-base-content/20 p-8 text-center">
  <div class="mb-4 rounded-full bg-base-200 p-4 text-base-content/50">
    <Inbox size={30} />
  </div>
  <h2 class="font-semibold">{title}</h2>
  {#if description}<p class="mt-1 max-w-md text-sm text-base-content/60">{description}</p>{/if}
</div>
