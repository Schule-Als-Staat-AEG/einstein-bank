<script lang="ts">
  import { goto } from '$app/navigation';
  import { onMount } from 'svelte';
  import { db } from '$lib/connections';
  import Alert from '$lib/components/Alert.svelte';
  import { errorMessage } from '$lib/format';
  import Building2 from '@lucide/svelte/icons/building-2';
  import KeyRound from '@lucide/svelte/icons/key-round';
  import LogIn from '@lucide/svelte/icons/log-in';
  import Mail from '@lucide/svelte/icons/mail';

  let email = '';
  let password = '';
  let loading = false;
  let error = '';

  onMount(() => {
    if (db.authStore.isValid) void goto('/work');
  });

  async function login(): Promise<void> {
    error = '';
    loading = true;

    try {
      await db.collection('users').authWithPassword(email.trim(), password);
      await goto('/work');
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      loading = false;
    }
  }
</script>

<svelte:head><title>Anmelden · Government Worktime</title></svelte:head>

<div class="grid min-h-screen bg-base-300 lg:grid-cols-2">
  <div class="relative hidden flex-col justify-between overflow-hidden bg-primary p-10 text-primary-content lg:flex">
    <div class="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_15%_15%,white_0,transparent_25%),radial-gradient(circle_at_85%_85%,var(--color-secondary)_0,transparent_35%)] opacity-30"></div>

    <div class="relative z-10 flex items-center gap-3">
      <div class="grid size-11 place-items-center rounded-xl bg-primary-content/15">
        <Building2 size={24} />
      </div>
      <span class="text-lg font-bold">Government Worktime</span>
    </div>

    <div class="relative z-10 max-w-sm">
      <h2 class="text-3xl font-bold leading-tight">Arbeitszeiten zuverlässig erfassen.</h2>
      <p class="mt-3 text-primary-content/75">Ein einfacher, verlässlicher Weg für verantwortliche Mitarbeiter, Arbeitszeiten zu erfassen und einzusehen.</p>
    </div>

    <p class="relative z-10 text-sm text-primary-content/60">© {new Date().getFullYear()} Government Worktime</p>
  </div>

  <div class="grid place-items-center p-4">
    <div class="w-full max-w-md">
      <div class="mb-8 text-center lg:text-left">
        <div class="mx-auto mb-4 grid size-14 place-items-center rounded-2xl bg-primary text-primary-content shadow-xl shadow-primary/25 lg:hidden">
          <Building2 size={28} />
        </div>
        <h1 class="text-2xl font-bold">Willkommen zurück</h1>
        <p class="mt-2 text-sm text-base-content/60">Melde dich mit deinem Mitarbeiterkonto an.</p>
      </div>

      <Alert message={error} type="error" />

      <form class="mt-6 space-y-4" on:submit|preventDefault={login}>
        <div class="form-control w-full">
          <div class="label"><span class="label-text font-medium">E-Mail-Adresse</span></div>
          <label class="input input-bordered flex w-full items-center gap-3">
            <Mail size={18} class="text-base-content/45" />
            <input bind:value={email} type="email" autocomplete="username" required class="grow" placeholder="manager@schule.example" />
          </label>
        </div>

        <div class="form-control w-full">
          <div class="label"><span class="label-text font-medium">Passwort</span></div>
          <label class="input input-bordered flex w-full items-center gap-3">
            <KeyRound size={18} class="text-base-content/45" />
            <input bind:value={password} type="password" autocomplete="current-password" required minlength="8" class="grow" placeholder="••••••••" />
          </label>
        </div>

        <button class="btn btn-primary mt-2 w-full" type="submit" disabled={loading}>
          {#if loading}<span class="loading loading-spinner loading-sm"></span>{:else}<LogIn size={19} />{/if}
          {loading ? 'Anmelden…' : 'Anmelden'}
        </button>
      </form>
    </div>
  </div>
</div>
