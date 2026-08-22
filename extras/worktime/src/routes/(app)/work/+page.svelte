<script lang="ts">
  import { onDestroy, onMount, tick } from 'svelte';
  import db from '$lib/connections';
  import Alert from '$lib/components/Alert.svelte';
  import EmptyState from '$lib/components/EmptyState.svelte';
  import LoadingState from '$lib/components/LoadingState.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import { errorMessage, formatDuration } from '$lib/format';
  import type { Employee, Ministry, Salary, WorkSession } from '$lib/types';
  import { endSession, startSession } from '$lib/worktime';
  import ChevronDown from '@lucide/svelte/icons/chevron-down';
  import Clock3 from '@lucide/svelte/icons/clock-3';
  import LogIn from '@lucide/svelte/icons/log-in';
  import LogOut from '@lucide/svelte/icons/log-out';
  import RefreshCw from '@lucide/svelte/icons/refresh-cw';
  import Search from '@lucide/svelte/icons/search';
  import UserRound from '@lucide/svelte/icons/user-round';

  let employees: Employee[] = [];
  let sessions: WorkSession[] = [];
  let search = '';
  let loading = true;
  let busyEmployee = '';
  let error = '';
  let success = '';
  let now = new Date();
  let ticker: ReturnType<typeof setInterval>;
  let searchInput: HTMLInputElement;
  let confirmDialog: HTMLDialogElement;
  let confirmButton: HTMLButtonElement | undefined;
  let confirmEmployee: Employee | null = null;
  let highlightedIndex = 0;
  let expandedMinistries = new Set<string>();

  const MAX_SUGGESTIONS = 8;

  $: normalizedSearch = search.trim().toLocaleLowerCase();
  $: activeSessionByEmployee = new Map(sessions.map((session) => [session.employee, session]));
  $: matchingEmployees = employees
    .filter((employee) => !employee.fired)
    .filter((employee) =>
      !normalizedSearch
        ? true
        : employee.name.toLocaleLowerCase().includes(normalizedSearch) || employee.account.includes(normalizedSearch)
    );
  $: ministryGroups = groupByMinistry(matchingEmployees);
  $: suggestions = normalizedSearch ? matchingEmployees.slice(0, MAX_SUGGESTIONS) : [];

  function groupByMinistry(
    list: Employee[]
  ): { ministry?: Ministry; salaryGroups: { salary?: Salary; employees: Employee[] }[] }[] {
    const groups = new Map<string, { ministry?: Ministry; employees: Employee[] }>();
    for (const employee of list) {
      const key = employee.ministry || 'unknown';
      if (!groups.has(key)) groups.set(key, { ministry: employee.expand?.ministry, employees: [] });
      groups.get(key)!.employees.push(employee);
    }
    return Array.from(groups.values())
      .sort((a, b) => (a.ministry?.name ?? 'Unknown ministry').localeCompare(b.ministry?.name ?? 'Unknown ministry'))
      .map((group) => ({ ministry: group.ministry, salaryGroups: groupBySalary(group.employees) }));
  }

  function groupBySalary(list: Employee[]): { salary?: Salary; employees: Employee[] }[] {
    const groups = new Map<string, { salary?: Salary; employees: Employee[] }>();
    for (const employee of list) {
      const key = employee.salary || 'unknown';
      if (!groups.has(key)) groups.set(key, { salary: employee.expand?.salary, employees: [] });
      groups.get(key)!.employees.push(employee);
    }
    for (const group of groups.values()) group.employees.sort((a, b) => a.name.localeCompare(b.name));
    return Array.from(groups.values()).sort(
      (a, b) => (b.salary?.amount ?? -1) - (a.salary?.amount ?? -1)
    );
  }

  onMount(async () => {
    ticker = setInterval(() => (now = new Date()), 1000);
    await loadData();

    try {
      await db.collection('sessions').subscribe('*', () => void loadSessions());
    } catch {
      // The page still works without realtime updates; manual refresh remains available.
    }
  });

  onDestroy(() => {
    clearInterval(ticker);
    void db.collection('sessions').unsubscribe('*');
  });

  async function loadData(): Promise<void> {
    loading = true;
    error = '';

    try {
      const [employeeRecords] = await Promise.all([
        db.collection('employees').getFullList<Employee>({ sort: 'name', expand: 'ministry,salary' }),
        loadSessions()
      ]);
      employees = employeeRecords;
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      loading = false;
    }
  }

  async function loadSessions(): Promise<void> {
    sessions = await db.collection('sessions').getFullList<WorkSession>({
      filter: 'finished = false',
      sort: 'start',
      expand: 'employee'
    });
  }

  async function toggleSession(employee: Employee): Promise<void> {
    const activeSession = activeSessionByEmployee.get(employee.id);
    busyEmployee = employee.id;
    error = '';
    success = '';

    try {
      if (activeSession) {
        await endSession(employee.id);
        success = `${employee.name} was clocked out.`;
      } else {
        await startSession(employee.id);
        success = `${employee.name} was clocked in.`;
      }
      await loadSessions();
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      busyEmployee = '';
    }
  }

  function handleSearchKeydown(event: KeyboardEvent): void {
    if (!suggestions.length) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      highlightedIndex = Math.min(highlightedIndex + 1, suggestions.length - 1);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      highlightedIndex = Math.max(highlightedIndex - 1, 0);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const employee = suggestions[highlightedIndex];
      if (employee) openConfirm(employee);
    }
  }

  function toggleMinistry(id: string): void {
    if (expandedMinistries.has(id)) expandedMinistries.delete(id);
    else expandedMinistries.add(id);
    expandedMinistries = expandedMinistries;
  }

  async function openConfirm(employee: Employee): Promise<void> {
    confirmEmployee = employee;
    confirmDialog.showModal();
    await tick();
    confirmButton?.focus();
  }

  function handleDialogClose(): void {
    confirmEmployee = null;
    searchInput?.focus();
  }

  async function confirmToggle(): Promise<void> {
    if (!confirmEmployee) return;
    await toggleSession(confirmEmployee);
    confirmDialog.close();
  }
