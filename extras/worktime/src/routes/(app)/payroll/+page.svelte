<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import db from '$lib/connections';
  import Alert from '$lib/components/Alert.svelte';
  import EmptyState from '$lib/components/EmptyState.svelte';
  import LoadingState from '$lib/components/LoadingState.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import { errorMessage, formatCurrency } from '$lib/format';
  import type { Employee, Ministry, WorkSession } from '$lib/types';
  import ChevronDown from '@lucide/svelte/icons/chevron-down';
  import RefreshCw from '@lucide/svelte/icons/refresh-cw';
  import Users from '@lucide/svelte/icons/users';
  import Wallet from '@lucide/svelte/icons/wallet';

  const FIVE_MINUTE_MS = 5 * 60 * 1000;

  type PayrollEntry = {
    employee: Employee;
    workedMs: number;
    payableHours: number;
    pay: number;
  };

  let sessions: WorkSession[] = [];
  let loading = true;
  let error = '';
  let now = new Date();
  let ticker: ReturnType<typeof setInterval>;
  let collapsedMinistries = new Set<string>();

  $: ministryGroups = groupByMinistry(buildPayroll(sessions, now));
  $: totalPay = ministryGroups.reduce((sum, group) => sum + group.entries.reduce((s, entry) => s + entry.pay, 0), 0);

  onMount(async () => {
    ticker = setInterval(() => (now = new Date()), 1000);
    await loadSessions();
  });

  onDestroy(() => clearInterval(ticker));

  async function loadSessions(): Promise<void> {
    loading = true;
    error = '';

    try {
      sessions = await db.collection('sessions').getFullList<WorkSession>({
        sort: 'start',
        expand: 'employee,employee.ministry,employee.salary'
      });
    } catch (caught) {
      error = errorMessage(caught);
    } finally {
      loading = false;
    }
  }

  function buildPayroll(sessionList: WorkSession[], reference: Date): PayrollEntry[] {
    const workedMsByEmployee = new Map<string, { employee: Employee; workedMs: number }>();

    for (const session of sessionList) {
      const employee = session.expand?.employee;
      if (!employee) continue;

      const start = new Date(session.start).getTime();
      const end = session.finished && session.end ? new Date(session.end).getTime() : reference.getTime();
      const durationMs = Math.max(0, end - start);

      const entry = workedMsByEmployee.get(employee.id) ?? { employee, workedMs: 0 };
      entry.workedMs += durationMs;
      workedMsByEmployee.set(employee.id, entry);
    }

    return Array.from(workedMsByEmployee.values()).map(({ employee, workedMs }) => {
      const payableFiveMinuteBlocks = Math.floor(workedMs / FIVE_MINUTE_MS);
      const payableHours = (payableFiveMinuteBlocks * FIVE_MINUTE_MS) / (60 * 60 * 1000);
      const rate = employee.expand?.salary?.amount ?? 0;
      const pay = Math.round(payableHours * rate * 100) / 100;
      return { employee, workedMs, payableHours, pay };
    });
  }

  function groupByMinistry(entries: PayrollEntry[]): { ministry?: Ministry; entries: PayrollEntry[] }[] {
    const groups = new Map<string, { ministry?: Ministry; entries: PayrollEntry[] }>();
    for (const entry of entries) {
      const key = entry.employee.ministry || 'unknown';
      if (!groups.has(key)) groups.set(key, { ministry: entry.employee.expand?.ministry, entries: [] });
      groups.get(key)!.entries.push(entry);
    }
    for (const group of groups.values()) {
      group.entries.sort((a, b) => a.employee.name.localeCompare(b.employee.name));
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

  function formatWorkedTime(ms: number): string {
    const totalMinutes = Math.floor(ms / 60000);
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return `${hours}h ${String(minutes).padStart(2, '0')}m`;
  }
</script>

<svelte:head><title>Payroll · Government Worktime</title></svelte:head>

<div class="space-y-6">
  <PageHeader title="Payroll" description="Salary owed per employee, based on their salary class and worked time. Value rounded to the nearest 5 Minute Block (up and down).">
    <button class="btn btn-ghost" on:click={loadSessions} disabled={loading}>
      <RefreshCw size={18} class={loading ? 'animate-spin' : ''} /> Refresh
    </button>
  </PageHeader>

  <Alert message={error} type="error" />

  <div class="stats w-full border border-base-content/10 bg-base-100 shadow-sm">
    <div class="stat">
      <div class="stat-figure text-primary"><Wallet size={28} /></div>
      <div class="stat-title">Total payroll</div>
      <div class="stat-value text-primary">{formatCurrency(totalPay)} STN</div>
    </div>
    <div class="stat">
      <div class="stat-figure text-base-content/40"><Users size={28} /></div>
      <div class="stat-title">Employees with worked time</div>
      <div class="stat-value">{ministryGroups.reduce((sum, group) => sum + group.entries.length, 0)}</div>
    </div>
  </div>

  <section class="card border border-base-content/10 bg-base-100 shadow-sm">
    <div class="card-body gap-3 p-4 sm:p-6">
      {#if loading}
        <LoadingState label="Loading sessions…" />
      {:else if ministryGroups.length === 0}
        <EmptyState title="No worked time yet" description="Payroll will appear here once employees start clocking in." />
      {:else}
        {#each ministryGroups as group (group.ministry?.id ?? 'unknown')}
          {@const ministryId = group.ministry?.id ?? 'unknown'}
          {@const collapsed = collapsedMinistries.has(ministryId)}
          {@const ministryTotal = group.entries.reduce((sum, entry) => sum + entry.pay, 0)}
          <div class="collapse collapse-arrow border border-base-content/10 bg-base-100" class:collapse-open={!collapsed} class:collapse-close={collapsed}>
            <button
              type="button"
              class="collapse-title flex items-center gap-2 pr-10 text-left"
              on:click={() => toggleMinistry(ministryId)}
            >
              <ChevronDown size={17} class="text-base-content/40 transition-transform {collapsed ? '-rotate-90' : ''}" />
              <span class="font-semibold">{group.ministry?.name ?? 'Unknown ministry'}</span>
              <span class="badge badge-ghost">{group.entries.length}</span>
              <span class="badge badge-primary badge-soft font-mono">{formatCurrency(ministryTotal)} STN</span>
            </button>
            <div class="collapse-content">
              <div class="overflow-x-auto rounded-box border border-base-content/10">
                <table class="table table-zebra">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Salary class</th>
                      <th>Worked time</th>
                      <th>Payable time</th>
                      <th class="text-right">Pay</th>
                    </tr>
                  </thead>
                  <tbody>
                    {#each group.entries as entry}
                      <tr>
                        <td class="font-medium">{entry.employee.name}</td>
                        <td>
                          <div class="font-medium">{entry.employee.expand?.salary?.name ?? '—'}</div>
                          <div class="text-xs text-base-content/50">
                            {formatCurrency(entry.employee.expand?.salary?.amount)} STN / hour
                          </div>
                        </td>
                        <td class="font-mono text-sm">{formatWorkedTime(entry.workedMs)}</td>
                        <td class="font-mono text-sm text-base-content/60">{entry.payableHours.toFixed(2)} h</td>
                        <td class="text-right font-mono font-semibold">{formatCurrency(entry.pay)} STN</td>
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
