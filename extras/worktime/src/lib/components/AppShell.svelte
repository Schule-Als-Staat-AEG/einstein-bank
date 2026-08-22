<script lang="ts">
  import { page } from '$app/stores';
  import { goto } from '$app/navigation';
  import { db } from '$lib/connections';
  import Building2 from '@lucide/svelte/icons/building-2';
  import Clock3 from '@lucide/svelte/icons/clock-3';
  import History from '@lucide/svelte/icons/history';
  import LogOut from '@lucide/svelte/icons/log-out';
  import Menu from '@lucide/svelte/icons/menu';
  import RadioTower from '@lucide/svelte/icons/radio-tower';
  import Users from '@lucide/svelte/icons/users';
  import Wallet from '@lucide/svelte/icons/wallet';
  import X from '@lucide/svelte/icons/x';

  let drawerOpen = false;

  function isActive(path: string): boolean {
    return $page.url.pathname === path || $page.url.pathname.startsWith(`${path}/`);
  }

  function closeDrawer(): void {
    drawerOpen = false;
  }

  async function logout(): Promise<void> {
    db.authStore.clear();
    await goto('/login');
  }
</script>

<div class="drawer lg:drawer-open">
  <input id="navigation-drawer" type="checkbox" class="drawer-toggle" bind:checked={drawerOpen} />

  <div class="drawer-content min-h-screen bg-base-300">
    <header class="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-base-content/10 bg-base-100/95 px-4 backdrop-blur lg:hidden">
      <label for="navigation-drawer" class="btn btn-square btn-ghost" aria-label="Open navigation">
        <Menu size={22} />
      </label>
      <div class="flex items-center gap-2 font-semibold">
        <Building2 size={20} class="text-primary" />
        Government Worktime
      </div>
    </header>

    <main class="mx-auto w-full max-w-[1600px] p-4 sm:p-6 lg:p-8">
      <slot />
    </main>
  </div>

  <div class="drawer-side z-40">
    <label for="navigation-drawer" aria-label="Close navigation" class="drawer-overlay"></label>
    <aside class="flex min-h-full w-72 flex-col border-r border-base-content/10 bg-base-100">
      <div class="flex h-20 items-center justify-between border-b border-base-content/10 px-5">
        <div class="flex items-center gap-3">
          <div class="grid size-11 place-items-center rounded-xl bg-primary text-primary-content shadow-lg shadow-primary/20">
            <Building2 size={24} />
          </div>
          <div>
            <div class="font-bold leading-tight">Finance Ministry</div>
            <div class="text-xs text-base-content/55">Employee timekeeping</div>
          </div>
        </div>
        <button class="btn btn-square btn-ghost btn-sm lg:hidden" on:click={closeDrawer} aria-label="Close navigation">
          <X size={20} />
        </button>
      </div>

      <nav class="flex-1 p-4">
        <div class="mb-2 px-3 text-xs font-semibold uppercase tracking-wider text-base-content/40">Management</div>
        <ul class="menu w-full gap-1 p-0">
          <li>
            <a href="/work" class:menu-active={isActive('/work')} on:click={closeDrawer}>
              <RadioTower size={19} />
              Clock in / out
            </a>
          </li>
          <li>
            <a href="/sessions" class:menu-active={isActive('/sessions')} on:click={closeDrawer}>
              <Clock3 size={19} />
              Current sessions
            </a>
          </li>
          <li>
            <a href="/employees" class:menu-active={isActive('/employees')} on:click={closeDrawer}>
              <Users size={19} />
              Employees
            </a>
          </li>
          <li>
            <a href="/logs" class:menu-active={isActive('/logs')} on:click={closeDrawer}>
              <History size={19} />
              Logs
            </a>
          </li>
          <li>
            <a href="/payroll" class:menu-active={isActive('/payroll')} on:click={closeDrawer}>
              <Wallet size={19} />
              Payroll
            </a>
          </li>
        </ul>
      </nav>

      <div class="border-t border-base-content/10 p-4">
        <div class="mb-3 rounded-box bg-base-200 p-3">
          <div class="truncate text-sm font-medium">{db.authStore.record?.email ?? 'Signed in'}</div>
          <div class="text-xs text-base-content/50">Managing employee</div>
        </div>
        <button class="btn btn-ghost w-full justify-start" on:click={logout}>
          <LogOut size={18} />
          Sign out
        </button>
      </div>
    </aside>
  </div>
</div>