</script>

<svelte:head><title>Clock in / out · Government Worktime</title></svelte:head>

<div class="space-y-6">
  <PageHeader title="Clock in / out" description="Search for an employee and start or stop their current work session.">
    <button class="btn btn-ghost" on:click={loadData} disabled={loading}>
      <RefreshCw size={18} class={loading ? 'animate-spin' : ''} /> Refresh
    </button>
  </PageHeader>

  <Alert message={error} type="error" />
  <Alert message={success} type="success" />

  <div class="stats w-full border border-base-content/10 bg-base-100 shadow-sm">
    <div class="stat">
      <div class="stat-figure text-success"><Clock3 size={28} /></div>
      <div class="stat-title">Working now</div>
      <div class="stat-value text-success">{sessions.length}</div>
    </div>
    <div class="stat">
      <div class="stat-figure text-base-content/40"><UserRound size={28} /></div>
      <div class="stat-title">Employees</div>
      <div class="stat-value">{employees.filter((employee) => !employee.fired).length}</div>
    </div>
  </div>

  <div class="relative">
    <label class="input input-bordered flex w-full items-center gap-3 bg-base-100 shadow-sm">
      <Search size={18} class="text-base-content/40" />
      <input
        bind:this={searchInput}
        bind:value={search}
        on:input={() => (highlightedIndex = 0)}
        on:keydown={handleSearchKeydown}
        class="grow"
        placeholder="Enter employee name or account…"
      />
      {#if search}<kbd class="kbd kbd-xs">{matchingEmployees.length}</kbd>{/if}
    </label>

    {#if suggestions.length > 0}
      <ul class="menu absolute z-30 mt-1 max-h-72 w-full overflow-y-auto rounded-box border border-base-content/10 bg-base-100 p-2 shadow-lg">
        {#each suggestions as employee, index}
          {@const isWorking = activeSessionByEmployee.has(employee.id)}
          <li>
            <button
              type="button"
              class="flex items-center justify-between gap-2 {index === highlightedIndex ? 'active' : ''}"
              on:mousedown|preventDefault={() => openConfirm(employee)}
              on:mouseenter={() => (highlightedIndex = index)}
            >
              <span class="truncate">{employee.name}</span>
              <span class="badge badge-xs {isWorking ? 'badge-success' : 'badge-ghost'}">{isWorking ? 'Working' : 'Offline'}</span>
            </button>
          </li>
        {/each}
        <li class="mt-1 px-2 text-xs text-base-content/40">↑↓ to select · Enter to confirm</li>
      </ul>
    {/if}
  </div>

  {#if loading}
    <section class="card border border-base-content/10 bg-base-100 shadow-sm">
      <div class="card-body p-4 sm:p-6"><LoadingState label="Loading employees and sessions…" /></div>
    </section>
  {:else if matchingEmployees.length === 0}
    <section class="card border border-base-content/10 bg-base-100 shadow-sm">
      <div class="card-body p-4 sm:p-6">
        <EmptyState title="No matching employee" description="Check the spelling or try searching by account number." />
      </div>
    </section>
  {:else}
    {#each ministryGroups as group (group.ministry?.id ?? 'unknown')}
      {@const employeeCount = group.salaryGroups.reduce((sum, salaryGroup) => sum + salaryGroup.employees.length, 0)}
      {@const workingCount = group.salaryGroups.reduce(
        (sum, salaryGroup) => sum + salaryGroup.employees.filter((employee) => activeSessionByEmployee.has(employee.id)).length,
        0
      )}
      {@const ministryId = group.ministry?.id ?? 'unknown'}
      {@const collapsed = !expandedMinistries.has(ministryId)}
      <div class="collapse collapse-arrow border border-base-content/10 bg-base-100" class:collapse-open={!collapsed} class:collapse-close={collapsed}>
        <button
          type="button"
          class="collapse-title flex items-center gap-2 pr-10 text-left"
          on:click={() => toggleMinistry(ministryId)}
        >
          <ChevronDown size={17} class="text-base-content/40 transition-transform {collapsed ? '-rotate-90' : ''}" />
          <UserRound size={17} class="text-base-content/40" />
          <span class="font-semibold">{group.ministry?.name ?? 'Unknown ministry'}</span>
          <span class="badge badge-ghost">{employeeCount}</span>
          {#if workingCount > 0}
            <span class="badge badge-success badge-soft">{workingCount} working</span>
          {/if}
        </button>
        <div class="collapse-content">
          <div class="flex flex-col gap-5 pt-2">
          {#each group.salaryGroups as salaryGroup (salaryGroup.salary?.id ?? 'unknown')}
            <div>
              <h3 class="mb-2 text-xs font-semibold uppercase tracking-wide text-base-content/40">
                {salaryGroup.salary?.name ?? 'Unknown class'}
              </h3>
              <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                {#each salaryGroup.employees as employee}
                  {@const activeSession = activeSessionByEmployee.get(employee.id)}
                  <article
                    class="flex items-center gap-3 rounded-box border p-4 transition {activeSession
                      ? 'border-success/30 bg-success/5'
                      : 'border-base-content/10 hover:border-primary/40 hover:shadow-md'}"
                  >
                    <div
                      class="grid size-11 shrink-0 place-items-center rounded-full {activeSession
                        ? 'bg-success text-success-content'
                        : 'bg-base-200 text-base-content/50'}"
                    >
                      <UserRound size={20} />
                    </div>
                    <div class="min-w-0 flex-1">
                      <div class="truncate font-semibold">{employee.name}</div>
                      <span class="badge badge-outline badge-xs mt-1 font-mono">{employee.expand?.salary?.name ?? '—'}</span>
                      {#if activeSession}
                        <div class="mt-1 truncate font-mono text-sm text-success">{formatDuration(activeSession.start, now)}</div>
                      {/if}
                    </div>
                    {#if activeSession}
                      <button class="btn btn-error btn-sm" disabled={busyEmployee === employee.id} on:click={() => toggleSession(employee)}>
                        {#if busyEmployee === employee.id}<span class="loading loading-spinner loading-xs"></span>{:else}<LogOut size={16} />{/if}
                        Clock out
                      </button>
                    {:else}
                      <button class="btn btn-success btn-sm" disabled={busyEmployee === employee.id} on:click={() => toggleSession(employee)}>
                        {#if busyEmployee === employee.id}<span class="loading loading-spinner loading-xs"></span>{:else}<LogIn size={16} />{/if}
                        Clock in
                      </button>
                    {/if}
                  </article>
                {/each}
              </div>
            </div>
          {/each}
          </div>
        </div>
      </div>
    {/each}
  {/if}
</div>

<dialog class="modal" bind:this={confirmDialog} on:close={handleDialogClose}>
  <div class="modal-box max-w-sm">
    {#if confirmEmployee}
      {@const isWorking = activeSessionByEmployee.has(confirmEmployee.id)}
      {@const isBusy = busyEmployee === confirmEmployee.id}
      <h2 class="text-lg font-bold">{isWorking ? 'Clock out' : 'Clock in'} {confirmEmployee.name}?</h2>
      <p class="mt-1 text-sm text-base-content/60">
        Press <kbd class="kbd kbd-xs">Enter</kbd> to confirm or <kbd class="kbd kbd-xs">Esc</kbd> to cancel.
      </p>
      <div class="modal-action">
        <button type="button" class="btn" on:click={() => confirmDialog.close()}>Cancel</button>
        <button
          bind:this={confirmButton}
          class="btn {isWorking ? 'btn-error' : 'btn-success'}"
          disabled={isBusy}
          on:click={confirmToggle}
        >
          {#if isBusy}<span class="loading loading-spinner loading-xs"></span>{:else if isWorking}<LogOut size={16} />{:else}<LogIn size={16} />{/if}
          Confirm
        </button>
      </div>
    {/if}
  </div>
  <form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>
