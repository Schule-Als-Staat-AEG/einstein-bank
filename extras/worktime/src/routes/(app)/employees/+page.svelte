<script lang="ts">
  import { onMount } from 'svelte';
  import db from '$lib/connections';
  import Alert from '$lib/components/Alert.svelte';
  import EmptyState from '$lib/components/EmptyState.svelte';
  import LoadingState from '$lib/components/LoadingState.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import { errorMessage, formatCurrency } from '$lib/format';
  import type { Employee, Ministry, Salary } from '$lib/types';
  import ChevronDown from '@lucide/svelte/icons/chevron-down';
  import Pencil from '@lucide/svelte/icons/pencil';
  import Plus from '@lucide/svelte/icons/plus';
  import RefreshCw from '@lucide/svelte/icons/refresh-cw';
  import Search from '@lucide/svelte/icons/search';
  import Trash2 from '@lucide/svelte/icons/trash-2';
  import UserRound from '@lucide/svelte/icons/user-round';
  import X from '@lucide/svelte/icons/x';

  let employees: Employee[] = [];
  let ministries: Ministry[] = [];
  let salaries: Salary[] = [];
  let loading = true;
  let saving = false;
  let error = '';
  let success = '';
  let search = '';
  let ministryFilter = '';
  let expandedMinistries = new Set<string>();
  let employeeDialog: HTMLDialogElement;
  let deleteDialog: HTMLDialogElement;
  let editing: Employee | null = null;
  let deleting: Employee | null = null;

  $: ministryGroups = groupByMinistry(employees);

  function groupByMinistry(list: Employee[]): { ministry?: Ministry; employees: Employee[] }[] {
    const groups = new Map<string, { ministry?: Ministry; employees: Employee[] }>();
    for (const employee of list) {
      const key = employee.ministry || 'unknown';
      if (!groups.has(key)) groups.set(key, { ministry: employee.expand?.ministry, employees: [] });
      groups.get(key)!.employees.push(employee);
    }
    return Array.from(groups.values()).sort((a, b) =>
      (a.ministry?.name ?? 'Unknown ministry').localeCompare(b.ministry?.name ?? 'Unknown ministry')
    );
  }

  function toggleMinistry(id: string): void {
    if (expandedMinistries.has(id)) expandedMinistries.delete(id);
    else expandedMinistries.add(id);
    expandedMinistries = expandedMinistries;
  }

  let form = {
    name: '',
    account: '',
    ministry: '',
    salary: ''
  };

  onMount(loadInitial);

  async function loadInitial(): Promise<void> {
    loading = true;
    error = '';

    try {
      const [ministryRecords, salaryRecords] = await Promise.all([
        db.collection('ministries').getFullList<Ministry>({ sort: 'name' }),
        db.collection('salaries').getFullList<Salary>({ sort: 'name,amount' })
      ]);
      ministries = ministryRecords;
      salaries = salaryRecords;
      await loadEmployees();
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      loading = false;
    }
  }

  async function loadEmployees(): Promise<void> {
    error = '';
    const trimmed = search.trim();
    const filters = [];
    if (trimmed) filters.push(db.filter('(name ~ {:query} || account ~ {:query})', { query: trimmed }));
    if (ministryFilter) filters.push(db.filter('ministry = {:ministry}', { ministry: ministryFilter }));

    try {
      employees = await db.collection('employees').getFullList<Employee>({
        filter: filters.join(' && '),
        sort: 'name',
        expand: 'ministry,salary'
      });
    } catch (caught) {
      error = errorMessage(caught);
    }
  }

  function openCreate(): void {
    editing = null;
    form = {
      name: '',
      account: '',
      ministry: ministries[0]?.id ?? '',
      salary: salaries[0]?.id ?? ''
    };
    employeeDialog.showModal();
  }

  function openEdit(employee: Employee): void {
    editing = employee;
    form = {
      name: employee.name,
      account: employee.account,
      ministry: employee.ministry,
      salary: employee.salary
    };
    employeeDialog.showModal();
  }

  async function saveEmployee(): Promise<void> {
    error = '';
    success = '';
    saving = true;

    try {
      const payload = {
        name: form.name.trim(),
        account: form.account.trim(),
        ministry: form.ministry,
        salary: form.salary,
        fired: false
      };

      if (editing) {
        await db.collection('employees').update(editing.id, payload);
        success = `${payload.name} was updated.`;
      } else {
        await db.collection('employees').create(payload);
        success = `${payload.name} was added.`;
      }

      employeeDialog.close();
      await loadEmployees();
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      saving = false;
    }
  }

  function askDelete(employee: Employee): void {
    deleting = employee;
    deleteDialog.showModal();
  }

  async function deleteEmployee(): Promise<void> {
    if (!deleting) return;

    saving = true;
    error = '';
    success = '';

    try {
      const name = deleting.name;
      await db.collection('employees').delete(deleting.id);
      deleteDialog.close();
      deleting = null;
      success = `${name} was removed.`;
      await loadEmployees();
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      saving = false;
    }
  }

  async function submitSearch(): Promise<void> {
    await loadEmployees();
  }
