<script lang="ts">
  import { goto } from '$app/navigation';
  import { onMount } from 'svelte';
  import AppShell from '$lib/components/AppShell.svelte';
  import { db } from '$lib/connections';

  let ready = false;

  onMount(async () => {
    if (!db.authStore.isValid) {
      await goto('/login');
      return;
    }

    try {
      await db.collection('users').authRefresh();
      ready = true;
    } catch {
      db.authStore.clear();
      await goto('/login');
    }
  });
</script>

{#if ready}
  <AppShell><slot /></AppShell>
{:else}
  <div class="grid min-h-screen place-items-center bg-base-300">
    <div class="flex flex-col items-center gap-3 text-base-content/60">
      <span class="loading loading-spinner loading-lg"></span>
      <span class="text-sm">Checking your session…</span>
    </div>
  </div>
{/if}
