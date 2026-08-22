<script lang="ts">
  import CircleAlert from '@lucide/svelte/icons/circle-alert';
  import CircleCheck from '@lucide/svelte/icons/circle-check';

  export let message = '';
  export let type: 'error' | 'success' | 'info' = 'info';
</script>

{#if message}
  <div
    class:alert-error={type === 'error'}
    class:alert-success={type === 'success'}
    class:alert-info={type === 'info'}
    class="alert shadow-sm"
    role="status"
  >
    {#if type === 'success'}
      <CircleCheck size={20} />
    {:else}
      <CircleAlert size={20} />
    {/if}
    <span>{message}</span>
  </div>
{/if}
