<script lang="ts">
  import { onMount } from 'svelte';
  import db from '$lib/connections';
  import Alert from '$lib/components/Alert.svelte';
  import EmptyState from '$lib/components/EmptyState.svelte';
  import LoadingState from '$lib/components/LoadingState.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import { errorMessage, formatDateTime } from '$lib/format';
  import type { WorkLog } from '$lib/types';
  import Download from '@lucide/svelte/icons/download';
  import LogIn from '@lucide/svelte/icons/log-in';
  import LogOut from '@lucide/svelte/icons/log-out';
  import RefreshCw from '@lucide/svelte/icons/refresh-cw';
  import Search from '@lucide/svelte/icons/search';

  let logs: WorkLog[] = [];
  let loading = true;
  let error = '';
  let search = '';
  let action = '';
  let page = 1;
  let perPage = 50;
  let totalPages = 1;
  let totalItems = 0;

  onMount(loadLogs);

  async function loadLogs(): Promise<void> {
    loading = true;
    error = '';

    try {
      const filters: string[] = [];
      if (action) filters.push(db.filter('action = {:action}', { action }));
      if (search.trim()) filters.push(db.filter('employee.name ~ {:search}', { search: search.trim() }));

      const result = await db.collection('log').getList<WorkLog>(page, perPage, {
        filter: filters.join(' && '),
        sort: '-timestamp',
        expand: 'employee,employee.ministry'
      });

      logs = result.items;
      totalPages = result.totalPages;
      totalItems = result.totalItems;
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      loading = false;
    }
  }

  async function applyFilters(): Promise<void> {
    page = 1;
    await loadLogs();
  }

  async function changePage(next: number): Promise<void> {
    page = Math.min(Math.max(next, 1), totalPages || 1);
    await loadLogs();
  }

  function exportCsv(): void {
    const rows = [
      ['Timestamp', 'Employee', 'Account', 'Ministry', 'Action'],
      ...logs.map((log) => [
        log.timestamp,
        log.expand?.employee?.name ?? '',
        log.expand?.employee?.account ?? '',
        log.expand?.employee?.expand?.ministry?.name ?? '',
        log.action
      ])
    ];

    const csv = rows
      .map((row) => row.map((cell) => `"${String(cell).replaceAll('"', '""')}"`).join(','))
      .join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `worktime-logs-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
</script>

<svelte:head><title>Logs · Government Worktime</title></svelte:head>

<div class="space-y-6">
  <PageHeader title="Worktime logs" description="Audit trail of every clock-in and clock-out action.">
    <button class="btn btn-outline" on:click={exportCsv} disabled={!logs.length}><Download size={18} /> Export page</button>
  </PageHeader>

  <Alert message={error} type="error" />

  <section class="card border border-base-content/10 bg-base-100 shadow-sm">
    <div class="card-body gap-4 p-4 sm:p-6">
      <form class="grid gap-3 md:grid-cols-[minmax(240px,1fr)_220px_auto_auto]" on:submit|preventDefault={applyFilters}>
        <label class="input input-bordered flex items-center gap-2">
          <Search size={18} class="text-base-content/45" />
          <input bind:value={search} class="grow" placeholder="Search employee name…" />
        </label>
        <select class="select select-bordered" bind:value={action}>
          <option value="">All actions</option>
          <option value="clocked_in">Clocked in</option>
          <option value="clocked_out">Clocked out</option>
        </select>
        <button class="btn btn-neutral" type="submit">Apply filters</button>
        <button class="btn btn-ghost" type="button" on:click={loadLogs} disabled={loading}>
          <RefreshCw size={18} class={loading ? 'animate-spin' : ''} /> Refresh
        </button>
      </form>

      {#if loading}
        <LoadingState label="Loading logs…" />
      {:else if logs.length === 0}
        <EmptyState title="No log entries found" description="Clock-in and clock-out actions will be recorded here." />
      {:else}
        <div class="overflow-x-auto rounded-box border border-base-content/10">
          <table class="table table-zebra">
            <thead>
              <tr>
                <th>Timestamp</th>
                <th>Employee</th>
                <th>Ministry</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {#each logs as log}
                <tr>
                  <td>{formatDateTime(log.timestamp)}</td>
                  <td>
                    <div class="font-semibold">{log.expand?.employee?.name ?? 'Deleted employee'}</div>
                    <div class="font-mono text-xs text-base-content/45">{log.expand?.employee?.account ?? '—'}</div>
                  </td>
                  <td>{log.expand?.employee?.expand?.ministry?.name ?? '—'}</td>
                  <td>
                    {#if log.action === 'clocked_in'}
                      <span class="badge badge-success badge-soft gap-1"><LogIn size={14} /> Clocked in</span>
                    {:else}
                      <span class="badge badge-error badge-soft gap-1"><LogOut size={14} /> Clocked out</span>
                    {/if}
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>

        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div class="text-sm text-base-content/55">{totalItems} log entr{totalItems === 1 ? 'y' : 'ies'}</div>
          <div class="join">
            <button class="btn join-item btn-sm" disabled={page <= 1} on:click={() => changePage(page - 1)}>Previous</button>
            <button class="btn join-item btn-sm pointer-events-none">Page {page} of {Math.max(totalPages, 1)}</button>
            <button class="btn join-item btn-sm" disabled={page >= totalPages} on:click={() => changePage(page + 1)}>Next</button>
          </div>
        </div>
      {/if}
    </div>
  </section>
</div>