</script>

<svelte:head><title>Employees · Government Worktime</title></svelte:head>

<div class="space-y-6">
  <PageHeader title="Employee management" description="Add, update, and remove employees in the ministries you manage.">
    <button class="btn btn-primary" on:click={openCreate} disabled={!ministries.length || !salaries.length}>
      <Plus size={18} /> Add employee
    </button>
  </PageHeader>

  <Alert message={error} type="error" />
  <Alert message={success} type="success" />

  <section class="card border border-base-content/10 bg-base-100 shadow-sm">
    <div class="card-body gap-4 p-4 sm:p-6">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <form class="join w-full max-w-xl" on:submit|preventDefault={submitSearch}>
          <label class="input input-bordered join-item flex w-full items-center gap-2">
            <Search size={18} class="text-base-content/45" />
            <input bind:value={search} class="grow" placeholder="Search name or account…" />
          </label>
          <select class="select select-bordered join-item" bind:value={ministryFilter} on:change={submitSearch}>
            <option value="">All ministries</option>
            {#each ministries as ministry}<option value={ministry.id}>{ministry.name}</option>{/each}
          </select>
          <button class="btn btn-neutral join-item" type="submit">Search</button>
        </form>
        <button class="btn btn-ghost" on:click={loadInitial} disabled={loading}>
          <RefreshCw size={18} class={loading ? 'animate-spin' : ''} /> Refresh
        </button>
      </div>

      {#if loading}
        <LoadingState label="Loading employees…" />
      {:else if employees.length === 0}
        <EmptyState title="No employees found" description="Try another search or add the first employee." />
      {:else}
        <div class="text-sm text-base-content/55">{employees.length} employee{employees.length === 1 ? '' : 's'}</div>

        {#each ministryGroups as group (group.ministry?.id ?? 'unknown')}
          {@const ministryId = group.ministry?.id ?? 'unknown'}
          {@const collapsed = !expandedMinistries.has(ministryId)}
          <div class="collapse collapse-arrow border border-base-content/10 bg-base-100" class:collapse-open={!collapsed} class:collapse-close={collapsed}>
            <button
              type="button"
              class="collapse-title flex items-center gap-2 pr-10 text-left"
              on:click={() => toggleMinistry(ministryId)}
            >
              <ChevronDown size={17} class="text-base-content/40 transition-transform {collapsed ? '-rotate-90' : ''}" />
              <span class="font-semibold">{group.ministry?.name ?? 'Unknown ministry'}</span>
              <span class="badge badge-ghost">{group.employees.length}</span>
            </button>
            <div class="collapse-content">
              <div class="overflow-x-auto rounded-box border border-base-content/10">
                <table class="table table-zebra">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Account</th>
                      <th>Salary</th>
                      <th>Status</th>
                      <th class="text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {#each group.employees as employee}
                      <tr>
                        <td>
                          <div class="flex items-center gap-3">
                            <div class="avatar placeholder">
                              <div class="flex w-10 items-center justify-center rounded-full bg-base-200 text-base-content/55"><UserRound size={18} /></div>
                            </div>
                            <div class="font-medium">{employee.name}</div>
                          </div>
                        </td>
                        <td class="font-mono text-sm">{employee.account}</td>
                        <td>
                          <div class="font-medium">{employee.expand?.salary?.name ?? '—'}</div>
                          <div class="text-xs text-base-content/50">
                            {formatCurrency(employee.expand?.salary?.amount)} STN / hour
                          </div>
                        </td>
                        <td>
                          <span class:badge-error={employee.fired} class:badge-success={!employee.fired} class="badge badge-soft">
                            {employee.fired ? 'Fired' : 'Active'}
                          </span>
                        </td>
                        <td>
                          <div class="flex justify-end gap-1">
                            <button class="btn btn-square btn-ghost btn-sm" on:click={() => openEdit(employee)} aria-label={`Edit ${employee.name}`}>
                              <Pencil size={17} />
                            </button>
                            <button class="btn btn-square btn-ghost btn-sm text-error" on:click={() => askDelete(employee)} aria-label={`Delete ${employee.name}`}>
                              <Trash2 size={17} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        {/each}
      {/if}
    </div>
  </section>
</div>

<dialog class="modal" bind:this={employeeDialog}>
  <div class="modal-box max-w-2xl">
    <form method="dialog"><button class="btn btn-circle btn-ghost btn-sm absolute right-3 top-3" aria-label="Close"><X size={18} /></button></form>
    <h2 class="text-xl font-bold">{editing ? 'Edit employee' : 'Add employee'}</h2>
    <p class="mt-1 text-sm text-base-content/60">All fields are required by the PocketBase collection.</p>

    <form class="mt-6 grid gap-4 sm:grid-cols-2" on:submit|preventDefault={saveEmployee}>
      <label class="form-control sm:col-span-2">
        <div class="label"><span class="label-text font-medium">Full name</span></div>
        <input class="input input-bordered" bind:value={form.name} required placeholder="Employee name" />
      </label>

      <label class="form-control">
        <div class="label"><span class="label-text font-medium">Account number</span></div>
        <input class="input input-bordered font-mono" bind:value={form.account} placeholder="1234-5678" />
      </label>

      <label class="form-control">
        <div class="label"><span class="label-text font-medium">Ministry</span></div>
        <select class="select select-bordered" bind:value={form.ministry} required>
          {#each ministries as ministry}<option value={ministry.id}>{ministry.name}</option>{/each}
        </select>
      </label>

      <label class="form-control sm:col-span-2">
        <div class="label"><span class="label-text font-medium">Salary</span></div>
        <select class="select select-bordered" bind:value={form.salary} required>
          {#each salaries as salary}
            <option value={salary.id}>{salary.name} — {formatCurrency(salary.amount)} STN / hour</option>
          {/each}
        </select>
      </label>

      <div class="modal-action sm:col-span-2">
        <button type="button" class="btn" on:click={() => employeeDialog.close()}>Cancel</button>
        <button class="btn btn-primary" type="submit" disabled={saving}>
          {#if saving}<span class="loading loading-spinner loading-sm"></span>{/if}
          {editing ? 'Save changes' : 'Add employee'}
        </button>
      </div>
    </form>
  </div>
  <form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>

<dialog class="modal" bind:this={deleteDialog}>
  <div class="modal-box">
    <h2 class="text-xl font-bold">Remove employee?</h2>
    <p class="mt-3 text-base-content/70">
      This permanently deletes <strong>{deleting?.name}</strong>. It can fail if related records are protected by your database rules.
    </p>
    <div class="modal-action">
      <button class="btn" on:click={() => deleteDialog.close()}>Cancel</button>
      <button class="btn btn-error" on:click={deleteEmployee} disabled={saving}>
        {#if saving}<span class="loading loading-spinner loading-sm"></span>{:else}<Trash2 size={18} />{/if}
        Remove
      </button>
    </div>
  </div>
  <form method="dialog" class="modal-backdrop"><button>close</button></form>
</dialog>
