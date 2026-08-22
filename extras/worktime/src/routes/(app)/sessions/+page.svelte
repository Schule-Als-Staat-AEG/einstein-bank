<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import db from '$lib/connections';
  import Alert from '$lib/components/Alert.svelte';
  import EmptyState from '$lib/components/EmptyState.svelte';
  import LoadingState from '$lib/components/LoadingState.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import { errorMessage, formatDateTime, formatDuration } from '$lib/format';
  import type { Ministry, WorkSession } from '$lib/types';
  import { endSession } from '$lib/worktime';
  import ChevronDown from '@lucide/svelte/icons/chevron-down';
  import Clock3 from '@lucide/svelte/icons/clock-3';
  import LogOut from '@lucide/svelte/icons/log-out';
  import RefreshCw from '@lucide/svelte/icons/refresh-cw';

  let sessions: WorkSession[] = [];
  let loading = true;
  let ending = '';
  let error = '';
  let success = '';
  let now = new Date();
  let ticker: ReturnType<typeof setInterval>;
  let collapsedMinistries = new Set<string>();

  $: ministryGroups = groupByMinistry(sessions);

  function groupByMinistry(list: WorkSession[]): { ministry?: Ministry; sessions: WorkSession[] }[] {
    const groups = new Map<string, { ministry?: Ministry; sessions: WorkSession[] }>();
    for (const session of list) {
      const key = session.expand?.employee?.ministry || 'unknown';
      if (!groups.has(key)) groups.set(key, { ministry: session.expand?.employee?.expand?.ministry, sessions: [] });
      groups.get(key)!.sessions.push(session);
    }
    return Array.from(groups.values()).sort((a, b) =>
      (a.ministry?.name ?? 'Unknown ministry').localeCompare(b.ministry?.name ?? 'Unknown ministry')
    );
  }

  function toggleMinistry(id: string): void {
    if (collapsedMinistries.has(id)) collapsedMinistries.delete(id);
    else collapsedMinistries.add(id);
    collapsedMinistries = collapsedMinistries;
  }

  onMount(async () => {
    ticker = setInterval(() => (now = new Date()), 1000);
    await loadSessions();

    try {
      await db.collection('sessions').subscribe('*', () => void loadSessions());
    } catch {
      // Manual refresh still works if realtime access is unavailable.
    }
  });

  onDestroy(() => {
    clearInterval(ticker);
    void db.collection('sessions').unsubscribe('*');
  });

  async function loadSessions(): Promise<void> {
    loading = true;
    error = '';

    try {
      sessions = await db.collection('sessions').getFullList<WorkSession>({
        filter: 'finished = false',
        sort: 'start',
        expand: 'employee,employee.ministry,employee.salary'
      });
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      loading = false;
    }
  }

  async function clockOut(session: WorkSession): Promise<void> {
    ending = session.id;
    error = '';
    success = '';

    try {
      await endSession(session.employee);
      success = `${session.expand?.employee?.name ?? 'Employee'} was clocked out.`;
      await loadSessions();
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      ending = '';
    }
  }
</script>

<svelte:head><title>Current sessions · Government Worktime</title></svelte:head>

<div class="space-y-6">
  <PageHeader title="Current sessions" description="Everyone who is currently clocked in, updated live.">
    <button class="btn btn-ghost" on:click={loadSessions} disabled={loading}>
      <RefreshCw size={18} class={loading ? 'animate-spin' : ''} /> Refresh
    </button>
  </PageHeader>

  <Alert message={error} type="error" />
  <Alert message={success} type="success" />

  <div class="stats w-full border border-base-content/10 bg-base-100 shadow-sm">
    <div class="stat">
      <div class="stat-figure text-success"><Clock3 size={30} /></div>
      <div class="stat-title">Employees working</div>
      <div class="stat-value text-success">{sessions.length}</div>
      <div class="stat-desc">Active right now</div>
    </div>
  </div>

  <section class="card border border-base-content/10 bg-base-100 shadow-sm">
    <div class="card-body p-4 sm:p-6">
      {#if loading}
        <LoadingState label="Loading active sessions…" />
      {:else if sessions.length === 0}
        <EmptyState title="Nobody is clocked in" description="New sessions will appear here as soon as an employee starts work." />
      {:else}
        <div class="flex flex-col gap-3">
          {#each ministryGroups as group (group.ministry?.id ?? 'unknown')}
            {@const ministryId = group.ministry?.id ?? 'unknown'}
            {@const collapsed = collapsedMinistries.has(ministryId)}
            <div class="collapse collapse-arrow border border-base-content/10 bg-base-100" class:collapse-open={!collapsed} class:collapse-close={collapsed}>
              <button
                type="button"
                class="collapse-title flex items-center gap-2 pr-10 text-left"
                on:click={() => toggleMinistry(ministryId)}
              >
                <ChevronDown size={17} class="text-base-content/40 transition-transform {collapsed ? '-rotate-90' : ''}" />
                <span class="font-semibold">{group.ministry?.name ?? 'Unknown ministry'}</span>
                <span class="badge badge-success badge-soft">{group.sessions.length}</span>
              </button>
              <div class="collapse-content">
                <div class="overflow-x-auto rounded-box border border-base-content/10">
                  <table class="table table-zebra">
                    <thead>
                      <tr>
                        <th>Employee</th>
                        <th>Started</th>
                        <th>Duration</th>
                        <th class="text-right">Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {#each group.sessions as session}
                        <tr>
                          <td>
                            <div class="font-semibold">{session.expand?.employee?.name ?? 'Unknown employee'}</div>
                            <div class="font-mono text-xs text-base-content/45">{session.expand?.employee?.account ?? '—'}</div>
                          </td>
                          <td>{formatDateTime(session.start)}</td>
                          <td><span class="badge badge-success badge-soft font-mono">{formatDuration(session.start, now)}</span></td>
                          <td class="text-right">
                            <button class="btn btn-error btn-sm" on:click={() => clockOut(session)} disabled={ending === session.id}>
                              {#if ending === session.id}<span class="loading loading-spinner loading-xs"></span>{:else}<LogOut size={16} />{/if}
                              Clock out
                            </button>
                          </td>
                        </tr>
                      {/each}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </div>
  </section>
</div>
